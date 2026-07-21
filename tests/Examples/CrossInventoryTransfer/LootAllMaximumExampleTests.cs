using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.CrossInventoryTransfer;

[TestFixture]
[Category("Example")]
public class LootAllMaximumExampleTests
{
    [Test]
    public void ChestLoot_MovesAsMuchAsLimitedBackpackCanAccept()
    {
        var catalog = new ItemCatalog<string>();
        var loot = catalog.Tags.Define("loot:treasure");
        var coin = new ItemDefinition<string>("coin", loot);
        var gem = new ItemDefinition<string>("gem", loot);
        catalog.Registry.Register(coin);
        catalog.Registry.Register(gem);
        catalog.Freeze();

        var chest = CreateManager(catalog).CreateInventory();
        var backpack = CreateManager(catalog, new MaxTotalItemAmountCapacityPolicy<string>(5)).CreateInventory();
        chest.TryAdd(coin, out _, 4);
        chest.TryAdd(gem, out _, 4);

        var moved = chest.TryMoveMaximumByTagTo(backpack, loot, null, out var movedAmount, out var error);

        Assert.That(moved, Is.True);
        Assert.That(movedAmount, Is.EqualTo(5));
        Assert.That(backpack.TotalItemCount, Is.EqualTo(5));
        Assert.That(chest.TotalItemCount, Is.EqualTo(3));

        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "CrossInventoryTransfer", "LootAllMaximumExample.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, "Moved amount: " + movedAmount + "\n\n" + Describe("Backpack", backpack) + Describe("Chest", chest));
        TestContext.Out.WriteLine("Loot all maximum example output: " + outputPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog, ICapacityPolicy<string>? capacityPolicy = null)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            capacityPolicy ?? new UnlimitedCapacityPolicy<string>(),
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


