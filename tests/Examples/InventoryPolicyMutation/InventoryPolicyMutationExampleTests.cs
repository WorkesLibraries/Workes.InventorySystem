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
public class InventoryPolicyMutationExampleTests
{
    [Test]
    public void BackpackUpgradeFlow_UsesInventoryOwnedPolicyParameterChanges()
    {
        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("potion");
        var gem = new ItemDefinition<string>("gem");

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(5),
            new MaxTotalItemAmountCapacityPolicy<string>(11),
            new SlotLayout<string>(2),
            new ItemCatalog<string>()
            );

        manager.Registry.Register(coin);
        manager.Registry.Register(potion);
        manager.Registry.Register(gem);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var operations = new StringBuilder();

        inventory.Add(coin, amount: 5, context: SlotLayoutContext<string>.Single(0));
        operations.AppendLine("Add coin x5: committed");

        inventory.Add(potion, amount: 1, context: SlotLayoutContext<string>.Single(1));
        operations.AppendLine("Add potion x1: committed");

        bool slotUpgrade = inventory.TrySetLayoutParameter("slotCount", 3, out var slotUpgradeError);
        operations.AppendLine($"Set layout slotCount = 3: {FormatResult(slotUpgrade, slotUpgradeError)}");

        bool stackUpgrade = inventory.TrySetStackResolverParameter("maxStack", 10, out var stackUpgradeError);
        operations.AppendLine($"Set stack maxStack = 10: {FormatResult(stackUpgrade, stackUpgradeError)}");

        inventory.Add(coin, amount: 5);
        operations.AppendLine("Add coin x5: committed");

        bool lowerTooFar = inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 3, out var lowerTooFarError);
        operations.AppendLine($"Set capacity maxTotalItemAmount = 3: {FormatResult(lowerTooFar, lowerTooFarError)}");

        inventory.RemoveByDefinition(potion, amount: 1, ignoreMetadata: true);
        operations.AppendLine("Remove potion x1: committed");

        bool lowerToFit = inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 10, out var lowerToFitError);
        operations.AppendLine($"Set capacity maxTotalItemAmount = 10: {FormatResult(lowerToFit, lowerToFitError)}");

        bool addGem = inventory.TryAdd(gem, out var addGemError, amount: 1, context: SlotLayoutContext<string>.Single(2));
        operations.AppendLine($"Try add gem x1: {FormatResult(addGem, addGemError)}");

        Assert.That(slotUpgrade, Is.True);
        Assert.That(stackUpgrade, Is.True);
        Assert.That(lowerTooFar, Is.False);
        Assert.That(lowerToFit, Is.True);
        Assert.That(addGem, Is.False);
        Assert.That(inventory.Count(coin), Is.EqualTo(10));
        Assert.That(inventory.Count(potion), Is.EqualTo(0));

        var output = BuildOutput(operations.ToString(), inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Inventory policy mutation example written to: {outputPath}");
    }

    private static string FormatResult(bool accepted, string? error)
    {
        return accepted ? "committed" : $"rejected ({error})";
    }

    private static string BuildOutput(string operations, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Inventory Policy Mutation Example");
        builder.AppendLine("=================================");
        builder.AppendLine();
        builder.AppendLine("Operations");
        builder.AppendLine("----------");
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Current Parameters");
        builder.AppendLine("------------------");
        builder.AppendLine($"Stack maxStack: {((FixedSizeStackResolver<string>)inventory.StackResolver).MaxStack}");
        builder.AppendLine($"Capacity maxTotalItemAmount: {((MaxTotalItemAmountCapacityPolicy<string>)inventory.CapacityPolicy).MaxTotalItemAmount}");
        builder.AppendLine($"Layout slotCount: {inventory.Layout.GetPositionCount(inventory)}");
        builder.AppendLine();
        builder.AppendLine("Final Inventory");
        builder.AppendLine("---------------");

        foreach (var item in inventory.Items.OrderBy(item => item.Definition.Id))
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

        var path = Path.Combine(directory, "InventoryPolicyMutationExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
