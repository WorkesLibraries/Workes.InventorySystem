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
    private sealed class MaxTotalAmountCapacityPolicy : ICapacityPolicy<string>
    {
        private readonly int _maxTotalAmount;

        public MaxTotalAmountCapacityPolicy(int maxTotalAmount)
        {
            _maxTotalAmount = maxTotalAmount;
        }

        public bool CanApply(Inventory<string> inventory, NormalizedInventoryTransaction<string> normalizedTransaction, out string? error)
        {
            int added = normalizedTransaction.Added.Sum(i => i.amount);
            int removed = normalizedTransaction.Removed.Sum(i => i.amount);
            if (inventory.TotalItemCount + added - removed > _maxTotalAmount)
            {
                error = "Capacity exceeded.";
                return false;
            }

            error = null;
            return true;
        }

        public bool CanAdd(Inventory<string> inventory, ItemInstance<string> instance, out string? error)
        {
            if (inventory.TotalItemCount + instance.Amount > _maxTotalAmount)
            {
                error = "Capacity exceeded.";
                return false;
            }

            error = null;
            return true;
        }
    }

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
        var backpack = CreateManager(catalog, new MaxTotalAmountCapacityPolicy(5)).CreateInventory();
        chest.TryAdd(coin, out _, 4);
        chest.TryAdd(gem, out _, 4);

        var moved = InventoryTransfer.TryMoveMaximumByTag(chest, backpack, loot, null, out var movedAmount, out var error);

        Assert.That(moved, Is.True, error);
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
            new DefaultStackResolver<string>(10),
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
