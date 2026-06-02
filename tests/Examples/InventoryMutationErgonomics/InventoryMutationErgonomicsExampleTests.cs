using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.InventoryMutationErgonomics;

[TestFixture]
[Category("Example")]
public class InventoryMutationErgonomicsExampleTests
{
    [Test]
    public void SmallInventory_UsesThrowingMutationsForExpectedSuccess_AndTryApiForExpectedRejection()
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(99),
            new MaxTotalItemAmountCapacityPolicy<string>(30),
            new EntryLayout<string>());

        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("potion");

        manager.Registry.Register(coin);
        manager.Registry.Register(potion);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var operations = new StringBuilder();

        inventory.Add(coin, amount: 25);
        operations.AppendLine("Add coin x25: committed");

        inventory.Add(potion, amount: 2);
        operations.AppendLine("Add potion x2: committed");

        inventory.RemoveByDefinition(coin, amount: 5, ignoreMetadata: true);
        operations.AppendLine("Remove coin x5: committed");

        bool overCapacityAccepted = inventory.TryAdd(coin, out var overCapacityError, amount: 20);
        operations.AppendLine($"Try add coin x20 over capacity: {(overCapacityAccepted ? "committed" : $"rejected ({overCapacityError})")}");

        Assert.That(overCapacityAccepted, Is.False);
        Assert.That(overCapacityError, Is.EqualTo("Capacity exceeded."));
        Assert.That(inventory.TotalItemCount, Is.EqualTo(22));
        Assert.That(inventory.Items.Single(i => i.Definition == coin).Amount, Is.EqualTo(20));
        Assert.That(inventory.Items.Single(i => i.Definition == potion).Amount, Is.EqualTo(2));

        var output = BuildOutput(operations.ToString(), inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Inventory mutation ergonomics example written to: {outputPath}");
    }

    private static string BuildOutput(string operations, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Inventory Mutation Ergonomics Example");
        builder.AppendLine("=====================================");
        builder.AppendLine();
        builder.AppendLine("Operations");
        builder.AppendLine("----------");
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Final Inventory");
        builder.AppendLine("---------------");

        foreach (var item in inventory.Items.OrderBy(i => i.Definition.Id))
            builder.AppendLine($"{item.Definition.Id}: {item.Amount}");

        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "InventoryMutationErgonomics");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "InventoryMutationErgonomicsExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
