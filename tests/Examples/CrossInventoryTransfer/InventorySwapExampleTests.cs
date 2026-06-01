using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.CrossInventoryTransfer;

[TestFixture]
[Category("Example")]
public class InventorySwapExampleTests
{
    [Test]
    public void PlayerBackpack_CanSwapAllContentsWithChest()
    {
        var catalog = new ItemCatalog<string>();
        var torch = new ItemDefinition<string>("torch");
        var rope = new ItemDefinition<string>("rope");
        var ore = new ItemDefinition<string>("iron_ore");
        var gem = new ItemDefinition<string>("ruby");
        catalog.Registry.Register(torch);
        catalog.Registry.Register(rope);
        catalog.Registry.Register(ore);
        catalog.Registry.Register(gem);
        catalog.Freeze();

        var backpack = CreateManager(catalog).CreateInventory();
        var chest = CreateManager(catalog).CreateInventory();
        backpack.TryAdd(torch, out _, 3);
        backpack.TryAdd(rope, out _, 1);
        chest.TryAdd(ore, out _, 8);
        chest.TryAdd(gem, out _, 2);

        var swapped = InventoryTransfer.TrySwapInventories(
            backpack,
            chest,
            firstTargetContext: null,
            secondTargetContext: null,
            out var error);

        Assert.That(swapped, Is.True, error);
        Assert.That(backpack.Count(ore), Is.EqualTo(8));
        Assert.That(backpack.Count(gem), Is.EqualTo(2));
        Assert.That(chest.Count(torch), Is.EqualTo(3));
        Assert.That(chest.Count(rope), Is.EqualTo(1));

        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "CrossInventoryTransfer", "InventorySwapExample.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, Describe("Backpack After Swap", backpack) + Describe("Chest After Swap", chest));
        TestContext.Out.WriteLine("Inventory swap example output: " + outputPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog: catalog);
    }

    private static string Describe(string title, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
        foreach (var item in inventory.Items.OrderBy(i => i.Definition.Id))
            builder.AppendLine(item.Definition.Id + " x" + item.Amount);
        builder.AppendLine();
        return builder.ToString();
    }
}
