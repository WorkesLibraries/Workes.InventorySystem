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
public class InventoryTransferTests
{
    private static InventoryManager<string> CreateManager(
        ItemCatalog<string>? catalog = null,
        ICapacityPolicy<string>? capacityPolicy = null,
        IInventoryLayout<string>? layout = null,
        RuleContainer<string>? rules = null,
        int maxStack = 10)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(maxStack),
            capacityPolicy ?? new UnlimitedCapacityPolicy<string>(),
            layout ?? new EntryLayout<string>(),
            rules,
            catalog);
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
            var added = normalizedTransaction.Added.Sum(i => i.amount);
            var removed = normalizedTransaction.Removed.Sum(i => i.amount);
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
    public void SameManagerTransfer_Succeeds()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 5);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 2, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(3));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void DifferentManagersWithSameCatalogTransfer_Succeeds()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog).CreateInventory();
        source.TryAdd(apple, out _, 4);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 4, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.Count(apple), Is.EqualTo(0));
        Assert.That(target.Count(apple), Is.EqualTo(4));
    }

    [Test]
    public void DifferentCatalogsTransfer_IsRejectedEvenWhenIdsMatch()
    {
        var sourceApple = new ItemDefinition<string>("apple");
        var targetApple = new ItemDefinition<string>("apple");
        var sourceManager = CreateManager();
        var targetManager = CreateManager();
        sourceManager.Registry.Register(sourceApple);
        targetManager.Registry.Register(targetApple);
        sourceManager.Catalog.Freeze();
        targetManager.Catalog.Freeze();
        var source = sourceManager.CreateInventory();
        var target = targetManager.CreateInventory();
        source.TryAdd(sourceApple, out _, 2);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Inventories must share the same item catalog."));
        Assert.That(source.Count(sourceApple), Is.EqualTo(2));
        Assert.That(target.Count(targetApple), Is.EqualTo(0));
    }

    [Test]
    public void InvalidAmount_IsRejectedAndNoInventoryChanges()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 0, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Amount must be greater than zero."));
        Assert.That(source.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.EqualTo(0));
    }

    [Test]
    public void SourceItemNotInSourceInventory_IsRejected()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        var detached = new ItemInstance<string>(apple, 1);

        var result = InventoryTransfer.TryTransfer(source, target, detached, 1, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Item not found in source inventory."));
    }

    [Test]
    public void TargetRuleRejection_LeavesSourceAndTargetUnchanged()
    {
        var food = TagKey.Parse("core:food");
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

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Does.Contain("food-only"));
        Assert.That(source.Count(stone), Is.EqualTo(2));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void TargetLayoutRejection_LeavesSourceAndTargetUnchanged()
    {
        var manager = CreateManager(layout: new SlotLayout<string>(2));
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1, new SlotLayoutContext<string>(0));
        target.TryAdd(berry, out _, 1, new SlotLayoutContext<string>(0));

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, new SlotLayoutContext<string>(0), out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Slot already occupied."));
        Assert.That(source.Count(apple), Is.EqualTo(1));
        Assert.That(target.Count(berry), Is.EqualTo(1));
        Assert.That(target.Count(apple), Is.EqualTo(0));
    }

    [Test]
    public void TargetStacking_IsRespectedWhenCompatibleStackExists()
    {
        var manager = CreateManager(maxStack: 10);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 3);
        target.TryAdd(apple, out _, 4);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 3, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.InstanceCount, Is.EqualTo(1));
        Assert.That(target.Items[0].Amount, Is.EqualTo(7));
    }

    [Test]
    public void TargetCapacityRejection_LeavesSourceAndTargetUnchanged()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog).CreateInventory();
        var target = CreateManager(catalog, capacityPolicy: new MaxTotalAmountCapacityPolicy(2)).CreateInventory();
        source.TryAdd(apple, out _, 3);
        target.TryAdd(apple, out _, 2);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
        Assert.That(source.Count(apple), Is.EqualTo(3));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void Metadata_IsPreservedStructurallyOnTarget()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(source);
        builder.TryAdd(apple, 2, null, metadata, out _);
        source.CommitTransaction(builder.ToInventoryTransaction());

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(target.Items[0].Metadata.StructuralEquals(metadata), Is.True);
        Assert.That(ReferenceEquals(target.Items[0].Metadata, source.Items[0].Metadata), Is.False);
    }

    [Test]
    public void SuccessfulTransfer_FiresChangedOnceOnBothInventories()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);
        int sourceChanged = 0;
        int targetChanged = 0;
        source.Changed += (_, _) => sourceChanged++;
        target.Changed += (_, _) => targetChanged++;

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(sourceChanged, Is.EqualTo(1));
        Assert.That(targetChanged, Is.EqualTo(1));
    }

    [Test]
    public void FailedTransfer_FiresNoChangedEvents()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 1);
        int sourceChanged = 0;
        int targetChanged = 0;
        source.Changed += (_, _) => sourceChanged++;
        target.Changed += (_, _) => targetChanged++;

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 2, null, out _);

        Assert.That(result, Is.False);
        Assert.That(sourceChanged, Is.EqualTo(0));
        Assert.That(targetChanged, Is.EqualTo(0));
    }

    [Test]
    public void FullStackTransfer_RemovesSourceInstance()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 2, null, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(source.InstanceCount, Is.EqualTo(0));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }
}
