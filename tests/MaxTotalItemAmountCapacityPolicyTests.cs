using System;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class MaxTotalItemAmountCapacityPolicyTests
{
    [Test]
    public void Constructor_RejectsNegativeMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaxTotalItemAmountCapacityPolicy<string>(-1));
    }

    [Test]
    public void CanAdd_AcceptsProjectedTotalEqualToLimit()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple);
        inventory.TryAdd(apple, out _, 3);
        var instance = new ItemInstance<string>(apple, 2);

        var result = new MaxTotalItemAmountCapacityPolicy<string>(5).CanAdd(inventory, instance, out var error);

        Assert.That(result, Is.True, error);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void CanAdd_RejectsProjectedTotalGreaterThanLimit()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple);
        inventory.TryAdd(apple, out _, 4);
        var instance = new ItemInstance<string>(apple, 2);

        var result = new MaxTotalItemAmountCapacityPolicy<string>(5).CanAdd(inventory, instance, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
    }

    [Test]
    public void TryAdd_SucceedsUpToLimit()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple);

        var result = inventory.TryAdd(apple, out var error, 5);

        Assert.That(result, Is.True, error);
        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
    }

    [Test]
    public void TryAdd_RejectsOverLimitWithoutMutation()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple);
        inventory.TryAdd(apple, out _, 4);

        var result = inventory.TryAdd(apple, out var error, 2);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(4));
    }

    [Test]
    public void TryCommitTransaction_RejectsStaleTransactionThatWouldExceedCapacityAndFiresNoEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple, berry);
        inventory.TryAdd(apple, out _, 4);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 1);
        var transaction = builder.ToInventoryTransaction();
        inventory.TryAdd(berry, out _, 1);
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        var result = inventory.TryCommitTransaction(transaction, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
        Assert.That(changed, Is.EqualTo(0));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
    }

    [Test]
    public void TransactionRemovalCreatesRoomInSameCommit()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new MaxTotalItemAmountCapacityPolicy<string>(5), apple, berry);
        inventory.TryAdd(apple, out _, 5);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryRemove(inventory.Items[0], out _, 2);
        builder.TryAdd(berry, out _, 2);

        var result = inventory.TryCommitTransaction(builder.ToInventoryTransaction(), out var error);

        Assert.That(result, Is.True, error);
        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
        Assert.That(inventory.Count(apple), Is.EqualTo(3));
        Assert.That(inventory.Count(berry), Is.EqualTo(2));
    }

    [Test]
    public void Transfer_TargetCapacityFailureLeavesBothInventoriesUnchanged()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var source = CreateManager(catalog, new UnlimitedCapacityPolicy<string>()).CreateInventory();
        var target = CreateManager(catalog, new MaxTotalItemAmountCapacityPolicy<string>(2)).CreateInventory();
        source.TryAdd(apple, out _, 3);
        target.TryAdd(apple, out _, 2);

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, null, out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
        Assert.That(source.Count(apple), Is.EqualTo(3));
        Assert.That(target.Count(apple), Is.EqualTo(2));
    }

    private static Inventory<string> CreateInventory(ICapacityPolicy<string> capacityPolicy, params ItemDefinition<string>[] definitions)
    {
        var manager = CreateManager(null, capacityPolicy);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string>? catalog, ICapacityPolicy<string> capacityPolicy)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            capacityPolicy,
            new EntryLayout<string>(),
            catalog: catalog);
    }
}
