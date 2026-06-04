using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.AttributeDrivenStacking;

[TestFixture]
[Category("Example")]
public class MultipliedAttributeStackingExampleTests
{
    private const string StackRatio = "stackRatio";

    private sealed class RatioStackDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> RatioStackSchema =
            ItemSchema<string>.CreateFor<RatioStackDefinition>("example-ratio-stack")
                .RequireAttribute<int>(StackRatio, inherited: true);

        public RatioStackDefinition(string id, int stackRatio)
            : base(id, RatioStackSchema)
        {
            DefineAttribute(StackRatio, stackRatio);
        }
    }

    [Test]
    public void SameItemUniverse_CanScaleStacksByInventory()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Attributes.Define<int>(StackRatio);

        var coin = new RatioStackDefinition("coin", stackRatio: 10);
        var gem = new RatioStackDefinition("gem", stackRatio: 2);
        var potion = new RatioStackDefinition("potion", stackRatio: 1);

        catalog.Registry.Register(coin);
        catalog.Registry.Register(gem);
        catalog.Registry.Register(potion);
        catalog.Freeze();

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(99),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog: catalog);

        var smallPouch = manager.CreateInventory(
            stackResolver: new MultipliedAttributeStackResolver<string>(StackRatio, multiplier: 1));
        smallPouch.Add(coin, amount: 23);
        smallPouch.Add(gem, amount: 7);
        smallPouch.Add(potion, amount: 3);

        var warehouse = manager.CreateInventory(
            stackResolver: new MultipliedAttributeStackResolver<string>(StackRatio, multiplier: 5));
        warehouse.Add(coin, amount: 23);
        warehouse.Add(gem, amount: 7);
        warehouse.Add(potion, amount: 3);

        Assert.That(StackAmounts(smallPouch, coin), Is.EqualTo(new[] { 10, 10, 3 }));
        Assert.That(StackAmounts(smallPouch, gem), Is.EqualTo(new[] { 2, 2, 2, 1 }));
        Assert.That(StackAmounts(smallPouch, potion), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(StackAmounts(warehouse, coin), Is.EqualTo(new[] { 23 }));
        Assert.That(StackAmounts(warehouse, gem), Is.EqualTo(new[] { 7 }));
        Assert.That(StackAmounts(warehouse, potion), Is.EqualTo(new[] { 3 }));

        var tuned = warehouse.TrySetStackResolverParameter(
            "multiplier",
            2.0,
            InventoryParameterMutationOptions.RepackAndCompress,
            out var tuneError);

        Assert.That(tuned, Is.True, tuneError);
        Assert.That(((MultipliedAttributeStackResolver<string>)warehouse.StackResolver).Multiplier, Is.EqualTo(2));
        Assert.That(StackAmounts(warehouse, coin), Is.EqualTo(new[] { 20, 3 }));
        Assert.That(StackAmounts(warehouse, gem), Is.EqualTo(new[] { 4, 3 }));
        Assert.That(StackAmounts(warehouse, potion), Is.EqualTo(new[] { 2, 1 }));

        var output = BuildOutput(smallPouch, warehouse, coin, gem, potion);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Multiplied attribute stacking example written to: {outputPath}");
    }

    private static int[] StackAmounts(Inventory<string> inventory, ItemDefinition<string> definition)
    {
        return inventory.Find(definition).Select(item => item.Amount).ToArray();
    }

    private static string BuildOutput(
        Inventory<string> smallPouch,
        Inventory<string> warehouse,
        ItemDefinition<string> coin,
        ItemDefinition<string> gem,
        ItemDefinition<string> potion)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Multiplied Attribute Stacking Example");
        builder.AppendLine("=====================================");
        builder.AppendLine();
        builder.AppendLine("Declared Attribute");
        builder.AppendLine("------------------");
        builder.AppendLine("stackRatio: Int32");
        builder.AppendLine();
        builder.AppendLine("Small Pouch");
        builder.AppendLine("-----------");
        builder.AppendLine($"coin x23 -> {FormatAmounts(StackAmounts(smallPouch, coin))}");
        builder.AppendLine($"gem x7 -> {FormatAmounts(StackAmounts(smallPouch, gem))}");
        builder.AppendLine($"potion x3 -> {FormatAmounts(StackAmounts(smallPouch, potion))}");
        builder.AppendLine();
        builder.AppendLine("Warehouse");
        builder.AppendLine("---------");
        builder.AppendLine($"coin x23 -> {FormatAmounts(StackAmounts(warehouse, coin))}");
        builder.AppendLine($"gem x7 -> {FormatAmounts(StackAmounts(warehouse, gem))}");
        builder.AppendLine($"potion x3 -> {FormatAmounts(StackAmounts(warehouse, potion))}");
        builder.AppendLine();
        builder.AppendLine("Runtime Tuning");
        builder.AppendLine("--------------");
        builder.AppendLine("warehouse multiplier 5 -> 2: committed");
        builder.AppendLine();
        builder.AppendLine("Notes");
        builder.AppendLine("-----");
        builder.AppendLine("Definitions provide stack ratios.");
        builder.AppendLine("Inventories choose final stack sizes with the resolver multiplier.");

        return builder.ToString();
    }

    private static string FormatAmounts(int[] amounts)
    {
        return string.Join(", ", amounts);
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "AttributeDrivenStacking");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "MultipliedAttributeStackingExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
