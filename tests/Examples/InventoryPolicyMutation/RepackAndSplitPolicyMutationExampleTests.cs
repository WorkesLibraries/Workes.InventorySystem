using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.InventoryPolicyMutation;

[TestFixture]
[Category("Example")]
public class RepackAndSplitPolicyMutationExampleTests
{
    [Test]
    public void BackpackDowngradeFlow_UsesOptionalRepackAndSplitting()
    {
        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("potion");

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new MaxTotalItemAmountCapacityPolicy<string>(20),
            new SlotLayout<string>(4),
            new ItemCatalog<string>()
            );

        manager.Registry.Register(coin);
        manager.Registry.Register(potion);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var operations = new StringBuilder();

        inventory.Add(coin, amount: 10, context: SlotLayoutContext<string>.Single(0));
        operations.AppendLine("Add coin x10: committed");

        inventory.Add(potion, amount: 1, context: SlotLayoutContext<string>.Single(3));
        operations.AppendLine("Add potion x1 at slot 3: committed");

        bool lowerWithoutSplit = inventory.TrySetStackResolverParameter("maxStack", 5, out var lowerWithoutSplitError);
        operations.AppendLine($"Lower maxStack to 5 without split: {FormatResult(lowerWithoutSplit, lowerWithoutSplitError)}");

        bool lowerWithSplitAndRepack = inventory.TrySetStackResolverParameter(
            "maxStack",
            5,
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks,
            out var lowerWithSplitAndRepackError);
        operations.AppendLine($"Lower maxStack to 5 with split/repack: {FormatResult(lowerWithSplitAndRepack, lowerWithSplitAndRepackError)}");

        bool shrinkWithoutRepack = inventory.TrySetLayoutParameter("slotCount", 2, out var shrinkWithoutRepackError);
        operations.AppendLine($"Shrink slotCount to 2 without repack: {FormatResult(shrinkWithoutRepack, shrinkWithoutRepackError)}");

        bool shrinkWithRepack = inventory.TrySetLayoutParameter(
            "slotCount",
            3,
            InventoryParameterMutationActions.RepackLayout,
            out var shrinkWithRepackError);
        operations.AppendLine($"Shrink slotCount to 3 with repack: {FormatResult(shrinkWithRepack, shrinkWithRepackError)}");

        Assert.That(lowerWithoutSplit, Is.False);
        Assert.That(lowerWithSplitAndRepack, Is.True, lowerWithSplitAndRepackError?.Message);
        Assert.That(shrinkWithoutRepack, Is.False);
        Assert.That(shrinkWithRepack, Is.True, shrinkWithRepackError?.Message);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EquivalentTo(new[] { 5, 5, 1 }));
        Assert.That(inventory.GetLayoutPositionCount(), Is.EqualTo(3));
        Assert.That(inventory.Count(coin), Is.EqualTo(10));
        Assert.That(inventory.Count(potion), Is.EqualTo(1));

        var output = BuildOutput(operations.ToString(), inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Inventory policy repack/split example written to: {outputPath}");
    }

    private static string FormatResult(bool accepted, InventoryFailure? failure)
    {
        return accepted ? "committed" : $"rejected ({failure})";
    }

    private static string BuildOutput(string operations, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Inventory Policy Repack And Split Example");
        builder.AppendLine("=========================================");
        builder.AppendLine();
        builder.AppendLine("Operations");
        builder.AppendLine("----------");
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Final Inventory");
        builder.AppendLine("---------------");

        foreach (var item in inventory.Items)
            builder.AppendLine($"{item.Definition.Id}: {item.Amount}");

        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "InventoryPolicyMutation");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "RepackAndSplitPolicyMutationExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
