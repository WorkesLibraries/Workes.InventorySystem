using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryQueryTests
{
    private sealed class QueryAxeDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> AxeSchema =
            ItemSchema<string>.Create("query-axe")
                .AddTag("core:equipment.tools.axe");

        public QueryAxeDefinition(string id, params string[] tags)
            : base(id, AxeSchema, tags)
        {
        }
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string>? catalog = null, int maxStack = 10)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog ?? new ItemCatalog<string>());
    }

    [Test]
    public void Count_ReturnsSummedAmount_ForSameDefinitionReference()
    {
        var manager = CreateManager(maxStack: 5);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 12);

        Assert.That(inventory.Count(apple), Is.EqualTo(12));
    }

    [Test]
    public void Count_DoesNotMatchDifferentDefinitionWithSameId()
    {
        var manager = CreateManager();
        var registeredApple = new ItemDefinition<string>("apple");
        var otherApple = new ItemDefinition<string>("apple");
        manager.Registry.Register(registeredApple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.TryAdd(registeredApple, out _, 3);

        Assert.That(inventory.Count(otherApple), Is.EqualTo(0));
        Assert.That(inventory.Find(otherApple), Is.Empty);
    }

    [Test]
    public void Contains_ReturnsTrueOnlyWhenEnoughAmountExists()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 4);

        Assert.That(inventory.Contains(apple, 3), Is.True);
        Assert.That(inventory.Contains(apple, 5), Is.False);
    }

    [Test]
    public void Contains_ThrowsForInvalidAmount()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager();
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Contains(apple, 0));
    }

    [Test]
    public void Find_ReturnsAllMatchingInstances_AsSnapshot()
    {
        var manager = CreateManager(maxStack: 10);
        var apple = new ItemDefinition<string>("apple");
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 3);
        builder.TryAdd(apple, 2, null, metadata, out _);
        inventory.CommitTransaction(builder.Build());

        var snapshot = inventory.Find(apple);
        inventory.TryAdd(apple, out _, 1);

        Assert.That(snapshot.Count, Is.EqualTo(2));
        Assert.That(snapshot.All(i => ReferenceEquals(i.Definition, apple)), Is.True);
        Assert.That(inventory.Count(apple), Is.EqualTo(6));
    }

    [Test]
    public void FindByTag_UsesCatalogResolvedSchemaDefinitionAndParentTags()
    {
        var tool = "core:equipment.tools";
        var axeTag = "core:equipment.tools.axe";
        var material = "c:materials.wood";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(axeTag);
        catalog.Tags.Define(material);
        var axe = new QueryAxeDefinition("axe", material);
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(axe);
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(axe, out _, 1);
        inventory.TryAdd(apple, out _, 1);

        Assert.That(inventory.FindByTag(tool).Single().Definition, Is.SameAs(axe));
        Assert.That(inventory.FindByTag(material).Single().Definition, Is.SameAs(axe));
    }

    [Test]
    public void FindByTag_NonNamespacedMode_UsesDotHierarchy()
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
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 2);
        inventory.TryAdd(stone, out _, 1);

        Assert.That(inventory.FindByTag("food.ingredient").Single().Definition, Is.SameAs(apple));
        Assert.That(inventory.FindByTag("food").Single().Definition, Is.SameAs(apple));
    }

    [Test]
    public void CountByTag_SumsResolvedTagMatches()
    {
        var fruit = "food:ingredient.fruit";
        var ingredient = "food:ingredient";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(fruit);
        var apple = new ItemDefinition<string>("apple", fruit);
        var berry = new ItemDefinition<string>("berry", fruit);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(berry);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 3);
        inventory.TryAdd(berry, out _, 2);

        Assert.That(inventory.CountByTag(ingredient), Is.EqualTo(5));
    }

    [Test]
    public void CountByTag_NonNamespacedMode_UsesDotHierarchy()
    {
        var fruit = "food.ingredient.fruit";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define(fruit);
        var apple = new ItemDefinition<string>("apple", fruit);
        var berry = new ItemDefinition<string>("berry", fruit);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(berry);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 3);
        inventory.TryAdd(berry, out _, 2);

        Assert.That(inventory.CountByTag("food.ingredient"), Is.EqualTo(5));
    }

    [Test]
    public void ContainsAllTags_RequiresOneDefinitionSatisfyingEveryResolvedTag()
    {
        var fruit = "food:ingredient.fruit";
        var ingredient = "food:ingredient";
        var wood = "crafting:material.wood";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define(fruit);
        catalog.Tags.Define(wood);
        var apple = new ItemDefinition<string>("apple", fruit);
        var log = new ItemDefinition<string>("oak_log", wood);
        catalog.Registry.Register(apple);
        catalog.Registry.Register(log);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 1);
        inventory.TryAdd(log, out _, 1);

        Assert.That(inventory.ContainsAllTags(ingredient, fruit), Is.True);
        Assert.That(inventory.ContainsAllTags(ingredient, wood), Is.False);
    }

    [Test]
    public void ContainsAllTags_NonNamespacedMode_UsesDotHierarchy()
    {
        var fruit = "food.ingredient.fruit";
        var fresh = "state.fresh";
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define(fruit);
        catalog.Tags.Define(fresh);
        var apple = new ItemDefinition<string>("apple", fruit, fresh);
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 1);

        Assert.That(inventory.ContainsAllTags("food.ingredient", "state"), Is.True);
    }

    [Test]
    public void FindByTag_NamespacedMode_DoesNotMatchFlatTag()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define("core:food.fruit");
        var apple = new ItemDefinition<string>("apple", "core:food.fruit");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var inventory = CreateManager(catalog).CreateInventory();

        inventory.TryAdd(apple, out _, 1);

        Assert.That(inventory.FindByTag("food.fruit"), Is.Empty);
    }

    [Test]
    public void FindWhere_ReturnsPredicateMatches()
    {
        var manager = CreateManager();
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        manager.Registry.Register(apple);
        manager.Registry.Register(berry);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _, 4);
        inventory.TryAdd(berry, out _, 1);

        var matches = inventory.FindWhere(item => item.Amount > 1);

        Assert.That(matches.Single().Definition, Is.SameAs(apple));
    }

    [Test]
    public void QueryMethods_ValidateArguments()
    {
        var manager = CreateManager();
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        var tag = "core:test";

        Assert.Throws<ArgumentNullException>(() => inventory.Count(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.Contains(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.Find(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.FindByTag(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.CountByTag(null!));
        Assert.Throws<ArgumentException>(() => inventory.ContainsAllTags());
        Assert.Throws<ArgumentException>(() => inventory.ContainsAllTags(tag, null!));
        Assert.Throws<ArgumentNullException>(() => inventory.FindWhere(null!));
    }
}


