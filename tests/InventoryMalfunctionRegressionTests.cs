using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;
namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryMalfunctionRegressionTests
{
    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string>? layout = null,
        IRulePolicy<string>? rule = null,
        int maxStack = 10,
        params ItemDefinition<string>[] definitions)
    {
        var rules = new RuleContainer<string>();
        if (rule != null)
            rules.Add("rule", rule);

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout ?? new EntryLayout<string>(),
            new ItemCatalog<string>(),
            rules
            );

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();

        return manager;
    }

    [Test]
    public void CreateInventory_ClonesDefaultLayout_PerInventory()
    {
        var apple = new ItemDefinition<string>("apple");
        var entryManager = CreateManager(new EntryLayout<string>(), definitions: apple);
        var entryA = entryManager.CreateInventory();
        var entryB = entryManager.CreateInventory();

        entryA.TryAdd(apple, out _);

        Assert.That(entryB.InstanceCount, Is.EqualTo(0));
        Assert.That(entryB.Layout.GetItemAt(entryB, new EntryLayoutContext<string>(0)), Is.Null);

        var slotManager = CreateManager(new SlotLayout<string>(2), definitions: apple);
        var slotA = slotManager.CreateInventory();
        var slotB = slotManager.CreateInventory();

        slotA.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(0));

        Assert.That(slotB.InstanceCount, Is.EqualTo(0));
        Assert.That(slotB.Layout.GetItemAt(slotB, new SlotLayoutContext<string>(0)), Is.Null);
    }

    [Test]
    public void EntryLayout_TryGetContextForStorageIndex_ReturnsEntryPosition()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: new[] { apple, berry }).CreateInventory();

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(berry, out _);

        Assert.That(inventory.Layout.TryGetContextForStorageIndex(inventory, 1, out var context), Is.True);
        var entryContext = (EntryLayoutContext<string>)context!;
        Assert.That(entryContext.TargetIndex, Is.EqualTo(1));
    }

    [Test]
    public void SlotLayout_TryGetContextForStorageIndex_ReturnsSlotPosition()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateManager(new SlotLayout<string>(4), definitions: new[] { apple, berry }).CreateInventory();

        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(2));
        inventory.TryAdd(berry, out _, 1, SlotLayoutContext<string>.Single(0));

        Assert.That(inventory.Layout.TryGetContextForStorageIndex(inventory, 0, out var context), Is.True);
        var slotContext = (SlotLayoutContext<string>)context!;
        Assert.That(slotContext.SlotIndex, Is.EqualTo(2));
    }

    [Test]
    public void TransactionBuilder_AddThenRemoveAddedItem_CommitsFinalNetState()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();

        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, out var addError, 5), Is.True);
        Assert.That(builder.TryRemoveByDefinition(apple, 2, ItemMetadataMatch.Any, out var removeError), Is.True);

        Assert.DoesNotThrow(() => inventory.CommitTransaction(builder.Build()));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(3));
        Assert.That(inventory.InstanceCount, Is.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(3));
    }

    [Test]
    public void TransactionBuilder_AddThenRemoveAllAddedItem_CommitsNoNetItem()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 5);
        builder.TryRemoveByDefinition(apple, 5, ItemMetadataMatch.Any, out _);

        var changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        inventory.CommitTransaction(builder.Build());

        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
        Assert.That(inventory.InstanceCount, Is.EqualTo(0));
        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void TransactionBuilder_RemoveExistingThenAdd_CommitsFinalNetState()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5);

        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryRemoveByDefinition(apple, 2, ItemMetadataMatch.Any, out _);
        builder.TryAdd(apple, out _, 4);

        Assert.DoesNotThrow(() => inventory.CommitTransaction(builder.Build()));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(7));
    }

    [Test]
    public void TryAdd_MergingIntoExistingStack_FiresModified_NotAdded()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 2);

        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        inventory.TryAdd(apple, out _, 3);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Added.Count, Is.EqualTo(0));
        Assert.That(captured.Modified.Count, Is.EqualTo(1));
        Assert.That(captured.Modified[0].Instance.Amount, Is.EqualTo(5));
    }

    [Test]
    public void TryRemove_PartialStack_FiresModified_NotRemoved()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5);

        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        inventory.TryRemoveByDefinition(apple, 2, ItemMetadataMatch.Any, out _);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Removed.Count, Is.EqualTo(0));
        Assert.That(captured.Modified.Count, Is.EqualTo(1));
        Assert.That(captured.Modified[0].Instance.Amount, Is.EqualTo(3));
    }

    [Test]
    public void TryRemove_FullStack_FiresRemoved_NotModified()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5);

        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        inventory.TryRemoveByDefinition(apple, 5, ItemMetadataMatch.Any, out _);

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Removed.Count, Is.EqualTo(1));
        Assert.That(captured.Modified.Count, Is.EqualTo(0));
    }

    [Test]
    public void InventoryChangedEventArgs_OptionalConstructor_DefaultsMissingListsToEmpty()
    {
        var args = new InventoryChangedEventArgs<string>(cleared: true);

        Assert.That(args.Added, Is.Not.Null);
        Assert.That(args.Removed, Is.Not.Null);
        Assert.That(args.Modified, Is.Not.Null);
        Assert.That(args.Moved, Is.Not.Null);
        Assert.That(args.Swapped, Is.Not.Null);
        Assert.That(args.ConfigurationChanged, Is.Not.Null);
        Assert.That(args.Cleared, Is.True);
    }

    [Test]
    public void AttributeContainer_ResolvesAttributes_ByIdAndValueType()
    {
        var container = new AttributeContainer();

        container.Set("weight", 5);

        Assert.That(container.TryGet<int>("weight", out int value), Is.True);
        Assert.That(value, Is.EqualTo(5));
        Assert.That(container.TryGet<string>("weight", out _), Is.False);
    }

    [Test]
    public void ItemDefinitionTags_ResolveEquivalentTags_ById()
    {
        var a = "core:food";
        var b = "core:food";
        var definition = new ItemDefinition<string>("apple", a);

        Assert.That(definition.HasTag(b), Is.True);
    }

    [Test]
    public void EntryLayout_AddWithContext_InsertsAtTargetIndex()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: new[] { apple, berry, carrot }).CreateInventory();

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(berry, out _);
        inventory.TryAdd(carrot, out _, 1, new EntryLayoutContext<string>(1));

        Assert.That(inventory.Layout.GetItemAt(inventory, new EntryLayoutContext<string>(0))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, new EntryLayoutContext<string>(1))!.Definition.Id, Is.EqualTo("carrot"));
        Assert.That(inventory.Layout.GetItemAt(inventory, new EntryLayoutContext<string>(2))!.Definition.Id, Is.EqualTo("berry"));
    }

    [Test]
    public void EntryLayout_AddWithContext_AllowsInsertAtEnd()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: new[] { apple, berry }).CreateInventory();

        inventory.TryAdd(apple, out _);

        Assert.That(inventory.TryAdd(berry, out var failure, 1, new EntryLayoutContext<string>(1)), Is.True);
        Assert.That(inventory.Layout.GetItemAt(inventory, new EntryLayoutContext<string>(1))!.Definition.Id, Is.EqualTo("berry"));
    }

    [Test]
    public void EntryLayout_AddWithContext_RejectsOutOfRangeIndex()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: apple).CreateInventory();

        Assert.That(inventory.TryAdd(apple, out var failure, 1, new EntryLayoutContext<string>(1)), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Target index out of range."));
    }

    [Test]
    public void EntryLayout_GetPersistentData_ReturnsDefensiveCopy()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateManager(new EntryLayout<string>(), definitions: new[] { apple, berry }).CreateInventory();

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(berry, out _);

        var data = (EntryLayoutPersistentData)inventory.Layout.GetPersistentData();
        data.Order.Clear();

        Assert.That(inventory.Layout.GetItemAt(inventory, new EntryLayoutContext<string>(0)), Is.Not.Null);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void TryMergeMove_Fails_WhenRequestedAmountIsNotPositive(int amount)
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), maxStack: 10, definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5, new SlotLayoutContext<string>(0));
        inventory.TryAdd(apple, out _, 2, new SlotLayoutContext<string>(1));

        var changedCount = 0;
        inventory.Changed += (_, _) => changedCount++;

        Assert.That(inventory.TryMergeMove(new SlotLayoutContext<string>(1), new SlotLayoutContext<string>(0), out var failure, amount), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Amount must be greater than zero."));
        Assert.That(changedCount, Is.EqualTo(0));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(5));
        Assert.That(inventory.Items[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void TryMergeMove_Fails_WhenRequestedAmountExceedsSourceAmount()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(2), maxStack: 10, definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5, new SlotLayoutContext<string>(0));
        inventory.TryAdd(apple, out _, 2, new SlotLayoutContext<string>(1));

        Assert.That(inventory.TryMergeMove(new SlotLayoutContext<string>(1), new SlotLayoutContext<string>(0), out var failure, 3), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Not enough quantity to move."));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(5));
        Assert.That(inventory.Items[1].Amount, Is.EqualTo(2));
    }

    [Test]
    public void UniqueItemRule_AllowsSingleStackWithAmountGreaterThanOne()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(rule: new UniqueItemRule<string>(1), definitions: apple).CreateInventory();

        Assert.That(inventory.TryAdd(apple, out var failure, 10), Is.True);
    }

    [Test]
    public void UniqueItemRule_RejectsSecondStackOfSameDefinition()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(rule: new UniqueItemRule<string>(1), definitions: apple).CreateInventory();
        var metaA = new InstanceMetadata();
        metaA.Set("quality", "A");
        var metaB = new InstanceMetadata();
        metaB.Set("quality", "B");

        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, 1, null, metaA, out var addError), Is.True);
        inventory.CommitTransaction(builder.Build());

        var secondBuilder = InventoryTransaction<string>.From(inventory);
        Assert.That(secondBuilder.TryAdd(apple, 1, null, metaB, out var secondError), Is.False);
        Assert.That(secondError, Is.Not.Null);
    }

    [Test]
    public void OnlyAllowItemsRule_ThrowsArgumentNull_WhenAllowedArrayIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new OnlyAllowItemsRule<string>(null!));
    }

    [Test]
    public void OnlyAllowItemsRule_ThrowsArgumentException_WhenAllowedContainsNull()
    {
        var apple = new ItemDefinition<string>("apple");

        Assert.Throws<ArgumentException>(() => new OnlyAllowItemsRule<string>(apple, null!));
    }
}


