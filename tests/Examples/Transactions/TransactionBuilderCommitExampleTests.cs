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
    public void BuilderStagesWorkAndCommitsIt()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new SlotLayout<string>(4),
            new ItemCatalog<string>()
            );

        manager.Registry.Register(apple);
        manager.Registry.Register(sword);
        manager.Catalog.Freeze();

        var inventory = manager.CreateInventory();
        var builder = InventoryTransaction<string>.For(inventory);
        Assert.That(builder.TryAdd(apple, out var failure, 3, SlotLayoutContext<string>.Single(1)), Is.True);
        Assert.That(builder.TryAdd(sword, out failure, context: SlotLayoutContext<string>.Single(3)), Is.True);

        var committed = builder.TryBuild(null, out var transaction, out failure)
            && transaction!.TryCommit(out failure);

        Assert.That(committed, Is.True);
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Definition, Is.SameAs(apple));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(3))!.Definition, Is.SameAs(sword));

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
        builder.AppendLine($"Transaction commit with direct slot contexts: {(committed ? "committed" : "rejected")}");
        builder.AppendLine();
        builder.AppendLine("Slots");
        builder.AppendLine("-----");
        for (int slot = 0; slot < inventory.GetLayoutPositionCount(); slot++)
        {
            var item = inventory.GetItemAt(SlotLayoutContext<string>.Single(slot));
            builder.AppendLine($"{slot}: {(item == null ? "." : item.Definition.Id + " x" + item.Amount)}");
        }

        builder.AppendLine();
        builder.AppendLine("Use cross-inventory transactions for planned multi-inventory actions.");
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
