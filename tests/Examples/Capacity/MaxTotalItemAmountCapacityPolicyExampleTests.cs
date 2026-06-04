using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Capacity;

[TestFixture]
[Category("Example")]
public class MaxTotalItemAmountCapacityPolicyExampleTests
{
    [Test]
    public void BackpackCapacity_RejectsItemsOverTotalAmountLimit()
    {
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new MaxTotalItemAmountCapacityPolicy<string>(5),
            new EntryLayout<string>());
        manager.Registry.Register(apple);
        manager.Registry.Register(potion);
        manager.Catalog.Freeze();
        var backpack = manager.CreateInventory();

        var addedApples = backpack.TryAdd(apple, out var appleError, 3);
        var addedPotions = backpack.TryAdd(potion, out var potionError, 2);
        var addedExtraPotion = backpack.TryAdd(potion, out var extraPotionError, 1);

        Assert.That(addedApples, Is.True, appleError);
        Assert.That(addedPotions, Is.True, potionError);
        Assert.That(addedExtraPotion, Is.False);
        Assert.That(extraPotionError, Is.EqualTo("Capacity exceeded."));
        Assert.That(backpack.TotalItemCount, Is.EqualTo(5));

        var outputPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "Capacity",
            "MaxTotalItemAmountCapacityPolicyExample.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, Describe(backpack, extraPotionError));
        TestContext.Out.WriteLine("Max total item amount capacity example output: " + outputPath);
    }

    private static string Describe(Inventory<string> inventory, string? rejection)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Backpack capacity");
        builder.AppendLine("-----------------");
        builder.AppendLine("Maximum total item amount: 5");
        builder.AppendLine("Current total item amount: " + inventory.TotalItemCount);
        builder.AppendLine();
        foreach (var item in inventory.Items.OrderBy(i => i.Definition.Id))
            builder.AppendLine(item.Definition.Id + " x" + item.Amount);
        builder.AppendLine();
        builder.AppendLine("Extra potion: rejected - " + rejection);
        return builder.ToString();
    }
}


