using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryTransferExpansionTests
{
    private static InventoryManager<string> CreateManager(
        ItemCatalog<string>? catalog = null,
        ICapacityPolicy<string>? capacityPolicy = null,
        IInventoryLayout<string>? layout = null,
        RuleContainer<string>? rules = null,
        int maxStack = 10)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            capacityPolicy ?? new UnlimitedCapacityPolicy<string>(),
            layout ?? new EntryLayout<string>(),
            catalog ?? new ItemCatalog<string>(),
            rules);
    }

    private sealed class MaxTotalAmountCapacityPolicy : ICapacityPolicy<string>
    {
        private readonly int _maxTotalAmount;

        public MaxTotalAmountCapacityPolicy(int maxTotalAmount)
        {
            _maxTotalAmount = maxTotalAmount;
        }

        public bool CanApply(Inventory<string> inventory, NormalizedInventoryTransaction<string> normalizedTransaction, out string? error)
        {
            int added = normalizedTransaction.Added.Sum(i => i.amount);
            int removed = normalizedTransaction.Removed.Sum(i => i.amount);
            if (inventory.TotalItemCount + added - removed > _maxTotalAmount)
            {
                error = "Capacity exceeded.";
                return false;
            }

            error = null;
            return true;
        }

        public bool CanAdd(Inventory<string> inventory, ItemInstance<string> instance, out string? error)
        {
            if (inventory.TotalItemCount + instance.Amount > _maxTotalAmount)
            {
                error = "Capacity exceeded.";
                return false;
            }

            error = null;
            return true;
        }
    }

    [Test]
    public void From_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryTransfer.From<string>(null!));
    }

    [Test]
    public void TryRemove_RemovesFromSimulationOnly()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        source.TryAdd(apple, out _, 5);

        var builder = InventoryTransfer.From(source);
        var result = builder.TryRemove(source.Items[0], 2, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(5));
        Assert.That(builder.Entries.Single().Amount, Is.EqualTo(2));
    }

    [Test]
    public void TryRemove_RejectsInvalidAmountAndItemFromAnotherInventory()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var other = manager.CreateInventory();
        other.TryAdd(apple, out _, 1);
        var builder = InventoryTransfer.From(source);
        var foreignItem = other.Items[0];

        Assert.That(builder.TryRemove(foreignItem, 1, out var foreignError), Is.False);
        Assert.That(foreignError, Is.EqualTo("Item not found in inventory."));
        Assert.That(builder.TryRemove(foreignItem, 0, out var amountError), Is.False);
        Assert.That(amountError, Is.EqualTo("Amount must be greater than zero."));
    }

    [Test]
    public void TryRemoveAtStorageIndex_RejectsInvalidIndex()
    {
        var manager = CreateManager();
        manager.Catalog.Freeze();
        var builder = InventoryTransfer.From(manager.CreateInventory());

        Assert.That(builder.TryRemoveAtStorageIndex(0, 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Index out of range."));
    }

    [Test]
    public void TryRemoveByDefinition_RespectsIgnoreMetadata()
    {
        var manager = CreateManager(maxStack: 10);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var seed = InventoryTransaction<string>.From(source);
        seed.TryAdd(apple, out _, 3);
        seed.TryAdd(apple, 2, null, metadata, out _);
        source.CommitTransaction(seed.Build());

        var builder = InventoryTransfer.From(source);
        Assert.That(builder.TryRemoveByDefinition(apple, 5, ignoreMetadata: true, out var error), Is.True, error);

        Assert.That(builder.Entries.Sum(e => e.Amount), Is.EqualTo(5));
    }

    [Test]
    public void Inventory_TryCommitTransfer_CommitsOneSourceAndOneTargetEvent()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);
        source.TryAdd(berry, out _, 3);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Find(apple).Single(), 2, out _);
        builder.TryRemove(source.Find(berry).Single(), 1, out _);
        int sourceEvents = 0;
        int targetEvents = 0;
        source.Changed += (_, _) => sourceEvents++;
        target.Changed += (_, _) => targetEvents++;

        var result = source.TryCommitTransfer(builder, target, targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(sourceEvents, Is.EqualTo(1));
        Assert.That(targetEvents, Is.EqualTo(1));
        Assert.That(source.Count(apple), Is.EqualTo(0));
        Assert.That(source.Count(berry), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(berry), Is.EqualTo(1));
    }

    [Test]
    public void Inventory_TryCommitTransfer_TargetRuleFailureLeavesSourceUnchanged()
    {
        var food = "core:food";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(food);
        var apple = new ItemDefinition<string>("apple", food);
        var stone = new ItemDefinition<string>("stone");
        catalog.Registry.Register(apple);
        catalog.Registry.Register(stone);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var rules = new RuleContainer<string>();
        rules.Add("food-only", new RequireAllTagsRule<string>(food));
        var target = CreateManager(catalog, rules: rules).CreateInventory();
        source.TryAdd(stone, out _, 2);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);

        var result = source.TryCommitTransfer(builder, target, targetContext: null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("food-only"));
        Assert.That(source.Count(stone), Is.EqualTo(2));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void CanTransfer_ReturnsTrueWithoutMutating()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);
        int sourceEvents = 0;
        int targetEvents = 0;
        source.Changed += (_, _) => sourceEvents++;
        target.Changed += (_, _) => targetEvents++;

        var result = source.CanTransferTo(target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.EqualTo(0));
        Assert.That(sourceEvents, Is.EqualTo(0));
        Assert.That(targetEvents, Is.EqualTo(0));
    }

    [Test]
    public void Inventory_CanCommitTransfer_ValidatesWithoutMutating()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);
        int sourceEvents = 0;
        int targetEvents = 0;
        source.Changed += (_, _) => sourceEvents++;
        target.Changed += (_, _) => targetEvents++;

        var result = source.CanCommitTransfer(builder, target, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.EqualTo(0));
        Assert.That(sourceEvents, Is.EqualTo(0));
        Assert.That(targetEvents, Is.EqualTo(0));
    }

    [Test]
    public void Inventory_TryCommitTransfer_CommitsSourceAndTarget()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 3);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 2, out _);

        var result = source.TryCommitTransfer(builder, target, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(1));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void Inventory_TryCommitTransfer_WithTargetContext_CommitsMappedPlacement()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(layout: new SlotLayout<string>(2));
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);

        var result = source.TryCommitTransfer(builder, target, SlotLayoutContext<string>.Single(1), out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.Layout.GetItemAt(target, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(apple));
    }

    [Test]
    public void Inventory_CommitTransfer_ThrowsWhenRejected()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        var builder = InventoryTransfer.From(source);

        var ex = Assert.Throws<InvalidOperationException>(() => source.CommitTransfer(builder, target));

        Assert.That(ex!.Message, Is.EqualTo("Transfer contains no items."));
    }

    [Test]
    public void Inventory_TryCommitTransfer_ReturnsFalseForEmptyTransfer()
    {
        var manager = CreateManager();
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        var builder = InventoryTransfer.From(source);

        var result = source.TryCommitTransfer(builder, target, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Transfer contains no items."));
    }

    [Test]
    public void Inventory_TryCommitTransfer_RejectsBuilderFromAnotherSourceInventory()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var otherSource = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);

        var result = otherSource.TryCommitTransfer(builder, target, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Transfer builder does not belong to this inventory."));
        Assert.That(source.Count(apple), Is.EqualTo(1));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void InventoryTransfer_StaticBuilderTransfer_DelegatesToSourceInventoryCommit()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);

        var result = source.TryCommitTransfer(builder, target, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.TotalItemCount, Is.EqualTo(0));
        Assert.That(target.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void InventoryTransfer_StaticBuilderCanTransfer_DelegatesToSourceInventoryValidation()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        var builder = InventoryTransfer.From(source);
        builder.TryRemove(source.Items[0], 1, out _);

        var result = source.CanCommitTransfer(builder, target, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(1));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void TrySwap_SucceedsForPartialAmountsAndPreservesMetadata()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        var gem = new ItemDefinition<string>("gem");
        manager.Registry.Register(apple);
        manager.Registry.Register(gem);
        manager.Catalog.Freeze();
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();
        first.TryAdd(apple, out _, 5);
        var gemMetadata = new InstanceMetadata();
        gemMetadata.Set("rarity", "rare");
        var seed = InventoryTransaction<string>.From(second);
        seed.TryAdd(gem, 2, null, gemMetadata, out _);
        second.CommitTransaction(seed.Build());

        var result = first.TrySwapItemsWithInventory(second, first.Items[0], 3, second.Items[0], 1, null, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(first.Count(apple), Is.EqualTo(2));
        Assert.That(first.Count(gem), Is.EqualTo(1));
        Assert.That(second.Count(apple), Is.EqualTo(3));
        Assert.That(second.Count(gem), Is.EqualTo(1));
        Assert.That(first.Find(gem).Single().Metadata.StructuralEquals(gemMetadata), Is.True);
    }

    [Test]
    public void TrySwap_TargetCapacityFailureLeavesBothInventoriesUnchanged()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        var gem = new ItemDefinition<string>("gem");
        catalog.Registry.Register(apple);
        catalog.Registry.Register(gem);
        catalog.Freeze();
        var first = CreateManager(catalog).CreateInventory();
        var second = CreateManager(catalog, capacityPolicy: new MaxTotalAmountCapacityPolicy(1)).CreateInventory();
        first.TryAdd(apple, out _, 2);
        second.TryAdd(gem, out _, 1);

        var result = first.TrySwapItemsWithInventory(second, first.Items[0], 2, second.Items[0], 1, null, null, out _);

        Assert.That(result, Is.False);
        Assert.That(first.Count(apple), Is.EqualTo(2));
        Assert.That(first.Count(gem), Is.EqualTo(0));
        Assert.That(second.Count(gem), Is.EqualTo(1));
        Assert.That(second.Count(apple), Is.EqualTo(0));
    }

    [Test]
    public void TrySwapInventories_SucceedsForTwoNonEmptyInventories()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        var gem = new ItemDefinition<string>("gem");
        manager.Registry.Register(apple);
        manager.Registry.Register(gem);
        manager.Catalog.Freeze();
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();
        first.TryAdd(apple, out _, 2);
        second.TryAdd(gem, out _, 3);
        int firstEvents = 0;
        int secondEvents = 0;
        first.Changed += (_, _) => firstEvents++;
        second.Changed += (_, _) => secondEvents++;

        var result = first.TrySwapWithInventory(
            second,
            sourceTargetContext: null,
            otherTargetContext: null,
            out var error);

        Assert.That(result, Is.True, error);
        Assert.That(first.Count(gem), Is.EqualTo(3));
        Assert.That(first.Count(apple), Is.EqualTo(0));
        Assert.That(second.Count(apple), Is.EqualTo(2));
        Assert.That(second.Count(gem), Is.EqualTo(0));
        Assert.That(firstEvents, Is.EqualTo(1));
        Assert.That(secondEvents, Is.EqualTo(1));
    }

    [Test]
    public void TrySwapInventories_EmptyWithEmptySucceedsWithoutEvents()
    {
        var manager = CreateManager();
        manager.Catalog.Freeze();
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();
        int events = 0;
        first.Changed += (_, _) => events++;
        second.Changed += (_, _) => events++;

        var result = first.TrySwapWithInventory(
            second,
            sourceTargetContext: null,
            otherTargetContext: null,
            out var error);

        Assert.That(result, Is.True, error);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TryMoveAll_MovesEveryItemAllOrNothing()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        var gem = new ItemDefinition<string>("gem");
        manager.Registry.Register(apple);
        manager.Registry.Register(gem);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);
        source.TryAdd(gem, out _, 1);

        var result = source.TryMoveAllTo(target, targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.TotalItemCount, Is.EqualTo(0));
        Assert.That(target.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(gem), Is.EqualTo(1));
    }

    [Test]
    public void TryMoveByTag_UsesCatalogResolvedTags()
    {
        var fruit = "food:ingredient.fruit";
        var ingredient = "food:ingredient";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(fruit);
        var apple = new ItemDefinition<string>("apple", fruit);
        var stone = new ItemDefinition<string>("stone");
        catalog.Registry.Register(apple);
        catalog.Registry.Register(stone);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 2);
        source.TryAdd(stone, out _, 3);

        var result = source.TryMoveByTagTo(target, ingredient, targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(0));
        Assert.That(source.Count(stone), Is.EqualTo(3));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void TryMoveByTagTo_NonNamespacedMode_UsesDotHierarchy()
    {
        var fruit = "food.ingredient.fruit";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define(fruit);
        var apple = new ItemDefinition<string>("apple", fruit);
        var stone = new ItemDefinition<string>("stone");
        catalog.Registry.Register(apple);
        catalog.Registry.Register(stone);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 2);
        source.TryAdd(stone, out _, 3);

        var result = source.TryMoveByTagTo(target, "food.ingredient", targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.Count(apple), Is.EqualTo(2));
        Assert.That(source.Count(stone), Is.EqualTo(3));
    }

    [Test]
    public void TryMoveAllTags_RequiresEveryResolvedTag()
    {
        var fruit = "food:ingredient.fruit";
        var fresh = "state:fresh";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(fruit);
        catalog.Tags.Define(fresh);
        var apple = new ItemDefinition<string>("apple", fruit, fresh);
        var berry = new ItemDefinition<string>("berry", fruit);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(berry);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 1);
        source.TryAdd(berry, out _, 1);

        var result = source.TryMoveAllTagsTo(target, new[] { "food:ingredient", fresh }, targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.Count(apple), Is.EqualTo(1));
        Assert.That(target.Count(berry), Is.EqualTo(0));
        Assert.That(source.Count(berry), Is.EqualTo(1));
    }

    [Test]
    public void TryMoveAllTagsTo_NonNamespacedMode_UsesCatalogResolvedTags()
    {
        var fruit = "food.ingredient.fruit";
        var fresh = "state.fresh";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define(fruit);
        catalog.Tags.Define(fresh);
        var apple = new ItemDefinition<string>("apple", fruit, fresh);
        var berry = new ItemDefinition<string>("berry", fruit);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(berry);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 1);
        source.TryAdd(berry, out _, 1);

        var result = source.TryMoveAllTagsTo(target, new[] { "food.ingredient", "state" }, targetContext: null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.Count(apple), Is.EqualTo(1));
        Assert.That(target.Count(berry), Is.EqualTo(0));
    }

    [Test]
    public void TryMoveByTagTo_WrongModeTag_ReturnsFalseWithError()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("food");
        var apple = new ItemDefinition<string>("apple", "food");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 1);

        var result = source.TryMoveByTagTo(target, "core:food", targetContext: null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Transfer contains no items."));
        Assert.That(source.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void TryMoveWhere_NoMatchesReturnsFalse()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);

        var result = source.TryMoveWhereTo(target, _ => false, targetContext: null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Transfer contains no items."));
        Assert.That(source.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void TryTransferMaximum_MovesPartialAmountWhenTargetCapacityLimited()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog, capacityPolicy: new MaxTotalAmountCapacityPolicy(3)).CreateInventory();
        source.TryAdd(apple, out _, 5);
        target.TryAdd(apple, out _, 1);

        var result = source.TryTransferMaximumTo(target, source.Items[0], 5, null, out var moved, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(moved, Is.EqualTo(2));
        Assert.That(source.Count(apple), Is.EqualTo(3));
        Assert.That(target.Count(apple), Is.EqualTo(3));
    }

    [Test]
    public void TryTransferMaximum_ReturnsFalseWhenNothingCanMove()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog, capacityPolicy: new MaxTotalAmountCapacityPolicy(0)).CreateInventory();
        source.TryAdd(apple, out _, 1);

        var result = source.TryTransferMaximumTo(target, source.Items[0], 1, null, out var moved, out _);

        Assert.That(result, Is.False);
        Assert.That(moved, Is.EqualTo(0));
        Assert.That(source.Count(apple), Is.EqualTo(1));
        Assert.That(target.Count(apple), Is.EqualTo(0));
    }

    [Test]
    public void TryMoveMaximumByTag_ReturnsTotalTransferredAmount()
    {
        var fruit = "food:ingredient.fruit";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(fruit);
        var apple = new ItemDefinition<string>("apple", fruit);
        var berry = new ItemDefinition<string>("berry", fruit);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(berry);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog, capacityPolicy: new MaxTotalAmountCapacityPolicy(4)).CreateInventory();
        source.TryAdd(apple, out _, 3);
        source.TryAdd(berry, out _, 3);

        var result = source.TryMoveMaximumByTagTo(target, fruit, null, out var moved, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(moved, Is.EqualTo(4));
        Assert.That(source.TotalItemCount, Is.EqualTo(2));
        Assert.That(target.TotalItemCount, Is.EqualTo(4));
    }
}


