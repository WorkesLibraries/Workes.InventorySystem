using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Persistence;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventorySnapshotBuilderTests
{
    [Test]
    public void ToBuilder_EditsDetachedCopyWithoutMutatingOriginalSnapshot()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), apple);
        inventory.Metadata.Set("owner", "old");
        inventory.Add(apple, 2);
        var snapshot = inventory.CaptureSnapshot();

        var builder = snapshot.ToBuilder();
        builder
            .SetMetadata("owner", InventorySnapshotCodecs.Encode("new"))
            .SetEntryAmount("e0", 5)
            .SetEntryMetadata("e0", "quality", InventorySnapshotCodecs.Encode("rare"));

        var migrated = builder.Build();
        snapshot.Metadata.Single(value => value.Name == "owner").Value =
            InventorySnapshotCodecs.Encode("mutated-original");
        migrated.Entries.Single().Metadata.Single(value => value.Name == "quality").Value =
            InventorySnapshotCodecs.Encode("mutated-result");

        InventorySnapshotCodecs.TryDecode(snapshot.Metadata.Single().Value, out string originalOwner, out _);
        InventorySnapshotCodecs.TryDecode(builder.Build().Metadata.Single().Value, out string builderOwner, out _);
        InventorySnapshotCodecs.TryDecode(builder.Build().Entries.Single().Metadata.Single().Value, out string quality, out _);

        Assert.That(originalOwner, Is.EqualTo("mutated-original"));
        Assert.That(builderOwner, Is.EqualTo("new"));
        Assert.That(quality, Is.EqualTo("rare"));
        Assert.That(migrated.Entries.Single().Amount, Is.EqualTo(5));
    }

    [Test]
    public void ReorderEntries_ChangesStorageOrderButPreservesSlotPlacementByEntryId()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var source = CreateInventory(new SlotLayout<string>(4), apple, berry);
        var add = InventoryTransaction<string>.From(source);
        Assert.That(add.TryAdd(apple, 1, SlotLayoutContext<string>.Single(3), null, out var failure), Is.True, failure?.Message);
        Assert.That(add.TryAdd(berry, 1, SlotLayoutContext<string>.Single(1), null, out failure), Is.True, failure?.Message);
        source.CommitTransaction(add);

        var migrated = source.CaptureSnapshot()
            .ToBuilder()
            .ReorderEntries(new[] { "e1", "e0" })
            .Build();

        var target = CreateInventory(new SlotLayout<string>(4), apple, berry);
        target.RestoreSnapshot(migrated);

        Assert.That(target.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "berry", "apple" }));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 0).Single()).SlotIndex, Is.EqualTo(1));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 1).Single()).SlotIndex, Is.EqualTo(3));
    }

    [Test]
    public void RemoveEntry_CleansBuiltInLayoutReferencesAndCanStillRestoreExactly()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var source = CreateInventory(new SlotLayout<string>(3), apple, berry);
        var add = InventoryTransaction<string>.From(source);
        Assert.That(add.TryAdd(apple, 1, SlotLayoutContext<string>.Single(0), null, out var failure), Is.True, failure?.Message);
        Assert.That(add.TryAdd(berry, 1, SlotLayoutContext<string>.Single(2), null, out failure), Is.True, failure?.Message);
        source.CommitTransaction(add);

        var migrated = source.CaptureSnapshot()
            .ToBuilder()
            .RemoveEntry("e0")
            .Build();

        Assert.That(migrated.Entries.Select(entry => entry.EntryId), Is.EqualTo(new[] { "e1" }));

        var target = CreateInventory(new SlotLayout<string>(3), apple, berry);
        target.RestoreSnapshot(migrated);

        Assert.That(target.Items.Single().Definition.Id, Is.EqualTo("berry"));
        Assert.That(((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, 0).Single()).SlotIndex, Is.EqualTo(2));
    }

    [Test]
    public void RemoveEntry_RejectsCustomLayoutUntilLayoutIsResetOrReplaced()
    {
        var apple = new ItemDefinition<string>("apple");
        var source = CreateInventory(new EntryLayout<string>(), apple);
        source.Add(apple, 1);
        var builder = source.CaptureSnapshot().ToBuilder();
        builder.ReplaceLayout(new InventoryLayoutSnapshot
        {
            Kind = "tests.custom-layout",
            DataVersion = 1,
            Data = SnapshotValue.Object(new[]
            {
                new SnapshotNamedValue
                {
                    Name = "opaque",
                    Value = InventorySnapshotCodecs.Encode("e0")
                }
            })
        });

        Assert.That(builder.TryRemoveEntry("e0", out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Layout));

        var migrated = builder
            .ResetLayoutToEntryOrder()
            .RemoveEntry("e0")
            .Build();

        Assert.That(migrated.Entries, Is.Empty);
        Assert.That(InventorySnapshotValidator.TryValidate(migrated, out var validationFailure), Is.True, validationFailure?.Message);
    }

    [Test]
    public void ResetLayoutToEntryOrder_DiscardsSavedPlacementForLosslessReconciliation()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var source = CreateInventory(new SlotLayout<string>(4), apple, berry);
        var add = InventoryTransaction<string>.From(source);
        Assert.That(add.TryAdd(apple, 1, SlotLayoutContext<string>.Single(3), null, out var failure), Is.True, failure?.Message);
        Assert.That(add.TryAdd(berry, 1, SlotLayoutContext<string>.Single(2), null, out failure), Is.True, failure?.Message);
        source.CommitTransaction(add);

        var migrated = source.CaptureSnapshot()
            .ToBuilder()
            .ResetLayoutToEntryOrder()
            .Build();

        var target = CreateInventory(new SlotLayout<string>(2), apple, berry);
        var result = target.ReconcileSnapshot(migrated);

        Assert.That(result.Outcome, Is.EqualTo(SnapshotApplicationOutcome.Reconciled));
        Assert.That(target.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "apple", "berry" }));
        Assert.That(
            Enumerable.Range(0, target.Items.Count)
                .Select(index => ((SlotLayoutContext<string>)target.Layout.GetContextsForStorageIndex(target, index).Single()).SlotIndex),
            Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void TryBuild_ReturnsStructuredFailureForMalformedBuilderOutput()
    {
        var apple = new ItemDefinition<string>("apple");
        var source = CreateInventory(new EntryLayout<string>(), apple);
        source.Add(apple, 1);
        var builder = source.CaptureSnapshot().ToBuilder();
        builder.FormatVersion = 999;

        Assert.That(builder.TryBuild(out var snapshot, out var failure), Is.False);
        Assert.That(snapshot, Is.Null);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Snapshot));
        Assert.That(failure?.Code, Is.EqualTo(InventoryFailureCodes.SnapshotRejected));
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            catalog);
        return manager.CreateInventory(layout: layout);
    }
}
