using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.LayoutContexts;

[TestFixture]
[Category("Example")]
public class LayoutContextExampleTests
{
    [Test]
    public void ManualSingleSlotPlacement_WritesReadableExample()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new SlotLayout<string>(5), apple).CreateInventory();

        var placed = inventory.TryAdd(apple, out var error, 5, SlotLayoutContext<string>.Single(3));

        Assert.That(placed, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(3))!.Amount, Is.EqualTo(5));
        WriteOutput("ManualSingleSlotPlacement.txt", DescribeSlots(inventory, 5));
    }

    [Test]
    public void MultiAddTransaction_CanMapAddedEntriesToSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new SlotLayout<string>(5), apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 5);
        builder.TryAdd(sword, out _, 2);

        var context = SlotLayoutContext<string>.Map()
            .Add(0, 3)
            .Add(1, 4)
            .Build();
        var built = builder.TryToInventoryTransaction(context, out var transaction, out var error);
        var committed = built && inventory.TryCommitTransaction(transaction!, out error);

        Assert.That(committed, Is.True, error);
        WriteOutput("MappedMultiAddTransaction.txt", DescribeSlots(inventory, 5));
    }

    private static InventoryManager<string> CreateManager(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new RuleContainer<string>());

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Registry.Freeze();
        return manager;
    }

    private static string DescribeSlots(Inventory<string> inventory, int slotCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Slots");
        builder.AppendLine("-----");
        for (int slot = 0; slot < slotCount; slot++)
        {
            var item = inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(slot));
            builder.Append("Slot ").Append(slot).Append(": ");
            builder.AppendLine(item == null ? "empty" : item.Definition.Id + " x" + item.Amount);
        }
        return builder.ToString();
    }

    private static void WriteOutput(string fileName, string content)
    {
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "LayoutContexts", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content);
        TestContext.Out.WriteLine("Layout context example output: " + outputPath);
    }
}
