using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Transactions;

[TestFixture]
[Category("Example")]
public class TransactionBuilderCommitExampleTests
{
    [Test]
    public void BuilderStagesWorkAndInventoryCommitsIt()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new SlotLayout<string>(4));

        manager.Registry.Register(apple);
        manager.Registry.Register(sword);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, out var error, 3), Is.True, error);
        Assert.That(builder.TryAdd(sword, out error), Is.True, error);

        var placement = SlotLayoutContext<string>.Map()
            .Add(0, 1)
            .Add(1, 3)
            .Build();
        var committed = inventory.TryCommitTransaction(builder, placement, out error);

        Assert.That(committed, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(3))!.Definition, Is.SameAs(sword));

        var output = BuildOutput(committed, inventory);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Transaction builder commit example written to: {outputPath}");
    }

    private static string BuildOutput(bool committed, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Transaction Builder Commit Example");
        builder.AppendLine("==================================");
        builder.AppendLine();
        builder.AppendLine("Builder staged:");
        builder.AppendLine("  apple x3");
        builder.AppendLine("  sword x1");
        builder.AppendLine();
        builder.AppendLine($"Inventory commit with mapped slot context: {(committed ? "committed" : "rejected")}");
        builder.AppendLine();
        builder.AppendLine("Slots");
        builder.AppendLine("-----");
        for (int slot = 0; slot < inventory.Layout.GetPositionCount(inventory); slot++)
        {
            var item = inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(slot));
            builder.AppendLine($"{slot}: {(item == null ? "." : item.Definition.Id + " x" + item.Amount)}");
        }

        builder.AppendLine();
        builder.AppendLine("Transfer helpers remain on InventoryTransfer for cross-inventory actions.");
        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "Transactions");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "TransactionBuilderCommitExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
