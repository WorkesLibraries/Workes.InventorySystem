using System.Linq;
using System;
using NUnit.Framework;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Capacity;

#pragma warning disable CS0618 // Legacy persistence compatibility coverage.

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryCoreTests
{
    private static InventoryManager<string> CreateSlotInventoryManager(int slotCount = 4, int maxStack = 10)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            new SlotLayout<string>(slotCount),
            new ItemCatalog<string>()
        );
    }

    [Test]
    public void CreateInventory_Throws_BeforeRegistryIsFrozen()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        Assert.Throws<InvalidOperationException>(() => manager.CreateInventory());
    }

    [Test]
    public void CreateInventory_Succeeds_AfterRegistryIsFrozen()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        manager.Registry.Register(new ItemDefinition<string>("apple"));
        manager.Catalog.Freeze();

        Assert.DoesNotThrow(() => manager.CreateInventory());
    }

    [Test]
    public void PublicNullArgumentExceptions_ReportParameterNames()
    {
        var catalog = new ItemCatalog<string>();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(
            Assert.Throws<ArgumentNullException>(() => new Inventory<string>(
                null!,
                new FixedSizeStackResolver<string>(10),
                new UnlimitedCapacityPolicy<string>(),
                new EntryLayout<string>(),
                new RuleContainer<string>()))!.ParamName,
            Is.EqualTo("manager"));
        Assert.That(
            Assert.Throws<ArgumentNullException>(() => inventory.Deserialize(null!))!.ParamName,
            Is.EqualTo("data"));
        Assert.That(
            Assert.Throws<ArgumentNullException>(() => catalog.Registry.Register(null!))!.ParamName,
            Is.EqualTo("definition"));
        Assert.That(
            Assert.Throws<ArgumentNullException>(() => catalog.Registry.RegisterMigration(null!, apple))!.ParamName,
            Is.EqualTo("oldId"));
        Assert.That(
            Assert.Throws<ArgumentNullException>(() => catalog.Registry.RegisterMigration("old-apple", null!))!.ParamName,
            Is.EqualTo("replacementDefinition"));
    }

    [Test]
    public void Inventory_TryAdd_CreatesInventoryOwnedItemInstance()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(apple, out var failure, 3), Is.True);
        var instance = inventory.Items.Single();
        instance.Metadata.Set("quality", "fresh");

        Assert.That(instance.Definition, Is.SameAs(apple));
        Assert.That(instance.Amount, Is.EqualTo(3));
        Assert.That(instance.Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void InventoryTransaction_TryAdd_CreatesInventoryOwnedItemInstance()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);

        Assert.That(builder.TryAdd(apple, out var buildError, 2), Is.True);
        Assert.That(inventory.TryCommitTransaction(builder.Build(), out var commitError), Is.True);

        var instance = inventory.Items.Single();
        Assert.That(instance.Definition, Is.SameAs(apple));
        Assert.That(instance.Amount, Is.EqualTo(2));
    }

    [Test]
    public void Serialize_And_Deserialize_RoundTrips_Items_And_Counts()
    {
        var manager = new InventoryManager<string>
        (
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
        );

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 3);
        inventory.TryAdd(berry, out _, 2);
        inventory.TryAdd(apple, out _, 1);

        SerializedInventory<string> serialized = inventory.Serialize();

        Assert.That(serialized, Is.Not.Null);
        Assert.That(serialized.Items.Count, Is.EqualTo(2));
        Assert.That(serialized.Items[0].DefinitionId, Is.EqualTo("apple"));
        Assert.That(serialized.Items[0].Amount, Is.EqualTo(4));
        Assert.That(serialized.Items[1].DefinitionId, Is.EqualTo("berry"));
        Assert.That(serialized.Items[1].Amount, Is.EqualTo(2));

        inventory.Deserialize(serialized);

        Assert.That(inventory.InstanceCount, Is.EqualTo(2));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(6));
        Assert.That(inventory.Items[0].Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(4));
        Assert.That(inventory.Items[1].Definition.Id, Is.EqualTo("berry"));
        Assert.That(inventory.Items[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void BatchOperation_FiresChangedExactlyOnce_ForComplexTransaction()
    {
        var manager = new InventoryManager<string>
        (
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
        );

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Registry.Register(carrot);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        int changedCallCount = 0;
        inventory.Changed += (_, _) => changedCallCount++;

        inventory.TryAdd(apple, out _, 5);
        inventory.TryAdd(berry, out _, 3);
        inventory.TryAdd(carrot, out _, 2);
        changedCallCount = 0;

        var appleInstance = inventory.Items[0];
        var berryInstance = inventory.Items[1];

        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 3);
        builder.TryRemove(berryInstance, out _, 2);
        builder.TryAdd(carrot, out _, 4);
        builder.TryRemoveAtStorageIndex(0, out _, 2);
        inventory.CommitTransaction(builder.Build());

        Assert.That(changedCallCount, Is.EqualTo(1), "Batch operation should fire Changed exactly once");

        Assert.That(inventory.TotalItemCount, Is.EqualTo(13));
        Assert.That(inventory.InstanceCount, Is.EqualTo(3));
    }

    [Test]
    public void BatchOperation_ChangedEventArgs_ReflectTransactionContent()
    {
        var manager = new InventoryManager<string>
        (
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
        );

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        inventory.TryAdd(apple, out _, 2);
        inventory.TryAdd(berry, out _, 1);
        var appleInstance = inventory.Items[0];

        InventoryChangedEventArgs<string>? capturedArgs = null;
        inventory.Changed += (_, e) => capturedArgs = (InventoryChangedEventArgs<string>)e;

        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(berry, out _, 2);
        builder.TryRemove(appleInstance, out _, 2);
        inventory.CommitTransaction(builder.Build());

        Assert.That(capturedArgs, Is.Not.Null);
        Assert.That(capturedArgs!.Added.Count, Is.EqualTo(0));
        Assert.That(capturedArgs.Modified.Count, Is.EqualTo(1));
        Assert.That(capturedArgs.Removed.Count, Is.EqualTo(1));

        var removedApple = capturedArgs.Removed[0].Instance;
        Assert.That(removedApple.Definition.Id, Is.EqualTo("apple"));
        Assert.That(removedApple.Amount, Is.EqualTo(2));

        var modifiedBerry = capturedArgs.Modified.Single(a => a.Instance.Definition.Id == "berry");
        Assert.That(modifiedBerry.Instance.Amount, Is.EqualTo(3));
    }

    [Test]
    public void TryMove_WithSlotLayout_MovesItemAndFiresMovedEvent()
    {
        var manager = CreateSlotInventoryManager(slotCount: 3);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(0));
        var appleInstance = inventory.Items[0];

        InventoryChangedEventArgs<string>? capturedArgs = null;
        inventory.Changed += (_, e) => capturedArgs = (InventoryChangedEventArgs<string>)e;

        var fromContext = new SlotLayoutContext<string>(0);
        var toContext = new SlotLayoutContext<string>(1);

        var result = inventory.TryMove(fromContext, toContext, out var failure);

        Assert.That(result, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(capturedArgs, Is.Not.Null);
        Assert.That(capturedArgs!.Moved.Count, Is.EqualTo(1));
        Assert.That(capturedArgs.Moved[0].Instance, Is.EqualTo(appleInstance));
        Assert.That(capturedArgs.Moved[0].FromPosition, Is.EqualTo(fromContext));
        Assert.That(capturedArgs.Moved[0].ToPosition, Is.EqualTo(toContext));

        var layout = (SlotLayout<string>)inventory.Layout;
        Assert.That(layout.GetItemAt(inventory, fromContext), Is.Null);
        Assert.That(layout.GetItemAt(inventory, toContext), Is.EqualTo(appleInstance));
    }

    [Test]
    public void TryMove_Fails_WhenSourceSlotEmpty()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        inventory.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(1));

        int changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        var fromContext = new SlotLayoutContext<string>(0);
        var toContext = new SlotLayoutContext<string>(1);

        var result = inventory.TryMove(fromContext, toContext, out var failure);

        Assert.That(result, Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Item not found in inventory."));
        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void TrySwap_WithSlotLayout_SwapsItemsAndFiresSwappedEvent()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(0));
        inventory.TryAdd(berry, out _, 1, new SlotLayoutContext<string>(1));

        var appleInstance = inventory.Items[0];
        var berryInstance = inventory.Items[1];

        InventoryChangedEventArgs<string>? capturedArgs = null;
        inventory.Changed += (_, e) => capturedArgs = (InventoryChangedEventArgs<string>)e;

        var slot0 = new SlotLayoutContext<string>(0);
        var slot1 = new SlotLayoutContext<string>(1);

        var result = inventory.TrySwap(slot0, slot1, out var failure);

        Assert.That(result, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(capturedArgs, Is.Not.Null);
        Assert.That(capturedArgs!.Swapped.Count, Is.EqualTo(1));

        var swap = capturedArgs.Swapped[0];
        Assert.That(swap.FromPosition, Is.EqualTo(slot0));
        Assert.That(swap.ToPosition, Is.EqualTo(slot1));
        Assert.That(swap.AfterSwapFromPositionInstance, Is.EqualTo(berryInstance));
        Assert.That(swap.AfterSwapToPositionInstance, Is.EqualTo(appleInstance));

        var layout = (SlotLayout<string>)inventory.Layout;
        Assert.That(layout.GetItemAt(inventory, slot0), Is.EqualTo(berryInstance));
        Assert.That(layout.GetItemAt(inventory, slot1), Is.EqualTo(appleInstance));
    }

    [Test]
    public void TrySwap_Fails_WhenOneSlotIsEmpty()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        inventory.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(0));

        int changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        var slot0 = new SlotLayoutContext<string>(0);
        var slot1 = new SlotLayoutContext<string>(1);

        var result = inventory.TrySwap(slot0, slot1, out var failure);

        Assert.That(result, Is.False);
        Assert.That(failure?.Message, Is.EqualTo("One or both of the items not found in inventory."));
        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void TryMergeMove_MergesUpToMaxStack_WhenAmountNotSpecified()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2, maxStack: 10);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 5, new SlotLayoutContext<string>(0));
        inventory.TryAdd(apple, out _, 7, new SlotLayoutContext<string>(1));

        var fromContext = new SlotLayoutContext<string>(1);
        var toContext = new SlotLayoutContext<string>(0);

        var result = inventory.TryMergeMove(fromContext, toContext, out var failure);

        Assert.That(result, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(inventory.TotalItemCount, Is.EqualTo(12));
        Assert.That(inventory.InstanceCount, Is.EqualTo(2));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(10));
        Assert.That(inventory.Items[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void TryMergeMove_FullyMovesStackAndRemovesSource_WhenAllItemsMoved()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2, maxStack: 10);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 3, new SlotLayoutContext<string>(0));
        inventory.TryAdd(apple, out _, 2, new SlotLayoutContext<string>(1));

        var fromContext = new SlotLayoutContext<string>(1);
        var toContext = new SlotLayoutContext<string>(0);

        var result = inventory.TryMergeMove(fromContext, toContext, out var failure);

        Assert.That(result, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
        Assert.That(inventory.InstanceCount, Is.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(5));
    }

    [Test]
    public void TryMergeMove_Fails_WhenRequestedAmountExceedsAvailableRoom()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2, maxStack: 10);

        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 9, new SlotLayoutContext<string>(0));
        inventory.TryAdd(apple, out _, 2, new SlotLayoutContext<string>(1));

        var fromContext = new SlotLayoutContext<string>(1);
        var toContext = new SlotLayoutContext<string>(0);

        int changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        var result = inventory.TryMergeMove(fromContext, toContext, out var failure, amount: 2);

        Assert.That(result, Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Not enough room in target stack to move the requested amount."));
        Assert.That(changedCount, Is.EqualTo(0));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(9));
        Assert.That(inventory.Items[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void TryMergeMove_Fails_WhenItemsAreNotStackCompatible()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2, maxStack: 10);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 5, new SlotLayoutContext<string>(0));
        inventory.TryAdd(berry, out _, 5, new SlotLayoutContext<string>(1));

        var fromContext = new SlotLayoutContext<string>(1);
        var toContext = new SlotLayoutContext<string>(0);

        var result = inventory.TryMergeMove(fromContext, toContext, out var failure);

        Assert.That(result, Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Items are not stack compatible."));
    }

    [Test]
    public void Add_CommitsWhenTryAddWouldSucceed()
    {
        var manager = CreateSlotInventoryManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.Add(apple, amount: 2);

        Assert.That(inventory.TotalItemCount, Is.EqualTo(2));
    }

    [Test]
    public void Add_ThrowsWhenTryAddWouldFail()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new MaxTotalItemAmountCapacityPolicy<string>(1),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        var exception = Assert.Throws<InventoryOperationException>(() => inventory.Add(apple, amount: 2));

        Assert.That(exception!.Message, Is.EqualTo("Capacity exceeded."));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void RemoveByDefinition_ThrowsWhenItemMissing()
    {
        var manager = CreateSlotInventoryManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.Throws<InventoryOperationException>(() => inventory.RemoveByDefinition(apple, amount: 1, metadataMatch: ItemMetadataMatch.Any));
    }

    [Test]
    public void Move_ThrowsWhenTryMoveWouldFail()
    {
        var manager = CreateSlotInventoryManager(slotCount: 2);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.Add(apple, context: new SlotLayoutContext<string>(1));

        var exception = Assert.Throws<InventoryOperationException>(() =>
            inventory.Move(new SlotLayoutContext<string>(0), new SlotLayoutContext<string>(1)));

        Assert.That(exception!.Message, Is.EqualTo("Item not found in inventory."));
    }

    [Test]
    public void Add_WrapperFiresChangedEventLikeTryAdd()
    {
        var manager = CreateSlotInventoryManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        int changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        inventory.Add(apple);

        Assert.That(changedCount, Is.EqualTo(1));
    }
}


