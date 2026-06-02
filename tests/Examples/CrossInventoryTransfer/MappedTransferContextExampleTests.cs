using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.CrossInventoryTransfer;

[TestFixture]
[Category("Example")]
public class MappedTransferContextExampleTests
{
    [Test]
    public void TransferBuilder_CanMapIncomingEntriesToTargetSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new SlotLayout<string>(5), apple, sword);
        var backpack = manager.CreateInventory();
        var chest = manager.CreateInventory();
        backpack.TryAdd(apple, out _, 5);
        backpack.TryAdd(sword, out _, 1);

        var transfer = InventoryTransfer.From(backpack);
        transfer.TryRemove(backpack.Items[0], 5, out _);
        transfer.TryRemove(backpack.Items[1], 1, out _);
        var context = SlotLayoutContext<string>.Map()
            .Add(0, 2)
            .Add(1, 3)
            .Build();

        var moved = InventoryTransfer.TryTransfer(transfer, chest, context, out var error);

        Assert.That(moved, Is.True, error);
        WriteOutput("MappedTransferBuilderExample.txt", Describe("Backpack", backpack, 5) + Describe("Chest", chest, 5));
    }

    [Test]
    public void SwapInventories_CanUseTransactionLevelLayoutContexts()
    {
        var torch = new ItemDefinition<string>("torch");
        var gem = new ItemDefinition<string>("gem");
        var manager = CreateManager(new SlotLayout<string>(4), torch, gem);
        var backpack = manager.CreateInventory();
        var chest = manager.CreateInventory();
        backpack.TryAdd(torch, out _, 3);
        chest.TryAdd(gem, out _, 2);

        var backpackContext = SlotLayoutContext<string>.Map().Add(0, 2).Build();
        var chestContext = SlotLayoutContext<string>.Map().Add(0, 1).Build();
        var swapped = InventoryTransfer.TrySwapInventories(backpack, chest, backpackContext, chestContext, out var error);

        Assert.That(swapped, Is.True, error);
        WriteOutput("MappedInventorySwapExample.txt", Describe("Backpack", backpack, 4) + Describe("Chest", chest, 4));
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
        manager.Catalog.Freeze();
        return manager;
    }

    private static string Describe(string title, Inventory<string> inventory, int slotCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
        for (int slot = 0; slot < slotCount; slot++)
        {
            var item = inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(slot));
            builder.Append("Slot ").Append(slot).Append(": ");
            builder.AppendLine(item == null ? "empty" : item.Definition.Id + " x" + item.Amount);
        }
        builder.AppendLine();
        return builder.ToString();
    }

    private static void WriteOutput(string fileName, string content)
    {
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "CrossInventoryTransfer", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content);
        TestContext.Out.WriteLine("Mapped transfer context example output: " + outputPath);
    }
}


