using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Persistence;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventorySnapshotRestorationTests
{
    [Test]
    public void ExactRestore_PreservesStacksMetadataStorageOrderAndSlotPlacement_InOneEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new SlotLayout<string>(4),
            new UnlimitedCapacityPolicy<string>(),
            apple,
            berry);
        var source = manager.CreateInventory(layout: new SlotLayout<string>(4));
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "rare");
        var add = InventoryTransaction<string>.From(source);
        Assert.That(add.TryAdd(apple, 4, SlotLayoutContext<string>.Single(3), metadata, out var error), Is.True);
        Assert.That(add.TryAdd(berry, 2, SlotLayoutContext<string>.Single(1), null, out error), Is.True);
        source.CommitTransaction(add);
        var snapshot = source.CaptureSnapshot();

        var target = manager.CreateInventory(layout: new SlotLayout<string>(4));
        target.Add(berry, context: SlotLayoutContext<string>.Single(0));
        var oldInstance = target.Items.Single();
        var events = new List<InventoryChangedEventArgs<string>>();
        target.Changed += (_, args) =>
        {
            events.Add(args);
            Assert.That(target.Items, Has.Count.EqualTo(2));
        };

        var result = target.RestoreSnapshot(snapshot);

        Assert.That(result.Outcome, Is.EqualTo(SnapshotApplicationOutcome.Exact));
        Assert.That(target.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "apple", "berry" }));
        Assert.That(target.Items.Select(item => item.Amount), Is.EqualTo(new[] { 4, 2 }));
        Assert.That(target.Items[0].Metadata.TryGet("quality", out string quality), Is.True);
        Assert.That(quality, Is.EqualTo("rare"));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 0).Single()).SlotIndex, Is.EqualTo(3));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 1).Single()).SlotIndex, Is.EqualTo(1));
        Assert.That(target.Items.Any(item => item.InstanceId == oldInstance.InstanceId), Is.False);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Origin, Is.EqualTo(InventoryChangeOrigin.SnapshotExactRestore));
        Assert.That(events[0].Removed, Has.Count.EqualTo(1));
        Assert.That(events[0].Added, Has.Count.EqualTo(2));
        Assert.That(events[0].Moved, Is.Empty);
    }

    [TestCase(SnapshotApplicationMode.Exact)]
    [TestCase(SnapshotApplicationMode.Reconcile)]
    [TestCase(SnapshotApplicationMode.Salvage)]
    public void SnapshotApplication_PreservesRootMetadataAndEmitsItInTheSingleEvent(
        SnapshotApplicationMode mode)
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = manager.CreateInventory();
        source.Metadata.Set("ownerId", "player-7");
        source.Metadata.Set("upgrades", new List<int> { 1, 3 });
        source.Add(apple, 2);
        var snapshot = source.CaptureSnapshot();

        var target = manager.CreateInventory();
        target.Metadata.Set("ownerId", "old-owner");
        var stableMetadata = target.Metadata;
        var events = new List<InventoryChangedEventArgs<string>>();
        target.Changed += (_, args) => events.Add(args);

        SnapshotApplicationResult result = mode switch
        {
            SnapshotApplicationMode.Exact => target.RestoreSnapshot(snapshot),
            SnapshotApplicationMode.Reconcile => target.ReconcileSnapshot(snapshot),
            _ => target.SalvageSnapshot(snapshot)
        };

        Assert.That(result.RestoredInstanceCount, Is.EqualTo(1));
        Assert.That(target.Metadata, Is.SameAs(stableMetadata));
        Assert.That(target.Metadata.TryGet<string>("ownerId", out var owner), Is.True);
        Assert.That(owner, Is.EqualTo("player-7"));
        Assert.That(target.Metadata.TryGet<List<int>>("upgrades", out var upgrades), Is.True);
        Assert.That(upgrades, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].InventoryMetadataChanged, Is.Not.Null);
        Assert.That(events[0].InventoryMetadataChanged!.ChangedKeys, Is.EqualTo(new[] { "ownerId", "upgrades" }));
    }

    [Test]
    public void MetadataOnlyExactRestore_EmitsOneSnapshotOriginEvent()
    {
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>());
        var source = manager.CreateInventory();
        source.Metadata.Set("name", "Vault");
        var target = manager.CreateInventory();
        InventoryChangedEventArgs<string>? captured = null;
        target.Changed += (_, args) => captured = args;

        target.RestoreSnapshot(source.CaptureSnapshot());

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Origin, Is.EqualTo(InventoryChangeOrigin.SnapshotExactRestore));
        Assert.That(captured.InventoryMetadataChanged, Is.Not.Null);
        Assert.That(captured.Added, Is.Empty);
        Assert.That(captured.Removed, Is.Empty);
    }

    [Test]
    public void Assessment_ReportsReconciliationWhenCurrentStackLimitRequiresSplitting()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 8);

        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(5),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var target = targetManager.CreateInventory();

        var assessment = target.AssessSnapshot(source.CaptureSnapshot());

        Assert.That(assessment.CanRestoreExactly, Is.False);
        Assert.That(assessment.CanReconcileWithoutLoss, Is.True);
        Assert.That(assessment.BestOutcome, Is.EqualTo(SnapshotApplicationOutcome.Reconciled));

        var result = target.ReconcileSnapshot(source.CaptureSnapshot());
        Assert.That(result.HasLosses, Is.False);
        Assert.That(target.Items.Select(item => item.Amount), Is.EqualTo(new[] { 5, 3 }));
    }

    [Test]
    public void Reconciliation_IgnoresSavedPlacementButRetainsAllQuantity()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new SlotLayout<string>(4),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = manager.CreateInventory(layout: new SlotLayout<string>(4));
        source.Add(apple, 2, SlotLayoutContext<string>.Single(3));
        var snapshot = source.CaptureSnapshot();
        var target = manager.CreateInventory(layout: new SlotLayout<string>(2));

        var assessment = target.AssessSnapshot(snapshot);
        Assert.That(assessment.CanRestoreExactly, Is.False);
        Assert.That(assessment.CanReconcileWithoutLoss, Is.True);

        target.ReconcileSnapshot(snapshot);
        Assert.That(target.TotalItemCount, Is.EqualTo(2));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 0).Single()).SlotIndex, Is.EqualTo(0));
    }

    [Test]
    public void Salvage_RetainsDeterministicPartialQuantityAndReportsLoss()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 8);

        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new MaxTotalItemAmountCapacityPolicy<string>(3),
            apple);
        var target = targetManager.CreateInventory();
        var events = new List<InventoryChangedEventArgs<string>>();
        target.Changed += (_, args) => events.Add(args);

        var assessment = target.AssessSnapshot(source.CaptureSnapshot());
        Assert.That(assessment.CanReconcileWithoutLoss, Is.False);
        Assert.That(assessment.CanSalvage, Is.True);
        Assert.That(assessment.ProjectedLosses.Single().Quantity, Is.EqualTo(5));

        var result = target.SalvageSnapshot(source.CaptureSnapshot());
        Assert.That(result.Outcome, Is.EqualTo(SnapshotApplicationOutcome.Salvaged));
        Assert.That(result.Losses.Single().Quantity, Is.EqualTo(5));
        Assert.That(target.TotalItemCount, Is.EqualTo(3));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Origin, Is.EqualTo(InventoryChangeOrigin.SnapshotSalvage));
    }

    [Test]
    public void FailedApplication_IsAtomicAndEmitsNoEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple,
            berry);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 5);

        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new MaxTotalItemAmountCapacityPolicy<string>(1),
            apple,
            berry);
        var target = targetManager.CreateInventory();
        target.Add(berry);
        var originalId = target.Items.Single().InstanceId;
        int eventCount = 0;
        target.Changed += (_, _) => eventCount++;

        Assert.That(target.TryReconcileSnapshot(source.CaptureSnapshot(), out var result, out var error), Is.False);
        Assert.That(result, Is.Null);
        Assert.That(error?.Message, Does.Contain("Capacity"));
        Assert.That(target.Items.Single().InstanceId, Is.EqualTo(originalId));
        Assert.That(eventCount, Is.Zero);
    }

    [Test]
    public void Application_RevalidatesAfterAssessment()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new MaxTotalItemAmountCapacityPolicy<string>(10),
            apple);
        var source = manager.CreateInventory();
        source.Add(apple, 5);
        var target = manager.CreateInventory();
        var snapshot = source.CaptureSnapshot();
        Assert.That(target.AssessSnapshot(snapshot).CanRestoreExactly, Is.True);

        Assert.That(target.TrySetCapacityPolicyParameter("maxTotalItemAmount", 2, out var parameterError), Is.True);

        Assert.That(target.TryRestoreSnapshot(snapshot, out _, out var restoreError), Is.False);
        Assert.That(restoreError?.Message, Does.Contain("Capacity"));
        Assert.That(target.Items, Is.Empty);
    }

    [Test]
    public void ExactRestore_SupportsEveryBuiltInLayout()
    {
        var layouts = new Func<IInventoryLayout<string>>[]
        {
            () => new EntryLayout<string>(),
            () => new SlotLayout<string>(2),
            () => new GridLayout<string>(2, 2),
            () => new MultiCellGridLayout<string>(2, 2, new UnitFootprintProvider()),
            () => new EquipmentLayout<string>(new EquipmentSlot<string>("head")),
            () => new SectionedLayout<string>(new SectionDefinition<string>("bag", 2))
        };

        foreach (var createLayout in layouts)
        {
            var apple = new ItemDefinition<string>("apple");
            var manager = CreateManager(
                new FixedSizeStackResolver<string>(10),
                createLayout(),
                new UnlimitedCapacityPolicy<string>(),
                apple);
            var source = manager.CreateInventory(layout: createLayout());
            source.Add(apple);
            var target = manager.CreateInventory(layout: createLayout());

            Assert.That(target.TryRestoreSnapshot(source.CaptureSnapshot(), out var result, out var error), Is.True);
            Assert.That(result!.Outcome, Is.EqualTo(SnapshotApplicationOutcome.Exact));
            Assert.That(target.TotalItemCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void MultiCellExactRestore_PreservesOccupiedCellsAndRejectsChangedFootprint()
    {
        var crate = new ItemDefinition<string>("crate");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new MultiCellGridLayout<string>(4, 4, new FixedFootprintProvider(2, 2)),
            new UnlimitedCapacityPolicy<string>(),
            crate);
        var source = manager.CreateInventory(
            layout: new MultiCellGridLayout<string>(4, 4, new FixedFootprintProvider(2, 2)));
        source.Add(
            crate,
            context: new MultiCellGridLayoutContext<string>(2, 2, GridAnchor.BottomRight));
        var expectedCells = source.Layout.GetContextsForStorageIndex(source, 0)
            .Cast<MultiCellGridLayoutContext<string>>()
            .Select(cell => (cell.X, cell.Y))
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();
        var snapshot = source.CaptureSnapshot();

        var exactTarget = manager.CreateInventory(
            layout: new MultiCellGridLayout<string>(4, 4, new FixedFootprintProvider(2, 2)));
        exactTarget.RestoreSnapshot(snapshot);
        var actualCells = exactTarget.Layout.GetContextsForStorageIndex(exactTarget, 0)
            .Cast<MultiCellGridLayoutContext<string>>()
            .Select(cell => (cell.X, cell.Y))
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();
        Assert.That(actualCells, Is.EqualTo(expectedCells));

        var changedTarget = manager.CreateInventory(
            layout: new MultiCellGridLayout<string>(4, 4, new FixedFootprintProvider(1, 1)));
        Assert.That(changedTarget.AssessSnapshot(snapshot).CanRestoreExactly, Is.False);
        Assert.That(changedTarget.AssessSnapshot(snapshot).CanReconcileWithoutLoss, Is.True);
    }

    [Test]
    public void Restoration_ResolvesDefinitionMigrations()
    {
        var oldApple = new ItemDefinition<string>("old-apple");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            oldApple);
        var source = sourceManager.CreateInventory();
        source.Add(oldApple, 2);

        var apple = new ItemDefinition<string>("apple");
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(apple);
        catalog.Registry.RegisterMigration("old-apple", apple);
        catalog.Freeze();
        var targetManager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        var target = targetManager.CreateInventory();

        target.RestoreSnapshot(source.CaptureSnapshot());

        Assert.That(target.Items.Single().Definition, Is.SameAs(apple));
        Assert.That(target.Items.Single().Amount, Is.EqualTo(2));
    }

    [Test]
    public void Salvage_UnknownDefinitionsRequireExplicitDiscardPermission()
    {
        var ghost = new ItemDefinition<string>("ghost");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            ghost);
        var source = sourceManager.CreateInventory();
        source.Add(ghost, 4);
        var snapshot = source.CaptureSnapshot();

        var apple = new ItemDefinition<string>("apple");
        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var target = targetManager.CreateInventory();

        Assert.That(target.TrySalvageSnapshot(snapshot, null, out _, out var strictError), Is.False);
        Assert.That(strictError?.Message, Does.Contain("could not be resolved"));

        var options = new SnapshotSalvageOptions<string>
        {
            UnknownDefinitionHandling = SnapshotUnknownDefinitionHandling.Discard
        };
        var result = target.SalvageSnapshot(snapshot, options);

        Assert.That(result.Losses.Single().Quantity, Is.EqualTo(4));
        Assert.That(target.Items, Is.Empty);
    }

    [Test]
    public void Salvage_PriorityComparerControlsGreedyRetention()
    {
        var apple = new ItemDefinition<string>("apple");
        var diamond = new ItemDefinition<string>("diamond");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple,
            diamond);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 5);
        source.Add(diamond, 2);

        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new MaxTotalItemAmountCapacityPolicy<string>(5),
            apple,
            diamond);
        var target = targetManager.CreateInventory();
        var options = new SnapshotSalvageOptions<string>
        {
            PriorityComparer = Comparer<SnapshotSalvageCandidate<string>>.Create(
                (left, right) =>
                    (left.Definition.Id == "diamond" ? 1 : 0)
                    .CompareTo(right.Definition.Id == "diamond" ? 1 : 0))
        };

        target.SalvageSnapshot(source.CaptureSnapshot(), options);

        Assert.That(target.Count(diamond), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.EqualTo(3));
    }

    [Test]
    public void Salvage_WholeEntryModeDoesNotRetainPartialQuantity()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 5);
        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new MaxTotalItemAmountCapacityPolicy<string>(3),
            apple);
        var target = targetManager.CreateInventory();

        var result = target.SalvageSnapshot(
            source.CaptureSnapshot(),
            new SnapshotSalvageOptions<string>
            {
                QuantityMode = SnapshotSalvageQuantityMode.WholeEntryOnly
            });

        Assert.That(target.Items, Is.Empty);
        Assert.That(result.Losses.Single().Quantity, Is.EqualTo(5));
    }

    [Test]
    public void CustomLayoutCodec_ProvidesExactRoundTripRestoration()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new AutomaticOnlyLayout(2),
            new UnlimitedCapacityPolicy<string>(),
            apple);
        var source = manager.CreateInventory(layout: new AutomaticOnlyLayout(2));
        source.Add(apple);
        var target = manager.CreateInventory(layout: new AutomaticOnlyLayout(2));
        var snapshot = source.CaptureSnapshot();

        var assessment = target.AssessSnapshot(snapshot);

        Assert.That(assessment.CanRestoreExactly, Is.True);
        Assert.That(assessment.CanReconcileWithoutLoss, Is.True);
        target.RestoreSnapshot(snapshot);
        Assert.That(target.TotalItemCount, Is.EqualTo(1));
    }

    [Test]
    public void EmptyExactRestore_IsANoOpWithoutEvent()
    {
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>());
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        int events = 0;
        target.Changed += (_, _) => events++;

        var result = target.RestoreSnapshot(source.CaptureSnapshot());

        Assert.That(result.Outcome, Is.EqualTo(SnapshotApplicationOutcome.Exact));
        Assert.That(events, Is.Zero);
    }

    [Test]
    public void MalformedSnapshot_IsAnOperationFailureEvenForSalvage()
    {
        var manager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>());
        var target = manager.CreateInventory();
        var malformed = new InventorySnapshot { FormatVersion = 99 };

        var assessment = target.AssessSnapshot(malformed);

        Assert.That(assessment.CanRestoreExactly, Is.False);
        Assert.That(assessment.CanReconcileWithoutLoss, Is.False);
        Assert.That(assessment.CanSalvage, Is.False);
        Assert.That(assessment.Issues, Is.Not.Empty);
        Assert.That(target.TrySalvageSnapshot(malformed, null, out _, out _), Is.False);
    }

    [Test]
    public void CurrentRulesRejectLosslessApplicationButAllowExplicitSalvageLoss()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var sourceManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple,
            berry);
        var source = sourceManager.CreateInventory();
        source.Add(apple, 2);

        var targetManager = CreateManager(
            new FixedSizeStackResolver<string>(10),
            new EntryLayout<string>(),
            new UnlimitedCapacityPolicy<string>(),
            apple,
            berry);
        var rules = new RuleContainer<string>();
        rules.Set("only-berry", new OnlyAllowItemsRule<string>(berry));
        var target = targetManager.CreateInventory(rules: rules);

        Assert.That(target.TryReconcileSnapshot(source.CaptureSnapshot(), out _, out var error), Is.False);
        Assert.That(error?.Message, Does.Contain("OnlyAllowItems"));

        var result = target.SalvageSnapshot(source.CaptureSnapshot());
        Assert.That(result.Losses.Single().Quantity, Is.EqualTo(2));
        Assert.That(target.Items, Is.Empty);
    }

    private static InventoryManager<string> CreateManager(
        IStackResolver<string> stackResolver,
        IInventoryLayout<string> layout,
        ICapacityPolicy<string> capacity,
        params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();
        return new InventoryManager<string>(stackResolver, capacity, layout, catalog);
    }

    private sealed class UnitFootprintProvider : IGridFootprintProvider<string>
    {
        public GridFootprint GetFootprint(ItemDefinition<string> definition) => new(1, 1);
    }

    private sealed class FixedFootprintProvider : IGridFootprintProvider<string>
    {
        private readonly GridFootprint _footprint;

        public FixedFootprintProvider(int width, int height)
        {
            _footprint = new GridFootprint(width, height);
        }

        public GridFootprint GetFootprint(ItemDefinition<string> definition) => _footprint;
    }

    private sealed class AutomaticOnlyLayout : SlotLayout<string>, IInventoryLayout<string>
    {
        public AutomaticOnlyLayout(int slotCount)
            : base(slotCount)
        {
        }

        public override IInventoryLayoutSnapshotCodec<string> SnapshotCodec =>
            AutomaticOnlyLayoutCodec.Instance;

        IInventoryLayout<string> IInventoryLayout<string>.Clone()
        {
            var state = (SlotLayoutPersistentData)GetPersistentData();
            var clone = new AutomaticOnlyLayout(state.SlotMap.Count);
            clone.RestorePersistentData(new SlotLayoutPersistentData
            {
                SlotMap = new List<int?>(state.SlotMap)
            });
            return clone;
        }
    }

    private sealed class AutomaticOnlyLayoutCodec : IInventoryLayoutSnapshotCodec<string>
    {
        public static AutomaticOnlyLayoutCodec Instance { get; } = new();
        public string LayoutKind => "tests.layout.automatic-only";
        public int CurrentVersion => 1;

        public bool TryCapture(
            InventoryLayoutSnapshotCaptureContext<string> context,
            out SnapshotValue? data,
            out InventoryFailure? error)
        {
            var state = (SlotLayoutPersistentData)context.Layout.GetPersistentData();
            var references = new List<object?>();
            foreach (var index in state.SlotMap)
            {
                references.Add(index.HasValue
                    ? context.TryGetEntryId(context.Inventory.Items[index.Value], out var entryId)
                        ? entryId
                        : null
                    : null);
            }
            data = SnapshotValue.Object(new[]
            {
                new SnapshotNamedValue
                {
                    Name = "slots",
                    Value = InventorySnapshotCodecs.Encode(references)
                }
            });
            error = null;
            return true;
        }

        public bool TryDecode(
            InventoryLayoutSnapshotDecodeContext<string> context,
            out InventoryLayoutSnapshotCandidate<string>? candidate,
            out InventoryFailure? error)
        {
            candidate = null;
            error = null;
            if (context.Snapshot.Kind != LayoutKind ||
                context.Snapshot.DataVersion != 1 ||
                context.Snapshot.Data.Kind != SnapshotValueKind.Object ||
                context.Snapshot.Data.Properties.Count != 1 ||
                !InventorySnapshotCodecs.TryDecode(
                    context.Snapshot.Data.Properties[0].Value,
                    out List<object?> references,
                    out error))
            {
                error ??= "Invalid automatic-only layout snapshot.";
                return false;
            }
            var mapped = new Dictionary<string, IReadOnlyList<ILayoutContext<string>>>(StringComparer.Ordinal);
            for (int index = 0; index < references.Count; index++)
            {
                if (references[index] is not string entryId)
                    continue;
                if (!context.TryGetEntry(entryId, out _))
                {
                    error = "Unknown entry.";
                    return false;
                }
                mapped.Add(entryId, new[] { SlotLayoutContext<string>.Single(index) });
            }
            if (mapped.Count != context.EntryCount)
            {
                error = "Every entry must be placed.";
                return false;
            }
            candidate = new InventoryLayoutSnapshotCandidate<string>(
                LayoutKind,
                1,
                context.Snapshot.Data,
                mapped);
            error = null;
            return true;
        }

        public bool TryCreateExactLayout(
            InventoryLayoutSnapshotRestoreContext<string> context,
            out IInventoryLayout<string>? layout,
            out InventoryFailure? error)
        {
            layout = null;
            error = null;
            if (context.TargetLayout is not AutomaticOnlyLayout ||
                !InventorySnapshotCodecs.TryDecode(
                    context.Candidate.Data.Properties[0].Value,
                    out List<object?> references,
                    out error))
                return false;

            var slots = new List<int?>();
            foreach (var reference in references)
            {
                if (reference == null)
                    slots.Add(null);
                else if (reference is string entryId &&
                         context.StorageIndices.TryGetValue(entryId, out int storageIndex))
                    slots.Add(storageIndex);
                else
                    return false;
            }
            var restored = new AutomaticOnlyLayout(slots.Count);
            restored.RestorePersistentData(new SlotLayoutPersistentData { SlotMap = slots });
            layout = restored;
            return true;
        }
    }
}
