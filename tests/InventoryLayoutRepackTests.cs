using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
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
    public void TryRepackLayout_FiresFullRefreshMovedEventWithoutConfigurationChange()
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
        Assert.That(captured!.RequiresFullRefresh, Is.True);
        Assert.That(captured.ConfigurationChanged, Is.Empty);
        Assert.That(captured.Added, Is.Empty);
        Assert.That(captured.Removed, Is.Empty);
        Assert.That(captured.Modified, Is.Empty);
        Assert.That(captured.MetadataChanged, Is.Empty);
        Assert.That(captured.Moved, Is.Not.Empty);
        Assert.That(captured.Moved.All(move => !move.IsSortResult), Is.True);

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
}
