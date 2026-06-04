using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class LayoutContextTransactionTests
{
    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string> layout,
        int maxStack = 10,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new RuleContainer<string>());

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    [Test]
    public void InventoryTransaction_From_CreatesUsableBuilder()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();

        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, out var error, 2), Is.True, error);

        Assert.That(inventory.TryCommitTransaction(builder.ToInventoryTransaction(), out error), Is.True, error);
        Assert.That(inventory.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void InventoryTransactionBuilder_Build_EqualsToInventoryTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 2);

        var legacy = builder.ToInventoryTransaction();
        var built = builder.Build();

        Assert.That(built.Added.Count, Is.EqualTo(legacy.Added.Count));
        Assert.That(built.Added.Single().instance.Definition, Is.SameAs(apple));
        Assert.That(built.Added.Single().instance.Amount, Is.EqualTo(2));
    }

    [Test]
    public void InventoryTransactionBuilder_TryBuild_AppliesPlacementContext()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);

        Assert.That(builder.TryBuild(SlotLayoutContext<string>.Single(1), out var transaction, out var error), Is.True, error);
        Assert.That(transaction!.Added.Single().context, Is.TypeOf<SlotLayoutContext<string>>());
        Assert.That(((SlotLayoutContext<string>)transaction.Added.Single().context!).SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void Inventory_TryCommitTransactionBuilder_CommitsBuiltTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 3);

        Assert.That(inventory.TryCommitTransaction(builder, out var error), Is.True, error);

        Assert.That(inventory.Count(apple), Is.EqualTo(3));
    }

    [Test]
    public void Inventory_TryCommitTransactionBuilder_WithPlacementContext_CommitsMappedTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);

        Assert.That(inventory.TryCommitTransaction(builder, SlotLayoutContext<string>.Single(1), out var error), Is.True, error);

        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(apple));
    }

    [Test]
    public void InventoryTransaction_IsEmpty_IsTrueForNoOpBuilder()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);

        Assert.That(builder.IsEmpty, Is.True);
        Assert.That(builder.Build().IsEmpty, Is.True);
    }

    [Test]
    public void InventoryTransaction_IsEmpty_IsFalseWhenBuilderHasChanges()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);

        Assert.That(builder.IsEmpty, Is.False);
        Assert.That(builder.Build().IsEmpty, Is.False);
    }

    [Test]
    public void SlotLayoutContext_Map_StoresAddedEntrySlotMappings()
    {
        var context = SlotLayoutContext<string>.Map()
            .Add(0, 3)
            .Add(1, 4)
            .Build();

        Assert.That(context.IsMapped, Is.True);
        Assert.That(context.SlotIndex, Is.EqualTo(-1));
        Assert.That(context.AddedEntrySlots[0], Is.EqualTo(3));
        Assert.That(context.AddedEntrySlots[1], Is.EqualTo(4));
        Assert.That(SlotLayoutContext<string>.Single(2).SlotIndex, Is.EqualTo(new SlotLayoutContext<string>(2).SlotIndex));
    }

    [Test]
    public void SlotLayout_RejectsDuplicateMappedTargetSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new SlotLayout<string>(5), 1, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 1);
        builder.TryAdd(sword, out _, 1);
        var context = SlotLayoutContext<string>.Map().Add(0, 3).Add(1, 3).Build();

        var result = builder.TryToInventoryTransaction(context, out _, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Duplicate mapped target slot."));
    }

    [Test]
    public void SlotLayout_RejectsInvalidMappedTargetSlot()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), 10, apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 1);
        var context = SlotLayoutContext<string>.Map().Add(0, 9).Build();

        var result = builder.TryToInventoryTransaction(context, out _, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Slot index out of range."));
    }

    [Test]
    public void SlotLayout_PlacesMultiAddTransactionIntoMappedSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new SlotLayout<string>(5), 10, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 5);
        builder.TryAdd(sword, out _, 2);
        var context = SlotLayoutContext<string>.Map().Add(0, 3).Add(1, 4).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(3))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(4))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void SlotLayout_MappedAddCanTargetSlotFreedBySameTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new SlotLayout<string>(2), 1, apple, sword).CreateInventory();
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryRemove(inventory.Items[0], out _, 1);
        builder.TryAdd(sword, out _, 1);
        var context = SlotLayoutContext<string>.Map().Add(0, 0).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void SlotLayout_MappedAddRejectsOccupiedSlotNotFreedByTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new SlotLayout<string>(2), 1, apple, sword).CreateInventory();
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(sword, out _, 1);
        var context = SlotLayoutContext<string>.Map().Add(0, 0).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Slot already occupied."));
    }

    [Test]
    public void SlotLayout_AddedIndexMappingDistinguishesSameDefinitionDifferentMetadata()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateManager(new SlotLayout<string>(2), 10, gem).CreateInventory();
        var polished = new InstanceMetadata();
        polished.Set("quality", "polished");
        var cracked = new InstanceMetadata();
        cracked.Set("quality", "cracked");
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(gem, 1, null, polished, out _);
        builder.TryAdd(gem, 1, null, cracked, out _);
        var context = SlotLayoutContext<string>.Map().Add(0, 1).Add(1, 0).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Metadata.TryGet<string>("quality", out var polishedQuality);
        inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Metadata.TryGet<string>("quality", out var crackedQuality);
        Assert.That(polishedQuality, Is.EqualTo("polished"));
        Assert.That(crackedQuality, Is.EqualTo("cracked"));
    }

    [Test]
    public void EntryLayout_MultiEntryInsertionMappingUsesDeterministicOrder()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new EntryLayout<string>(), 1, apple, berry, carrot, sword).CreateInventory();
        inventory.TryAdd(apple, out _);
        inventory.TryAdd(berry, out _);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(carrot, out _);
        builder.TryAdd(sword, out _);
        var context = EntryLayoutContext<string>.Map().Insert(0, 1).Insert(1, 1).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        Assert.That(inventory.Layout.GetItemAt(inventory, EntryLayoutContext<string>.Single(0))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, EntryLayoutContext<string>.Single(1))!.Definition.Id, Is.EqualTo("carrot"));
        Assert.That(inventory.Layout.GetItemAt(inventory, EntryLayoutContext<string>.Single(2))!.Definition.Id, Is.EqualTo("sword"));
        Assert.That(inventory.Layout.GetItemAt(inventory, EntryLayoutContext<string>.Single(3))!.Definition.Id, Is.EqualTo("berry"));
    }

    [Test]
    public void EntryLayout_MappedInsertionRejectsOutOfRangeIndex()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), 10, apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 1);
        var context = EntryLayoutContext<string>.Map().Insert(0, 2).Build();

        Assert.That(builder.TryToInventoryTransaction(context, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Target index out of range."));
    }

    [Test]
    public void SlotLayout_AmountDeltaDoesNotRequirePlacementMapping()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), 10, apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5, SlotLayoutContext<string>.Single(0));
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        var result = inventory.TryAdd(apple, out var error, 2, SlotLayoutContext<string>.Single(0));

        Assert.That(result, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(7));
        Assert.That(changed, Is.EqualTo(1));
    }

    [Test]
    public void TransferBuilder_WithMappedTargetContext_PlacesIncomingEntriesIntoSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new SlotLayout<string>(5), 10, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 5);
        source.TryAdd(sword, out _, 2);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemove(source.Find(apple).Single(), 5, out _);
        transfer.TryRemove(source.Find(sword).Single(), 2, out _);
        var context = SlotLayoutContext<string>.Map().Add(0, 3).Add(1, 4).Build();

        Assert.That(InventoryTransfer.TryTransfer(transfer, target, context, out var error), Is.True, error);

        Assert.That(target.Layout.GetItemAt(target, SlotLayoutContext<string>.Single(3))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(target.Layout.GetItemAt(target, SlotLayoutContext<string>.Single(4))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void TransferPlanning_UsesLayoutContextMappedContractWithoutConcreteContextSwitch()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new EntryLayout<string>(), 10, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory(layout: new SectionedLayout<string>(
            new SectionDefinition<string>("hotbar", 2),
            new SectionDefinition<string>("bag", 1)));
        source.TryAdd(apple, out _, 1);
        source.TryAdd(sword, out _, 1);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemoveByDefinition(apple, 1, ignoreMetadata: true, out _);
        transfer.TryRemoveByDefinition(sword, 1, ignoreMetadata: true, out _);
        ILayoutContext<string> context = SectionedLayoutContext<string>.Map()
            .Add(0, "bag", 0)
            .Add(1, "hotbar", 1)
            .Build();

        Assert.That(context.IsMapped, Is.True);
        Assert.That(InventoryTransfer.TryTransfer(transfer, target, context, out var error), Is.True, error);
        Assert.That(source.Items, Is.Empty);
        Assert.That(target.Layout.GetItemAt(target, SectionedLayoutContext<string>.Single("bag", 0))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(target.Layout.GetItemAt(target, SectionedLayoutContext<string>.Single("hotbar", 1))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void FailedMappedTransferLeavesInventoriesUnchangedAndFiresNoEvents()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new SlotLayout<string>(2), 1, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _);
        source.TryAdd(sword, out _);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemove(source.Items[0], 1, out _);
        transfer.TryRemove(source.Items[1], 1, out _);
        int sourceEvents = 0;
        int targetEvents = 0;
        source.Changed += (_, _) => sourceEvents++;
        target.Changed += (_, _) => targetEvents++;
        var context = SlotLayoutContext<string>.Map().Add(0, 0).Add(1, 0).Build();

        Assert.That(InventoryTransfer.TryTransfer(transfer, target, context, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Duplicate mapped target slot."));
        Assert.That(source.TotalItemCount, Is.EqualTo(2));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
        Assert.That(sourceEvents, Is.EqualTo(0));
        Assert.That(targetEvents, Is.EqualTo(0));
    }

    [Test]
    public void MultiEntryTransfer_RejectsSimpleSharedContextButAllowsNull()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new SlotLayout<string>(5), 10, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        source.TryAdd(sword, out _, 1);
        var rejected = InventoryTransfer.From(source);
        rejected.TryRemove(source.Items[0], 1, out _);
        rejected.TryRemove(source.Items[1], 1, out _);

        Assert.That(InventoryTransfer.TryTransfer(rejected, target, SlotLayoutContext<string>.Single(0), out var error), Is.False);
        Assert.That(error, Is.EqualTo("Transaction placement context can only target one added entry unless it is a mapped context."));

        var accepted = InventoryTransfer.From(source);
        accepted.TryRemove(source.Items[0], 1, out _);
        accepted.TryRemove(source.Items[1], 1, out _);
        Assert.That(InventoryTransfer.TryTransfer(accepted, target, targetContext: null, out error), Is.True, error);
    }

    [Test]
    public void TrySwapInventories_WithMappedContexts_PlacesBothDirections()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new SlotLayout<string>(4), 10, apple, sword);
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();
        first.TryAdd(apple, out _, 3);
        second.TryAdd(sword, out _, 1);
        var firstContext = SlotLayoutContext<string>.Map().Add(0, 2).Build();
        var secondContext = SlotLayoutContext<string>.Map().Add(0, 1).Build();

        Assert.That(InventoryTransfer.TrySwapInventories(first, second, firstContext, secondContext, out var error), Is.True, error);

        Assert.That(first.Layout.GetItemAt(first, SlotLayoutContext<string>.Single(2))!.Definition.Id, Is.EqualTo("sword"));
        Assert.That(second.Layout.GetItemAt(second, SlotLayoutContext<string>.Single(1))!.Definition.Id, Is.EqualTo("apple"));
    }
}


