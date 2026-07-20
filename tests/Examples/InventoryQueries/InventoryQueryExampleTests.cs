using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.InventoryQueries;

[TestFixture]
[Category("Example")]
public class InventoryQueryExampleTests
{
    [Test]
    public void BackpackAndCraftingInventory_CanQueryDefinitionsAndTags()
    {
        var catalog = new ItemCatalog<string>();
        var fruit = catalog.Tags.Define("food:ingredient.fruit");
        var vegetable = catalog.Tags.Define("food:ingredient.vegetable");
        var wood = catalog.Tags.Define("crafting:material.wood");
        var ingredient = catalog.Tags.Get("food:ingredient");
        var craftingMaterial = catalog.Tags.Get("crafting:material");

        var apple = new ItemDefinition<string>("apple", fruit);
        var berry = new ItemDefinition<string>("berry", fruit);
        var carrot = new ItemDefinition<string>("carrot", vegetable);
        var oakLog = new ItemDefinition<string>("oak_log", wood);
        var definitions = new[] { apple, berry, carrot, oakLog };

        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();

        var backpack = CreateManager(catalog).CreateInventory();
        backpack.TryAdd("apple", out _, 4);
        backpack.TryAdd("berry", out _, 2);
        backpack.TryAdd("carrot", out _, 1);
        backpack.TryAdd("oak_log", out _, 6);

        Assert.That(backpack.Count("apple"), Is.EqualTo(4));
        Assert.That(backpack.Contains("apple", 3), Is.True);
        Assert.That(backpack.Find("apple").Single().Definition, Is.SameAs(apple));
        Assert.That(backpack.FindByTag(ingredient).Count, Is.EqualTo(3));
        Assert.That(backpack.CountByTag(craftingMaterial), Is.EqualTo(6));
        Assert.That(backpack.ContainsAllTags(ingredient, fruit), Is.True);

        var outputPath = WriteExampleOutput(
            backpack,
            apple,
            ingredient,
            craftingMaterial,
            new Dictionary<string, object>
            {
                { "Count(\"apple\")", backpack.Count("apple") },
                { "Contains(\"apple\", 3)", backpack.Contains("apple", 3) },
                { "Find(\"apple\")", string.Join(", ", backpack.Find("apple").Select(i => i.Definition.Id + " x" + i.Amount)) },
                { "FindByTag(food:ingredient)", string.Join(", ", backpack.FindByTag(ingredient).Select(i => i.Definition.Id + " x" + i.Amount)) },
                { "CountByTag(crafting:material)", backpack.CountByTag(craftingMaterial) },
                { "ContainsAllTags(food:ingredient, food:ingredient.fruit)", backpack.ContainsAllTags(ingredient, fruit) }
            });

        TestContext.Out.WriteLine("Inventory query example output: " + outputPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog: catalog);
    }

    private static string WriteExampleOutput(
        Inventory<string> backpack,
        ItemDefinition<string> apple,
        string ingredient,
        string craftingMaterial,
        IReadOnlyDictionary<string, object> queryResults)
    {
        var outputDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "InventoryQueries");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "InventoryQueriesExample.txt");
        var builder = new StringBuilder();

        builder.AppendLine("Inventory Queries Example");
        builder.AppendLine("=========================");
        builder.AppendLine();
        builder.AppendLine("Backpack Contents");
        builder.AppendLine("-----------------");
        foreach (var item in backpack.Items.OrderBy(i => i.Definition.Id, StringComparer.Ordinal))
            builder.AppendLine(item.Definition.Id + " x" + item.Amount);

        builder.AppendLine();
        builder.AppendLine("Query Results");
        builder.AppendLine("-------------");
        builder.AppendLine("Definition queried: " + apple.Id);
        builder.AppendLine("Ingredient tag: " + ingredient);
        builder.AppendLine("Crafting material tag: " + craftingMaterial);
        builder.AppendLine();

        foreach (var result in queryResults)
            builder.AppendLine(result.Key + ": " + result.Value);

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }
}


