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
public class TransferBuilderExampleTests
{
    [Test]
    public void TransferBuilder_MovesSelectedCraftingIngredients()
    {
        var catalog = new ItemCatalog<string>();
        var herbTag = catalog.Tags.Define("crafting:ingredient.herb");
        var bottleTag = catalog.Tags.Define("crafting:container.bottle");
        var herb = new ItemDefinition<string>("moon_herb", herbTag);
        var bottle = new ItemDefinition<string>("glass_bottle", bottleTag);
        var coin = new ItemDefinition<string>("coin");
        catalog.Registry.Register(herb);
        catalog.Registry.Register(bottle);
        catalog.Registry.Register(coin);
        catalog.Freeze();

        var backpack = CreateManager(catalog).CreateInventory();
        var craftingInput = CreateManager(catalog).CreateInventory();
        backpack.TryAdd(herb, out _, 4);
        backpack.TryAdd(bottle, out _, 2);
        backpack.TryAdd(coin, out _, 12);

        var builder = InventoryTransfer.From(backpack);
        builder.TryRemove(backpack.Find(herb).Single(), 3, out _);
        builder.TryRemove(backpack.Find(bottle).Single(), 1, out _);

        var moved = backpack.TryCommitTransfer(builder, craftingInput, targetContext: null, out var failure);

        Assert.That(moved, Is.True);
        Assert.That(backpack.Count(herb), Is.EqualTo(1));
        Assert.That(backpack.Count(bottle), Is.EqualTo(1));
        Assert.That(backpack.Count(coin), Is.EqualTo(12));
        Assert.That(craftingInput.Count(herb), Is.EqualTo(3));
        Assert.That(craftingInput.Count(bottle), Is.EqualTo(1));

        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "CrossInventoryTransfer", "TransferBuilderExample.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, BuildOutput(moved, backpack, craftingInput));
        TestContext.Out.WriteLine("Transfer builder example output: " + outputPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
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

    private static string BuildOutput(bool committed, Inventory<string> backpack, Inventory<string> craftingInput)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Transfer Builder Example");
        builder.AppendLine("========================");
        builder.AppendLine();
        builder.AppendLine("Builder staged:");
        builder.AppendLine("  moon_herb x3");
        builder.AppendLine("  glass_bottle x1");
        builder.AppendLine();
        builder.AppendLine("Transfer builder commit: " + (committed ? "committed" : "rejected"));
        builder.AppendLine();
        builder.Append(Describe("Backpack", backpack));
        builder.Append(Describe("Crafting Input", craftingInput));
        return builder.ToString();
    }
}


