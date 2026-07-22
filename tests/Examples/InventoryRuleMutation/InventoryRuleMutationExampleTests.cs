using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.InventoryRuleMutation;

[TestFixture]
[Category("Example")]
public class InventoryRuleMutationExampleTests
{
    [Test]
    public void InventoryOwnedRuleChanges_ValidateExistingContentsBeforeCommitting()
    {
        var questGem = new ItemDefinition<string>("quest_gem");
        var apple = new ItemDefinition<string>("apple");

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        manager.Registry.Register(questGem);
        manager.Registry.Register(apple);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var operations = new StringBuilder();

        inventory.Add(questGem);
        operations.AppendLine("Add quest_gem: committed");

        inventory.Add(apple);
        operations.AppendLine("Add apple: committed");

        var questOnlyRule = new OnlyAllowItemsRule<string>(questGem);
        bool enabledWithApplePresent = inventory.TrySetRule("quest-only", questOnlyRule, out var applePresentError);
        operations.AppendLine($"Enable quest-only rule while apple is present: {FormatResult(enabledWithApplePresent, applePresentError)}");

        inventory.RemoveByDefinition(apple, amount: 1, metadataMatch: ItemMetadataMatch.Any);
        operations.AppendLine("Remove apple: committed");

        bool enabledAfterCleanup = inventory.TrySetRule("quest-only", questOnlyRule, out var cleanupError);
        operations.AppendLine($"Enable quest-only rule after cleanup: {FormatResult(enabledAfterCleanup, cleanupError)}");

        bool addAppleAfterRule = inventory.TryAdd(apple, out var addAppleError);
        operations.AppendLine($"Try add apple after rule enabled: {FormatResult(addAppleAfterRule, addAppleError)}");

        Assert.That(enabledWithApplePresent, Is.False);
        Assert.That(enabledAfterCleanup, Is.True);
        Assert.That(addAppleAfterRule, Is.False);
        Assert.That(inventory.Rules.ContainsKey("quest-only"), Is.True);
        Assert.That(inventory.Items.Single().Definition, Is.SameAs(questGem));

        var output = BuildOutput(operations.ToString(), inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Inventory rule mutation example written to: {outputPath}");
    }

    private static string FormatResult(bool accepted, InventoryFailure? failure)
    {
        return accepted ? "committed" : $"rejected ({failure})";
    }

    private static string BuildOutput(string operations, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Inventory Rule Mutation Example");
        builder.AppendLine("===============================");
        builder.AppendLine();
        builder.AppendLine("Operations");
        builder.AppendLine("----------");
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Final Inventory");
        builder.AppendLine("---------------");

        foreach (var item in inventory.Items.OrderBy(i => i.Definition.Id))
            builder.AppendLine($"{item.Definition.Id}: {item.Amount}");

        builder.AppendLine();
        builder.AppendLine("Active Rules");
        builder.AppendLine("------------");
        foreach (var rule in inventory.Rules.Keys.OrderBy(id => id))
            builder.AppendLine(rule);

        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "InventoryRuleMutation");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "InventoryRuleMutationExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
