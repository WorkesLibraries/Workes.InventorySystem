using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.CrossInventoryTransfer;

[TestFixture]
[Category("Example")]
public class CrossInventoryTransferExampleTests
{
    [Test]
    public void BackpackCanTransferValidCraftingMaterialWithoutLosingRejectedItems()
    {
        var catalog = new ItemCatalog<string>();
        var wood = catalog.Tags.Define("crafting:material.wood");
        var material = catalog.Tags.Get("crafting:material");
        var food = catalog.Tags.Define("food:ingredient.fruit");
        var oakLog = new ItemDefinition<string>("oak_log", wood);
        var apple = new ItemDefinition<string>("apple", food);
        catalog.Registry.Register(oakLog);
        catalog.Registry.Register(apple);
        catalog.Freeze();

        var backpack = CreateManager(catalog).CreateInventory();
        backpack.TryAdd(oakLog, out _, 5);
        backpack.TryAdd(apple, out _, 2);

        var craftingRules = new RuleContainer<string>();
        craftingRules.Add("materials-only", new RequireAllTagsRule<string>(material));
        var craftingInput = CreateManager(catalog, craftingRules).CreateInventory();

        var beforeBackpack = Describe(backpack);
        var beforeCraftingInput = Describe(craftingInput);
        var movedLogs = InventoryTransfer.TryTransfer(backpack, craftingInput, backpack.Find(oakLog).Single(), 3, null, out var moveLogError);
        var rejectedApple = InventoryTransfer.TryTransfer(backpack, craftingInput, backpack.Find(apple).Single(), 1, null, out var appleError);

        Assert.That(movedLogs, Is.True, moveLogError);
        Assert.That(rejectedApple, Is.False);
        Assert.That(backpack.Count(oakLog), Is.EqualTo(2));
        Assert.That(backpack.Count(apple), Is.EqualTo(2));
        Assert.That(craftingInput.Count(oakLog), Is.EqualTo(3));
        Assert.That(craftingInput.Count(apple), Is.EqualTo(0));

        var outputPath = WriteExampleOutput(
            beforeBackpack,
            beforeCraftingInput,
            Describe(backpack),
            Describe(craftingInput),
            movedLogs,
            moveLogError,
            rejectedApple,
            appleError);

        TestContext.Out.WriteLine("Cross-inventory transfer example output: " + outputPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog, RuleContainer<string>? rules = null)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            rules,
            catalog);
    }

    private static IReadOnlyList<string> Describe(Inventory<string> inventory)
    {
        return inventory.Items
            .OrderBy(i => i.Definition.Id, StringComparer.Ordinal)
            .Select(i => i.Definition.Id + " x" + i.Amount)
            .ToList();
    }

    private static string WriteExampleOutput(
        IReadOnlyList<string> beforeBackpack,
        IReadOnlyList<string> beforeCraftingInput,
        IReadOnlyList<string> afterBackpack,
        IReadOnlyList<string> afterCraftingInput,
        bool movedLogs,
        string? moveLogError,
        bool rejectedApple,
        string? appleError)
    {
        var outputDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "CrossInventoryTransfer");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "CrossInventoryTransferExample.txt");
        var builder = new StringBuilder();

        builder.AppendLine("Cross-Inventory Transfer Example");
        builder.AppendLine("================================");
        builder.AppendLine();
        AppendSection(builder, "Backpack Before", beforeBackpack);
        AppendSection(builder, "Crafting Input Before", beforeCraftingInput);

        builder.AppendLine("Transfer Results");
        builder.AppendLine("----------------");
        builder.AppendLine("oak_log x3: " + (movedLogs ? "accepted" : "rejected: " + moveLogError));
        builder.AppendLine("apple x1: " + (rejectedApple ? "accepted" : "rejected: " + appleError));
        builder.AppendLine();

        AppendSection(builder, "Backpack After", afterBackpack);
        AppendSection(builder, "Crafting Input After", afterCraftingInput);

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }

    private static void AppendSection(StringBuilder builder, string title, IReadOnlyList<string> lines)
    {
        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
        if (lines.Count == 0)
            builder.AppendLine("empty");
        else
        {
            foreach (var line in lines)
                builder.AppendLine(line);
        }

        builder.AppendLine();
    }
}


