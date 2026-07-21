using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

#pragma warning disable CS0618 // Legacy persistence compatibility coverage.

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryRegisteredDefinitionTests
{
    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string>? layout = null,
        int maxStack = 10,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout ?? new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        manager.Catalog.Freeze();
        return manager;
    }

    [Test]
    public void Inventory_TryAdd_RejectsUnregisteredDefinition()
    {
        var manager = CreateManager();
        var inventory = manager.CreateInventory();
        var detached = new ItemDefinition<string>("family_heirloom");
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TryAdd(detached, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("not registered"));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void Inventory_Add_ThrowsForUnregisteredDefinition()
    {
        var manager = CreateManager();
        var inventory = manager.CreateInventory();
        var detached = new ItemDefinition<string>("family_heirloom");

        var exception = Assert.Throws<InventoryOperationException>(() => inventory.Add(detached));

        Assert.That(exception!.Message, Does.Contain("not registered"));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void Inventory_TryAdd_RejectsDetachedDefinitionWithRegisteredId()
    {
        var registered = new ItemDefinition<string>("coin");
        var manager = CreateManager(definitions: registered);
        var inventory = manager.CreateInventory();
        var detachedSameId = new ItemDefinition<string>("coin");

        var accepted = inventory.TryAdd(detachedSameId, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("not the registered definition instance"));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
    }

    [Test]
    public void Inventory_TryAdd_AcceptsCanonicalRegisteredDefinition()
    {
        var registered = new ItemDefinition<string>("coin");
        var manager = CreateManager(definitions: registered);
        var inventory = manager.CreateInventory();

        var accepted = inventory.TryAdd(registered, out var failure, amount: 2);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Count(registered), Is.EqualTo(2));
        Assert.That(inventory.Items.Single().Definition, Is.SameAs(registered));
    }

    [Test]
    public void InventoryTransactionBuilder_TryAdd_RejectsUnregisteredDefinition()
    {
        var manager = CreateManager();
        var inventory = manager.CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        var detached = new ItemDefinition<string>("gem");

        var accepted = builder.TryAdd(detached, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("not registered"));
        Assert.That(builder.IsEmpty, Is.True);
    }

    [Test]
    public void InventoryTransactionBuilder_TryAdd_RejectsDetachedDefinitionWithRegisteredId()
    {
        var registered = new ItemDefinition<string>("gem");
        var manager = CreateManager(definitions: registered);
        var inventory = manager.CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        var detachedSameId = new ItemDefinition<string>("gem");

        var accepted = builder.TryAdd(detachedSameId, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("not the registered definition instance"));
        Assert.That(builder.IsEmpty, Is.True);
    }

    [Test]
    public void TryFormulateFromNormalized_RejectsUnregisteredAddedDefinition()
    {
        var manager = CreateManager();
        var inventory = manager.CreateInventory();
        var detached = new ItemDefinition<string>("gem");
        var normalized = new NormalizedInventoryTransaction<string>(
            new() { (detached, null, 1) },
            new());

        var accepted = inventory.TryFormulateFromNormalized(normalized, out var transaction, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(transaction, Is.Null);
        Assert.That(failure?.Message, Does.Contain("not registered"));
    }

    [Test]
    public void Inventory_TryTransferTo_UsesRegisteredSourceDefinitions()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(definitions: apple);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.Add(apple, amount: 2);

        var accepted = source.TryTransferTo(target, source.Items[0], 1, targetContext: null, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(target.Items.Single().Definition, Is.SameAs(apple));
    }

    [Test]
    public void SplitAndSetMetadata_PreservesRegisteredDefinitionInvariant()
    {
        var gem = new ItemDefinition<string>("gem");
        var manager = CreateManager(new SlotLayout<string>(2), definitions: gem);
        var inventory = manager.CreateInventory();
        inventory.Add(gem, amount: 5);

        var accepted = inventory.Items[0].TrySplitAndSetMetadata(2, "quest-item", true, out var metadataStack, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(metadataStack, Is.Not.Null);
        Assert.That(inventory.Items.All(item => ReferenceEquals(item.Definition, gem)), Is.True);
    }

    [Test]
    public void RepackAndSplit_PreservesRegisteredDefinitionInvariant()
    {
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new SlotLayout<string>(3), maxStack: 10, definitions: coin);
        var inventory = manager.CreateInventory();
        inventory.Add(coin, amount: 10);

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            5,
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks,
            out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Items, Has.Count.EqualTo(2));
        Assert.That(inventory.Items.All(item => ReferenceEquals(item.Definition, coin)), Is.True);
    }

    [Test]
    public void Deserialize_UsesRegisteredDefinitionInstance()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(definitions: apple);
        var source = manager.CreateInventory();
        source.Add(apple, amount: 2);
        var serialized = source.Serialize();
        var inventory = manager.CreateInventory();

        inventory.Deserialize(serialized);

        Assert.That(inventory.Items.Single().Definition, Is.SameAs(apple));
    }

    [Test]
    public void Deserialize_RejectsUnknownDefinitionId()
    {
        var manager = CreateManager();
        var inventory = manager.CreateInventory();
        var serialized = new SerializedInventory<string>
        {
            Items =
            {
                new SerializedItem<string>
                {
                    DefinitionId = "missing",
                    Amount = 1
                }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => inventory.Deserialize(serialized));

        Assert.That(exception!.Message, Does.Contain("could not be resolved"));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(0));
    }
}
