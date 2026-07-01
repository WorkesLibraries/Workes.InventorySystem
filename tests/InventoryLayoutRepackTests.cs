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
public class InventoryLayoutRepackTests
{
    [Test]
    public void TryRepackLayout_CompactsSlotsByCurrentLayoutOrder()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new SlotLayout<string>(5), sword, apple, potion);

        inventory.Add(sword, context: SlotLayoutContext<string>.Single(4));
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(1));
        inventory.Add(potion, context: SlotLayoutContext<string>.Single(3));
        var originalItems = inventory.Items.ToArray();

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(potion));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2))!.Definition, Is.SameAs(sword));
        Assert.That(inventory.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "sword", "apple", "potion" }));
        Assert.That(inventory.Items, Is.EqualTo(originalItems));
    }

    [Test]
    public void TryRepackLayout_FiresCompleteMovedEventWithoutFullRefreshOrConfigurationChange()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new SlotLayout<string>(5), sword, apple, potion);
        inventory.Add(sword, context: SlotLayoutContext<string>.Single(4));
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(1));
        inventory.Add(potion, context: SlotLayoutContext<string>.Single(3));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.RequiresFullRefresh, Is.False);
        Assert.That(captured.ConfigurationChanged, Is.Empty);
        Assert.That(captured.Added, Is.Empty);
        Assert.That(captured.Removed, Is.Empty);
        Assert.That(captured.Modified, Is.Empty);
        Assert.That(captured.MetadataChanged, Is.Empty);
        Assert.That(captured.Moved, Is.Not.Empty);
        Assert.That(captured.Moved.All(move => move.Cause == ItemMovementCause.Repack), Is.True);
        Assert.That(captured.Moved.All(move => move.IsAutomatic), Is.True);

        var movedContexts = captured.Moved
            .SelectMany(move => move.FromLayoutContexts.Concat(move.ToLayoutContexts))
            .ToList();
        foreach (var context in movedContexts)
            Assert.That(captured.AffectedLayoutContexts, Does.Contain(context));
    }

    [Test]
    public void TryRepackLayout_WhenPlacementDoesNotChange_FiresNoEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new SlotLayout<string>(2), coin);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(0));
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void RepackLayout_WhenRejected_ThrowsInvalidOperationException()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new UnsupportedLayout(), coin);
        inventory.Add(coin);

        var exception = Assert.Throws<InvalidOperationException>(() => inventory.RepackLayout());

        Assert.That(exception!.Message, Does.Contain("does not support inventory-owned repack"));
    }

    [Test]
    public void TryRepackLayout_RejectsUnsupportedCustomLayout()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new UnsupportedLayout(), coin);
        inventory.Add(coin);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("Current layout type 'UnsupportedLayout' does not support inventory-owned repack."));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TryRepackLayout_RejectsEntryLayoutBecauseRepackIsAlwaysANoOp()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new EntryLayout<string>(), coin);
        inventory.Add(coin);
        var originalLayout = inventory.Layout;
        var originalItem = inventory.Items.Single();
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("EntryLayout").And.Contain("does not support inventory-owned repack"));
        Assert.That(inventory.Layout, Is.SameAs(originalLayout));
        Assert.That(inventory.Items.Single(), Is.SameAs(originalItem));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TryRepackLayout_RejectsEquipmentLayoutAndPreservesNamedSlot()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(
            new EquipmentLayout<string>(
                new EquipmentSlot<string>("first"),
                new EquipmentSlot<string>("second")),
            coin);
        inventory.Add(coin, context: EquipmentLayoutContext<string>.Single("second"));
        var originalLayout = inventory.Layout;
        var originalItem = inventory.Items.Single();
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("EquipmentLayout").And.Contain("does not support inventory-owned repack"));
        Assert.That(inventory.Layout, Is.SameAs(originalLayout));
        Assert.That(inventory.Items.Single(), Is.SameAs(originalItem));
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("first")), Is.Null);
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("second")), Is.SameAs(originalItem));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TryRepackLayout_GridLayoutCompactsUsingPlacementOrder()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new GridLayout<string>(3, 2, GridPlacementOrder.ColumnMajor), sword, apple, potion);
        inventory.Add(sword, context: GridLayoutContext<string>.Single(2, 1));
        inventory.Add(apple, context: GridLayoutContext<string>.Single(0, 1));
        inventory.Add(potion, context: GridLayoutContext<string>.Single(1, 1));
        var originalItems = inventory.Items.ToArray();

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 0))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 1))!.Definition, Is.SameAs(potion));
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(1, 0))!.Definition, Is.SameAs(sword));
        Assert.That(inventory.Items, Is.EqualTo(originalItems));
    }

    [Test]
    public void TryRepackLayout_CustomCapabilityCompactsAndReportsMovement()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new CustomRepackableLayout(4), sword, apple);
        inventory.Add(sword, context: SlotLayoutContext<string>.Single(3));
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout, Is.TypeOf<CustomRepackableLayout>());
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(sword));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Moved, Has.Count.EqualTo(2));
        Assert.That(captured.RequiresFullRefresh, Is.False);
        Assert.That(captured.ConfigurationChanged, Is.Empty);
    }

    [Test]
    public void StackResolverRebuildRepack_UsesCustomCapability()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new CustomRepackableLayout(4, requestFullRefresh: true), coin);
        inventory.Add(coin, amount: 10);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            6,
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.RepackLayout,
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout, Is.TypeOf<CustomRepackableLayout>());
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 6, 4 }));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.ConfigurationChanged, Has.Count.EqualTo(1));
        Assert.That(captured.RequiresFullRefresh, Is.True);
    }

    [Test]
    public void TrySetLayoutParameter_CustomLayoutPreservesPlacementWithoutRepack()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new CustomRepackableLayout(3), coin);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));

        var accepted = inventory.TrySetLayoutParameter("capacity", 4, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((CustomRepackableLayout)inventory.Layout).Capacity, Is.EqualTo(4));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2)), Is.SameAs(inventory.Items[0]));
    }

    [Test]
    public void TrySetLayoutParameter_CustomParameterizedRepackReflowsIntoNewShape()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new CustomRepackableLayout(4), sword, apple);
        inventory.Add(sword, context: SlotLayoutContext<string>.Single(3));
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(2));
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetLayoutParameter(
            "capacity",
            2,
            InventoryParameterMutationActions.RepackLayout,
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(events, Is.EqualTo(1));
        Assert.That(((CustomRepackableLayout)inventory.Layout).Capacity, Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(sword));
    }

    [Test]
    public void TryRepackLayout_WhenCustomCapabilityRejects_IsAtomicAndSilent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new CustomRepackableLayout(3, rejectRepack: true), coin);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));
        var originalLayout = inventory.Layout;
        var originalItem = inventory.Items.Single();
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryRepackLayout(out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.EqualTo("Custom repack rejected."));
        Assert.That(inventory.Layout, Is.SameAs(originalLayout));
        Assert.That(inventory.Items.Single(), Is.SameAs(originalItem));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2)), Is.SameAs(originalItem));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TrySetLayoutParameter_WhenCustomRepackCannotFit_IsAtomicAndSilent()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new CustomRepackableLayout(3), sword, apple);
        inventory.Add(sword, context: SlotLayoutContext<string>.Single(1));
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(2));
        var originalLayout = inventory.Layout;
        var originalItems = inventory.Items.ToArray();
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetLayoutParameter(
            "capacity",
            1,
            InventoryParameterMutationActions.RepackLayout,
            out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
        Assert.That(inventory.Layout, Is.SameAs(originalLayout));
        Assert.That(inventory.Items, Is.EqualTo(originalItems));
        Assert.That(events, Is.EqualTo(0));
    }

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

    private sealed class UnsupportedLayout : IInventoryLayout<string>
    {
        private readonly List<int> _indices = new();

        public int GetPositionCount(Inventory<string> inventory) => _indices.Count;

        public IReadOnlyList<ILayoutContext<string>> GetAddressableContexts(Inventory<string> inventory)
        {
            var contexts = new List<ILayoutContext<string>>(_indices.Count);
            for (int i = 0; i < _indices.Count; i++)
                contexts.Add(EntryLayoutContext<string>.Single(i));
            return contexts;
        }

        public ItemInstance<string>? GetItemAt(Inventory<string> inventory, ILayoutContext<string> context)
        {
            if (context is not EntryLayoutContext<string> entry || entry.TargetIndex < 0 || entry.TargetIndex >= _indices.Count)
                return null;

            return inventory.Items[_indices[entry.TargetIndex]];
        }

        public IReadOnlyList<ILayoutContext<string>> GetContextsForStorageIndex(Inventory<string> inventory, int storageIndex)
        {
            int position = _indices.IndexOf(storageIndex);
            return position >= 0
                ? new[] { EntryLayoutContext<string>.Single(position) }
                : Array.Empty<ILayoutContext<string>>();
        }

        public bool TryGetContextForStorageIndex(Inventory<string> inventory, int storageIndex, out ILayoutContext<string>? context)
        {
            context = GetContextsForStorageIndex(inventory, storageIndex).FirstOrDefault();
            return context != null;
        }

        public IEnumerable<int> GetMergeCandidates(Inventory<string> inventory, ItemInstance<string> prototype, ILayoutContext<string>? context)
            => _indices;

        public bool CanSatisfyPlacement(Inventory<string> inventory, InventoryTransaction<string> transaction, out string? error)
        {
            error = null;
            return true;
        }

        public bool TryApplyPlacementContext(
            Inventory<string> inventory,
            InventoryTransaction<string> transaction,
            ILayoutContext<string>? context,
            out InventoryTransaction<string>? mappedTransaction,
            out string? error)
        {
            mappedTransaction = transaction;
            error = null;
            return true;
        }

        public bool CanAcceptNewItem(Inventory<string> inventory, ItemInstance<string> instance, ILayoutContext<string>? context, out string? error)
        {
            error = null;
            return true;
        }

        public bool TryMove(Inventory<string> inventory, ILayoutContext<string> contextFrom, ILayoutContext<string> contextTo, out string? error)
        {
            error = "Unsupported.";
            return false;
        }

        public bool TrySwap(Inventory<string> inventory, ILayoutContext<string> contextFrom, ILayoutContext<string> contextTo, out string? error)
        {
            error = "Unsupported.";
            return false;
        }

        public bool TrySort(Inventory<string> inventory, IInventorySortContext<string> sortContext, out string? error)
        {
            error = "Unsupported.";
            return false;
        }

        public void OnItemAdded(Inventory<string> inventory, int index, ILayoutContext<string>? context)
            => _indices.Add(index);

        public void OnItemRemoved(Inventory<string> inventory, int index)
        {
            _indices.Remove(index);
            for (int i = 0; i < _indices.Count; i++)
            {
                if (_indices[i] > index)
                    _indices[i]--;
            }
        }

        public void OnInventoryCleared(Inventory<string> inventory)
            => _indices.Clear();

        public ILayoutPersistentData GetPersistentData() => new EntryLayoutPersistentData();

        public void RestorePersistentData(ILayoutPersistentData? persistentData)
        {
        }

        public IInventoryLayout<string> Clone() => new UnsupportedLayout();
    }

    private sealed class CustomRepackableLayout :
        IParameterizedRepackableInventoryLayout<string>,
        IInventoryLayoutReconciler<string>
    {
        private readonly SlotLayout<string> _inner;
        private readonly bool _rejectRepack;
        private readonly bool _requestFullRefresh;
        private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
            new[]
            {
                new InventoryParameterDefinition("capacity", typeof(int), "Number of custom layout positions.")
            };

        public CustomRepackableLayout(
            int capacity,
            bool rejectRepack = false,
            bool requestFullRefresh = false)
            : this(new SlotLayout<string>(capacity), rejectRepack, requestFullRefresh)
        {
        }

        private CustomRepackableLayout(
            SlotLayout<string> inner,
            bool rejectRepack,
            bool requestFullRefresh)
        {
            _inner = inner;
            _rejectRepack = rejectRepack;
            _requestFullRefresh = requestFullRefresh;
        }

        public int Capacity => _inner.GetPersistentData() is SlotLayoutPersistentData data
            ? data.SlotMap.Count
            : 0;

        public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

        public bool TryCreateEmptyRepackLayout(
            out IInventoryLayout<string>? layout,
            out string? error)
        {
            if (_rejectRepack)
            {
                layout = null;
                error = "Custom repack rejected.";
                return false;
            }

            layout = new CustomRepackableLayout(
                Capacity,
                requestFullRefresh: _requestFullRefresh);
            error = null;
            return true;
        }

        public bool TryCreateEmptyRepackLayoutWithParameter(
            string parameterId,
            object? value,
            out IInventoryLayout<string>? layout,
            out string? error)
        {
            layout = null;
            if (_rejectRepack)
            {
                error = "Custom repack rejected.";
                return false;
            }

            if (!TryResolveCapacity(parameterId, value, out int capacity, out error))
                return false;

            layout = new CustomRepackableLayout(
                capacity,
                requestFullRefresh: _requestFullRefresh);
            error = null;
            return true;
        }

        public bool TryCreateWithParameter(
            Inventory<string> inventory,
            string parameterId,
            object? value,
            out IInventoryLayout<string>? layout,
            out string? error)
        {
            layout = null;
            if (!TryResolveCapacity(parameterId, value, out int capacity, out error))
                return false;

            if (!_inner.TryCreateWithParameter(inventory, "slotCount", capacity, out var innerLayout, out error) ||
                innerLayout is not SlotLayout<string> slotLayout)
            {
                return false;
            }

            layout = new CustomRepackableLayout(
                slotLayout,
                _rejectRepack,
                _requestFullRefresh);
            error = null;
            return true;
        }

        private static bool TryResolveCapacity(
            string parameterId,
            object? value,
            out int capacity,
            out string? error)
        {
            capacity = 0;
            if (parameterId != "capacity")
            {
                error = $"Parameter '{parameterId}' is not supported by CustomRepackableLayout.";
                return false;
            }

            if (value is not int resolvedCapacity)
            {
                error = "Parameter 'capacity' expects value type 'Int32'.";
                return false;
            }

            if (resolvedCapacity <= 0)
            {
                error = "Capacity must be greater than zero.";
                return false;
            }

            capacity = resolvedCapacity;
            error = null;
            return true;
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

        public InventoryLayoutReconciliationResult<string> ReconcileAfterInventoryMutation(
            Inventory<string> inventory)
            => new(requiresFullRefresh: _requestFullRefresh);

        public ILayoutPersistentData GetPersistentData() => _inner.GetPersistentData();

        public void RestorePersistentData(ILayoutPersistentData? persistentData)
            => _inner.RestorePersistentData(persistentData);

        public IInventoryLayout<string> Clone()
            => new CustomRepackableLayout(
                (SlotLayout<string>)_inner.Clone(),
                _rejectRepack,
                _requestFullRefresh);
    }
}
