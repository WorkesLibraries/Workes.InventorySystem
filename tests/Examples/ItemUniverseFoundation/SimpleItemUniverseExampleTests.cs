using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.ItemUniverseFoundation;

[TestFixture]
[Category("Example")]
public class SimpleItemUniverseExampleTests
{
    [Test]
    public void PlainDefinitions_DoNotNeedSchemasTagsOrAttributes()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("health_potion");
        var definitions = new[] { apple, coin, potion };

        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(99),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog: catalog);
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(apple, out var appleError, 5), Is.True, appleError);
        Assert.That(inventory.TryAdd(coin, out var coinError, 25), Is.True, coinError);
        Assert.That(inventory.TryAdd(potion, out var potionError, 2), Is.True, potionError);
        Assert.That(inventory.TryRemoveByDefinition(apple, amount: 1, ignoreMetadata: true, out var removeError), Is.True, removeError);

        Assert.That(definitions.All(d => d.Schema == ItemSchema<string>.Default), Is.True);
        Assert.That(definitions.All(d => !d.Attributes.GetAllKeys().Any()), Is.True);
        Assert.That(definitions.All(d => !d.Tags.All().Any()), Is.True);

        var artifactPath = WriteExampleOutput(definitions);
        TestContext.Out.WriteLine("Item universe foundation simple example output: " + artifactPath);
    }

    private static string WriteExampleOutput(ItemDefinition<string>[] definitions)
    {
        var outputDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "ItemUniverseFoundation");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "SimpleExample.txt");
        var builder = new StringBuilder();

        builder.AppendLine("Item Universe Foundation - Simple Example");
        builder.AppendLine("=========================================");
        builder.AppendLine();
        builder.AppendLine("Registered Definitions");
        builder.AppendLine("----------------------");

        foreach (var definition in definitions.OrderBy(d => d.Id))
        {
            builder.AppendLine(definition.Id);
            builder.AppendLine("  Schema: " + definition.Schema.Id);
            builder.AppendLine("  Attributes: none");
            builder.AppendLine("  Tags: none");
            builder.AppendLine();
        }

        builder.AppendLine("This example uses the default schema and no tags or attributes.");

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }
}


