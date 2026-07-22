using System;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class TKeyOverloadTests
{
    [Test]
    public void InventoryTKeyOverloads_ResolveCurrentAndMigratedStringIds()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventoryWithMigration("old-apple", apple);

        Assert.That(inventory.TryAdd("old-apple", out var addError, amount: 5), Is.True);

        Assert.That(inventory.Count("apple"), Is.EqualTo(5));
        Assert.That(inventory.Count("old-apple"), Is.EqualTo(5));
        Assert.That(inventory.Contains("old-apple", amount: 4), Is.True);
        Assert.That(inventory.Find("old-apple"), Has.Count.EqualTo(1));
        Assert.That(inventory.Find("old-apple")[0].Definition, Is.SameAs(apple));

        Assert.That(inventory.TryRemoveByDefinition("old-apple", amount: 2, metadataMatch: ItemMetadataMatch.Any, out var removeError), Is.True);
        Assert.That(inventory.Count("apple"), Is.EqualTo(3));

        inventory.RemoveByDefinition("apple", amount: 1, metadataMatch: ItemMetadataMatch.Any);
        Assert.That(inventory.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void InventoryTKeyOverloads_UnknownIdsUseTryAndThrowingSemantics()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), apple);

        Assert.That(inventory.TryAdd("missing", out var addError), Is.False);
        Assert.That(addError?.Message, Does.Contain("could not be resolved"));

        Assert.That(inventory.TryRemoveByDefinition("missing", amount: 1, metadataMatch: ItemMetadataMatch.Any, out var removeError), Is.False);
        Assert.That(removeError?.Message, Does.Contain("could not be resolved"));

        Assert.Throws<InventoryOperationException>(() => inventory.Add("missing"));
        Assert.Throws<InventoryOperationException>(() => inventory.Count("missing"));
        Assert.Throws<InventoryOperationException>(() => inventory.Contains("missing"));
        Assert.Throws<InventoryOperationException>(() => inventory.Find("missing"));
        Assert.Throws<InventoryOperationException>(() => inventory.RemoveByDefinition("missing", amount: 1, metadataMatch: ItemMetadataMatch.Any));
    }

    [Test]
    public void InventoryTKeyOverloads_WorkWithIntegerKeysAndContexts()
    {
        var coin = new ItemDefinition<int>(100);
        var inventory = CreateInventory(new SlotLayout<int>(2), coin);

        Assert.That(inventory.TryAdd(100, out var failure, amount: 3, context: SlotLayoutContext<int>.Single(1)), Is.True);

        Assert.That(inventory.Count(100), Is.EqualTo(3));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<int>.Single(1))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void InventoryTKeyOverloads_WorkWithCustomValueObjects()
    {
        var key = new ItemKey("apple");
        var equivalentKey = new ItemKey("apple");
        var apple = new ItemDefinition<ItemKey>(key);
        var inventory = CreateInventory(new EntryLayout<ItemKey>(), apple);

        inventory.Add(equivalentKey, amount: 2);

        Assert.That(inventory.Count(key), Is.EqualTo(2));
        Assert.That(inventory.Find(equivalentKey)[0].Definition, Is.SameAs(apple));
    }

    [Test]
    public void TransactionBuilderTKeyOverloads_AddAndRemoveResolvedDefinitions()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventoryWithMigration("old-apple", apple, sword);
        inventory.Add("apple", amount: 2);

        var builder = InventoryTransaction<string>.From(inventory);

        Assert.That(builder.TryAdd("sword", out var addError, amount: 1, context: EntryLayoutContext<string>.Single(1)), Is.True);
        Assert.That(builder.TryRemoveByDefinition("old-apple", amount: 1, metadataMatch: ItemMetadataMatch.Any, out var removeError), Is.True);
        Assert.That(inventory.TryCommitTransaction(builder, out var commitError), Is.True);

        Assert.That(inventory.Count("apple"), Is.EqualTo(1));
        Assert.That(inventory.Count("sword"), Is.EqualTo(1));
    }

    [Test]
    public void TransactionBuilderTKeyOverloads_SupportMetadataAddOverload()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), apple);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "polished");

        var builder = InventoryTransaction<string>.From(inventory);

        Assert.That(builder.TryAdd("apple", amount: 1, context: null, metadata, out var failure), Is.True);
        Assert.That(inventory.TryCommitTransaction(builder, out var commitError), Is.True);
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("polished"));
    }

    [Test]
    public void TransferBuilderTKeyOverload_RemovesByResolvedDefinitionId()
    {
        var apple = new ItemDefinition<string>("apple");
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(apple);
        catalog.Registry.RegisterMigration("old-apple", apple);
        catalog.Freeze();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.Add("apple", amount: 5);

        var builder = InventoryTransfer.From(source);

        Assert.That(builder.TryRemoveByDefinition("old-apple", amount: 3, metadataMatch: ItemMetadataMatch.Any, out var failure), Is.True);
        Assert.That(source.TryCommitTransfer(builder, target, out var commitError), Is.True);

        Assert.That(source.Count("apple"), Is.EqualTo(2));
        Assert.That(target.Count("apple"), Is.EqualTo(3));
    }

    [Test]
    public void BuilderTKeyOverloads_UnknownIdsReturnFalse()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), apple);
        var transactionBuilder = InventoryTransaction<string>.From(inventory);
        var transferBuilder = InventoryTransfer.From(inventory);

        Assert.That(transactionBuilder.TryAdd("missing", out var addError), Is.False);
        Assert.That(addError?.Message, Does.Contain("could not be resolved"));
        Assert.That(transactionBuilder.TryRemoveByDefinition("missing", amount: 1, metadataMatch: ItemMetadataMatch.Any, out var removeError), Is.False);
        Assert.That(removeError?.Message, Does.Contain("could not be resolved"));
        Assert.That(transferBuilder.TryRemoveByDefinition("missing", amount: 1, metadataMatch: ItemMetadataMatch.Any, out var transferError), Is.False);
        Assert.That(transferError?.Message, Does.Contain("could not be resolved"));
    }

    private static Inventory<TKey> CreateInventory<TKey>(
        IInventoryLayout<TKey> layout,
        params ItemDefinition<TKey>[] definitions)
    {
        var catalog = new ItemCatalog<TKey>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();

        return new InventoryManager<TKey>(
            new FixedSizeStackResolver<TKey>(10),
            new UnlimitedCapacityPolicy<TKey>(),
            layout,
            catalog).CreateInventory();
    }

    private static Inventory<string> CreateInventoryWithMigration(
        string oldId,
        ItemDefinition<string> migratedDefinition,
        params ItemDefinition<string>[] additionalDefinitions)
    {
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(migratedDefinition);
        foreach (var definition in additionalDefinitions)
            catalog.Registry.Register(definition);
        catalog.Registry.RegisterMigration(oldId, migratedDefinition);
        catalog.Freeze();

        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog).CreateInventory();
    }

    private sealed class ItemKey : IEquatable<ItemKey>
    {
        public ItemKey(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public bool Equals(ItemKey? other) =>
            other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is ItemKey other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;
    }
}
