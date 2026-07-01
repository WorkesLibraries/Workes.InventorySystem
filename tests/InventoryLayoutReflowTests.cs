using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryLayoutReflowTests
{
    [Test]
    public void IndexedEntryAdd_ReportsShiftedSurvivorAndCompleteContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var inventory = CreateInventory(apple, berry, carrot);
        inventory.Add(apple);
        inventory.Add(berry);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Add(carrot, context: EntryLayoutContext<string>.Single(1));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Added.Single().Instance.Definition, Is.SameAs(carrot));
        Assert.That(((EntryLayoutContext<string>)captured.Added.Single().LayoutContext!).TargetIndex, Is.EqualTo(1));
        AssertMovement(captured, inventory.Items.Single(item => ReferenceEquals(item.Definition, berry)), 1, 2);
        AssertAffectedEntryIndices(captured, 1, 2);
    }

    [Test]
    public void EntryRemoval_ReportsEveryShiftedSurvivor()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var inventory = CreateInventory(apple, berry, carrot);
        inventory.Add(apple);
        inventory.Add(berry);
        inventory.Add(carrot);
        var appleInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, apple));
        var berryInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, berry));
        var carrotInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, carrot));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Remove(appleInstance);

        Assert.That(captured, Is.Not.Null);
        Assert.That(((EntryLayoutContext<string>)captured!.Removed.Single().LayoutContext!).TargetIndex, Is.EqualTo(0));
        AssertMovement(captured, berryInstance, 1, 0);
        AssertMovement(captured, carrotInstance, 2, 1);
        AssertAffectedEntryIndices(captured, 0, 1, 2);
    }

    [Test]
    public void EntryMove_ReportsExplicitItemAndEveryDisplacedNeighbour()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var date = new ItemDefinition<string>("date");
        var inventory = CreateInventory(apple, berry, carrot, date);
        inventory.Add(apple);
        inventory.Add(berry);
        inventory.Add(carrot);
        inventory.Add(date);
        var appleInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, apple));
        var berryInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, berry));
        var carrotInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, carrot));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Move(
            EntryLayoutContext<string>.Single(0),
            EntryLayoutContext<string>.Single(3));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Moved, Has.Count.EqualTo(3));
        AssertMovement(captured, appleInstance, 0, 2, ItemMovementCause.ExplicitMove);
        AssertMovement(captured, berryInstance, 1, 0);
        AssertMovement(captured, carrotInstance, 2, 1);
        AssertAffectedEntryIndices(captured, 0, 1, 2);
    }

    [Test]
    public void MultiOperationTransaction_ReportsOnlyFinalReflow()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var date = new ItemDefinition<string>("date");
        var inventory = CreateInventory(apple, berry, carrot, date);
        inventory.Add(apple);
        inventory.Add(berry);
        inventory.Add(carrot);
        var berryInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, berry));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryRemove(berryInstance, out var removeError), Is.True, removeError);
        Assert.That(
            builder.TryAdd(date, out var addError, context: EntryLayoutContext<string>.Single(1)),
            Is.True,
            addError);

        inventory.CommitTransaction(builder);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Removed.Single().Instance, Is.SameAs(berryInstance));
        Assert.That(captured.Added.Single().Instance.Definition, Is.SameAs(date));
        Assert.That(captured.Moved, Is.Empty);
        AssertAffectedEntryIndices(captured, 1);
    }

    [Test]
    public void MergeRemoval_ReportsLaterEntryShift()
    {
        var coin = new ItemDefinition<string>("coin");
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventoryWithMaxStack(10, coin, gem);
        inventory.Add(coin, amount: 15);
        inventory.Add(gem);
        var firstCoin = inventory.Items[0];
        var secondCoin = inventory.Items[1];
        var gemInstance = inventory.Items[2];
        inventory.Remove(firstCoin, amount: 5);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.MergeMove(
            EntryLayoutContext<string>.Single(1),
            EntryLayoutContext<string>.Single(0));

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Removed.Single().Instance, Is.SameAs(secondCoin));
        AssertMovement(captured, gemInstance, 2, 1);
        AssertAffectedEntryIndices(captured, 0, 1, 2);
    }

    [Test]
    public void StackCompressionWithoutRepack_ReportsEntryReflow()
    {
        var coin = new ItemDefinition<string>("coin");
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(coin, gem);
        inventory.Add(coin, amount: 10);
        inventory.Add(gem);
        var removedCoin = inventory.Items[1];
        var gemInstance = inventory.Items[2];
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            10,
            InventoryParameterMutationActions.CompressCompatibleStacks,
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Removed.Single().Instance, Is.SameAs(removedCoin));
        AssertMovement(captured, gemInstance, 2, 1);
        Assert.That(captured.RequiresFullRefresh, Is.False);
    }

    [Test]
    public void SourceTransfer_ReportsEntryReflowInItsSingleCommittedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var catalog = new ItemCatalog<string>();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(5),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Registry.Register(carrot);
        catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.Add(apple);
        source.Add(berry);
        source.Add(carrot);
        var appleInstance = source.Items.Single(item => ReferenceEquals(item.Definition, apple));
        var berryInstance = source.Items.Single(item => ReferenceEquals(item.Definition, berry));
        var carrotInstance = source.Items.Single(item => ReferenceEquals(item.Definition, carrot));
        InventoryChangedEventArgs<string>? sourceEvent = null;
        int sourceEvents = 0;
        source.Changed += (_, args) =>
        {
            sourceEvent = args;
            sourceEvents++;
        };

        source.TransferTo(target, appleInstance, appleInstance.Amount);

        Assert.That(sourceEvents, Is.EqualTo(1));
        Assert.That(sourceEvent, Is.Not.Null);
        AssertMovement(sourceEvent!, berryInstance, 1, 0);
        AssertMovement(sourceEvent!, carrotInstance, 2, 1);
    }

    [Test]
    public void AmountDrivenCustomLayout_ReconcilesAndReportsEveryMovement()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(
            new StateOrderedEntryLayout(
                Comparer<ItemInstance<string>>.Create((left, right) => left.Amount.CompareTo(right.Amount))),
            apple,
            berry);
        inventory.Add(apple, amount: 2);
        inventory.Add(berry, amount: 3);
        var appleInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, apple));
        var berryInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, berry));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Add(apple, amount: 2);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Modified.Single().Instance, Is.SameAs(appleInstance));
        AssertMovement(captured, appleInstance, 0, 1);
        AssertMovement(captured, berryInstance, 1, 0);
    }

    [Test]
    public void MetadataDrivenCustomLayout_ReconcilesAndReportsEveryMovement()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(
            new StateOrderedEntryLayout(Comparer<ItemInstance<string>>.Create(ComparePriority)),
            apple,
            berry);
        inventory.Add(apple);
        inventory.Add(berry);
        var appleInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, apple));
        var berryInstance = inventory.Items.Single(item => ReferenceEquals(item.Definition, berry));
        appleInstance.Metadata.Set("priority", 1);
        berryInstance.Metadata.Set("priority", 2);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        appleInstance.Metadata.Set("priority", 3);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.MetadataChanged.Single().Instance, Is.SameAs(appleInstance));
        AssertMovement(captured, appleInstance, 0, 1);
        AssertMovement(captured, berryInstance, 1, 0);
    }

    [Test]
    public void CustomReconciliationResult_AddsContextsAndCanRequestFullRefresh()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new StateOrderedEntryLayout(
                Comparer<ItemInstance<string>>.Create((left, right) => left.Amount.CompareTo(right.Amount)),
                requestFullRefresh: true,
                additionalAffectedIndex: 99),
            apple);
        inventory.Add(apple);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Add(apple);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.RequiresFullRefresh, Is.True);
        Assert.That(
            captured.AffectedLayoutContexts
                .Cast<EntryLayoutContext<string>>()
                .Any(context => context.TargetIndex == 99),
            Is.True);
    }

    [Test]
    public void ReplaceContents_WithNoSurvivingInstances_DoesNotInventMovement()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var inventory = CreateInventory(apple, berry, carrot);
        inventory.Add(apple);
        inventory.Add(berry);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.ReplaceContents(new[]
        {
            (carrot, 1, (ILayoutContext<string>?)EntryLayoutContext<string>.Single(0))
        });

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Cleared, Is.True);
        Assert.That(captured.Removed, Has.Count.EqualTo(2));
        Assert.That(captured.Added, Has.Count.EqualTo(1));
        Assert.That(captured.Moved, Is.Empty);
    }

    [Test]
    public void EquivalentMetadataMutation_RemainsANoOpWithoutEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(apple);
        inventory.Add(apple);
        var item = inventory.Items.Single();
        item.Metadata.Set("quality", "fresh");
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = item.Metadata.TrySet("quality", "fresh", out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(events, Is.EqualTo(0));
    }

    private static int ComparePriority(ItemInstance<string> left, ItemInstance<string> right)
    {
        int leftPriority = left.Metadata.TryGet<int>("priority", out var leftValue) ? leftValue : 0;
        int rightPriority = right.Metadata.TryGet<int>("priority", out var rightValue) ? rightValue : 0;
        return leftPriority.CompareTo(rightPriority);
    }

    private static Inventory<string> CreateInventory(params ItemDefinition<string>[] definitions)
        => CreateInventoryWithMaxStack(5, definitions);

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            catalog);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        catalog.Freeze();
        return manager.CreateInventory();
    }

    private static Inventory<string> CreateInventoryWithMaxStack(
        int maxStack,
        params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        catalog.Freeze();
        return manager.CreateInventory();
    }

    private static void AssertMovement(
        InventoryChangedEventArgs<string> args,
        ItemInstance<string> instance,
        int from,
        int to,
        ItemMovementCause expectedCause = ItemMovementCause.LayoutReflow)
    {
        var movement = args.Moved.Single(move => ReferenceEquals(move.Instance, instance));
        Assert.That(((EntryLayoutContext<string>)movement.FromPosition!).TargetIndex, Is.EqualTo(from));
        Assert.That(((EntryLayoutContext<string>)movement.ToPosition!).TargetIndex, Is.EqualTo(to));
        Assert.That(movement.Cause, Is.EqualTo(expectedCause));
        Assert.That(movement.IsAutomatic, Is.EqualTo(expectedCause != ItemMovementCause.ExplicitMove));
    }

    private static void AssertAffectedEntryIndices(
        InventoryChangedEventArgs<string> args,
        params int[] expected)
    {
        var indices = args.AffectedLayoutContexts
            .Cast<EntryLayoutContext<string>>()
            .Select(context => context.TargetIndex)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        Assert.That(indices, Is.EqualTo(expected.OrderBy(index => index).ToArray()));
    }

    private sealed class StateOrderedEntryLayout : IInventoryLayoutReconciler<string>
    {
        private readonly EntryLayout<string> _inner;
        private readonly IComparer<ItemInstance<string>> _comparer;
        private readonly bool _requestFullRefresh;
        private readonly int? _additionalAffectedIndex;

        public StateOrderedEntryLayout(
            IComparer<ItemInstance<string>> comparer,
            bool requestFullRefresh = false,
            int? additionalAffectedIndex = null)
            : this(new EntryLayout<string>(), comparer, requestFullRefresh, additionalAffectedIndex)
        {
        }

        private StateOrderedEntryLayout(
            EntryLayout<string> inner,
            IComparer<ItemInstance<string>> comparer,
            bool requestFullRefresh,
            int? additionalAffectedIndex)
        {
            _inner = inner;
            _comparer = comparer;
            _requestFullRefresh = requestFullRefresh;
            _additionalAffectedIndex = additionalAffectedIndex;
        }

        public InventoryLayoutReconciliationResult<string> ReconcileAfterInventoryMutation(Inventory<string> inventory)
        {
            if (!_inner.TrySort(inventory, new ItemSortContext<string>(_comparer), out var error))
                throw new InvalidOperationException(error);

            var contexts = _additionalAffectedIndex.HasValue
                ? new[] { EntryLayoutContext<string>.Single(_additionalAffectedIndex.Value) }
                : Array.Empty<EntryLayoutContext<string>>();
            return new InventoryLayoutReconciliationResult<string>(contexts, _requestFullRefresh);
        }

        public int GetPositionCount(Inventory<string> inventory) => _inner.GetPositionCount(inventory);

        public IReadOnlyList<ILayoutContext<string>> GetAddressableContexts(Inventory<string> inventory)
            => _inner.GetAddressableContexts(inventory);

        public ItemInstance<string>? GetItemAt(Inventory<string> inventory, ILayoutContext<string> context)
            => _inner.GetItemAt(inventory, context);

        public IReadOnlyList<ILayoutContext<string>> GetContextsForStorageIndex(Inventory<string> inventory, int storageIndex)
            => _inner.GetContextsForStorageIndex(inventory, storageIndex);

        public bool TryGetContextForStorageIndex(
            Inventory<string> inventory,
            int storageIndex,
            out ILayoutContext<string>? context)
            => _inner.TryGetContextForStorageIndex(inventory, storageIndex, out context);

        public IEnumerable<int> GetMergeCandidates(
            Inventory<string> inventory,
            ItemInstance<string> prototype,
            ILayoutContext<string>? context)
            => _inner.GetMergeCandidates(inventory, prototype, context);

        public bool CanSatisfyPlacement(
            Inventory<string> inventory,
            InventoryTransaction<string> transaction,
            out string? error)
            => _inner.CanSatisfyPlacement(inventory, transaction, out error);

        public bool TryApplyPlacementContext(
            Inventory<string> inventory,
            InventoryTransaction<string> transaction,
            ILayoutContext<string>? context,
            out InventoryTransaction<string>? mappedTransaction,
            out string? error)
            => _inner.TryApplyPlacementContext(inventory, transaction, context, out mappedTransaction, out error);

        public bool CanAcceptNewItem(
            Inventory<string> inventory,
            ItemInstance<string> instance,
            ILayoutContext<string>? context,
            out string? error)
            => _inner.CanAcceptNewItem(inventory, instance, context, out error);

        public bool TryMove(
            Inventory<string> inventory,
            ILayoutContext<string> contextFrom,
            ILayoutContext<string> contextTo,
            out string? error)
            => _inner.TryMove(inventory, contextFrom, contextTo, out error);

        public bool TrySwap(
            Inventory<string> inventory,
            ILayoutContext<string> contextFrom,
            ILayoutContext<string> contextTo,
            out string? error)
            => _inner.TrySwap(inventory, contextFrom, contextTo, out error);

        public bool TrySort(
            Inventory<string> inventory,
            IInventorySortContext<string> sortContext,
            out string? error)
            => _inner.TrySort(inventory, sortContext, out error);

        public void OnItemAdded(Inventory<string> inventory, int index, ILayoutContext<string>? context)
            => _inner.OnItemAdded(inventory, index, context);

        public void OnItemRemoved(Inventory<string> inventory, int index)
            => _inner.OnItemRemoved(inventory, index);

        public void OnInventoryCleared(Inventory<string> inventory)
            => _inner.OnInventoryCleared(inventory);

        public ILayoutPersistentData GetPersistentData() => _inner.GetPersistentData();

        public void RestorePersistentData(ILayoutPersistentData? persistentData)
            => _inner.RestorePersistentData(persistentData);

        public IInventoryLayout<string> Clone()
            => new StateOrderedEntryLayout(
                (EntryLayout<string>)_inner.Clone(),
                _comparer,
                _requestFullRefresh,
                _additionalAffectedIndex);
    }
}
