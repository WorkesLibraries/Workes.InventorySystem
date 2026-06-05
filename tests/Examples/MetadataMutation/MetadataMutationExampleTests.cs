using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.MetadataMutation;

[TestFixture]
[Category("Example")]
public class MetadataMutationExampleTests
{
    [Test]
    public void StackMetadata_IsMutatedThroughInventoryOwnedMetadata()
    {
        var gem = new ItemDefinition<string>("gem");
        var potion = new ItemDefinition<string>("potion");

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new SlotLayout<string>(3));

        manager.Registry.Register(gem);
        manager.Registry.Register(potion);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var operations = new StringBuilder();

        inventory.Add(gem, amount: 5);
        operations.AppendLine("Add gem x5: committed");

        var gemStack = inventory.Items.Single();
        gemStack.Metadata.Set("quality", "common");
        operations.AppendLine("Set quality = common on gem x5: committed");

        bool changeQuality = gemStack.Metadata.TryChange("quality", "polished", out var changeQualityError);
        operations.AppendLine($"Change quality = polished on gem x5: {FormatResult(changeQuality, changeQualityError)}");

        bool splitQuestStack = gemStack.TrySplitAndSetMetadata(2, "quest-item", true, out var questStack, out var splitQuestStackError);
        operations.AppendLine($"Split gem x2 and set quest-item = true: {FormatResult(splitQuestStack, splitQuestStackError)}");

        bool ruleAdded = inventory.TrySetRule("requires-quality", new RequireMetadataKeyRule<string>("quality"), out var ruleError);
        operations.AppendLine($"Require quality metadata for future state: {FormatResult(ruleAdded, ruleError)}");

        bool removeRequiredQuality = questStack!.Metadata.TryRemove("quality", out var removeRequiredQualityError);
        operations.AppendLine($"Try remove required quality metadata: {FormatResult(removeRequiredQuality, removeRequiredQualityError)}");

        Assert.That(changeQuality, Is.True, changeQualityError);
        Assert.That(splitQuestStack, Is.True, splitQuestStackError);
        Assert.That(ruleAdded, Is.True, ruleError);
        Assert.That(removeRequiredQuality, Is.False);
        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EquivalentTo(new[] { 3, 2 }));
        Assert.That(questStack.Metadata.TryGet<bool>("quest-item", out var questItem), Is.True);
        Assert.That(questItem, Is.True);
        Assert.That(questStack.Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("polished"));

        var output = BuildOutput(operations.ToString(), inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Metadata mutation example written to: {outputPath}");
    }

    private static string FormatResult(bool accepted, string? error)
    {
        return accepted ? "committed" : $"rejected ({error})";
    }

    private static string BuildOutput(string operations, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Metadata Mutation Example");
        builder.AppendLine("=========================");
        builder.AppendLine();
        builder.AppendLine("Operations");
        builder.AppendLine("----------");
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Final Inventory");
        builder.AppendLine("---------------");

        foreach (var item in inventory.Items.OrderByDescending(item => item.Amount))
        {
            builder.AppendLine($"{item.Definition.Id} x{item.Amount}");
            foreach (var metadata in item.Metadata.AsReadOnly().OrderBy(pair => pair.Key))
                builder.AppendLine($"  {metadata.Key} = {metadata.Value}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "MetadataMutation");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "MetadataMutationExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
