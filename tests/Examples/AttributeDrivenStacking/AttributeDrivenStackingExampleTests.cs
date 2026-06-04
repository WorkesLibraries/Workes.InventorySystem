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
public class AttributeDrivenStackingExampleTests
{
    private const string Stackable = "stackable";
    private const string MaxStack = "maxStack";

    private sealed class FullyStackConfiguredDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> FullyStackConfiguredSchema =
            ItemSchema<string>.CreateFor<FullyStackConfiguredDefinition>("example-stack-configured")
                .RequireAttribute<bool>(Stackable, inherited: true)
                .RequireAttribute<int>(MaxStack, inherited: true);

        public FullyStackConfiguredDefinition(string id, bool stackable, int maxStack)
            : base(id, FullyStackConfiguredSchema)
        {
            DefineAttribute(Stackable, stackable);
            DefineAttribute(MaxStack, maxStack);
        }
    }

    private sealed class StackabilityConfiguredDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StackabilityConfiguredSchema =
            ItemSchema<string>.CreateFor<StackabilityConfiguredDefinition>("example-stackability-configured")
                .RequireAttribute<bool>(Stackable, inherited: true);

        public StackabilityConfiguredDefinition(string id, bool stackable)
            : base(id, StackabilityConfiguredSchema)
        {
            DefineAttribute(Stackable, stackable);
        }
    }

    [Test]
    public void SameItemUniverse_CanUseConditionalAndAttributeMaxStackResolvers()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Attributes.Define<bool>(Stackable);
        catalog.Attributes.Define<int>(MaxStack);

        var coin = new FullyStackConfiguredDefinition("coin", stackable: true, maxStack: 25);
        var gem = new FullyStackConfiguredDefinition("gem", stackable: true, maxStack: 5);
        var sword = new StackabilityConfiguredDefinition("sword", stackable: false);
        var questNote = new ItemDefinition<string>("quest_note");

        catalog.Registry.Register(coin);
        catalog.Registry.Register(gem);
        catalog.Registry.Register(sword);
        catalog.Registry.Register(questNote);
        catalog.Freeze();

        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(99),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog: catalog);

        var conditionalInventory = manager.CreateInventory(
            stackResolver: new ConditionalMaxStackResolver<string>(Stackable, maxStack: 10));
        conditionalInventory.Add(coin, amount: 12);
        conditionalInventory.Add(sword, amount: 2);
        conditionalInventory.Add(questNote, amount: 2);

        var attributeMaxInventory = manager.CreateInventory(
            stackResolver: new AttributeMaxStackResolver<string>(MaxStack, missingAttributeMaxStack: 1));
        attributeMaxInventory.Add(coin, amount: 30);
        attributeMaxInventory.Add(gem, amount: 12);
        attributeMaxInventory.Add(sword, amount: 2);

        Assert.That(StackAmounts(conditionalInventory, coin), Is.EqualTo(new[] { 10, 2 }));
        Assert.That(StackAmounts(conditionalInventory, sword), Is.EqualTo(new[] { 1, 1 }));
        Assert.That(StackAmounts(conditionalInventory, questNote), Is.EqualTo(new[] { 1, 1 }));
        Assert.That(StackAmounts(attributeMaxInventory, coin), Is.EqualTo(new[] { 25, 5 }));
        Assert.That(StackAmounts(attributeMaxInventory, gem), Is.EqualTo(new[] { 5, 5, 2 }));
        Assert.That(StackAmounts(attributeMaxInventory, sword), Is.EqualTo(new[] { 1, 1 }));

        var output = BuildOutput(conditionalInventory, attributeMaxInventory, coin, gem, sword, questNote);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Attribute-driven stacking example written to: {outputPath}");
    }

    private static int[] StackAmounts(Inventory<string> inventory, ItemDefinition<string> definition)
    {
        return inventory.Find(definition).Select(item => item.Amount).ToArray();
    }

    private static string BuildOutput(
        Inventory<string> conditionalInventory,
        Inventory<string> attributeMaxInventory,
        ItemDefinition<string> coin,
        ItemDefinition<string> gem,
        ItemDefinition<string> sword,
        ItemDefinition<string> questNote)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Attribute-Driven Stacking Example");
        builder.AppendLine("=================================");
        builder.AppendLine();
        builder.AppendLine("Declared Attributes");
        builder.AppendLine("-------------------");
        builder.AppendLine("stackable: Boolean");
        builder.AppendLine("maxStack: Int32");
        builder.AppendLine();
        builder.AppendLine("Conditional Stackability Inventory");
        builder.AppendLine("----------------------------------");
        builder.AppendLine($"coin x12 -> {FormatAmounts(StackAmounts(conditionalInventory, coin))}");
        builder.AppendLine($"sword x2 -> {FormatAmounts(StackAmounts(conditionalInventory, sword))}");
        builder.AppendLine($"quest_note x2 -> {FormatAmounts(StackAmounts(conditionalInventory, questNote))}");
        builder.AppendLine();
        builder.AppendLine("Attribute Max Stack Inventory");
        builder.AppendLine("-----------------------------");
        builder.AppendLine($"coin x30 -> {FormatAmounts(StackAmounts(attributeMaxInventory, coin))}");
        builder.AppendLine($"gem x12 -> {FormatAmounts(StackAmounts(attributeMaxInventory, gem))}");
        builder.AppendLine($"sword x2 -> {FormatAmounts(StackAmounts(attributeMaxInventory, sword))}");
        builder.AppendLine();
        builder.AppendLine("Notes");
        builder.AppendLine("-----");
        builder.AppendLine("Conditional resolver uses stackable=false or missing as unstackable.");
        builder.AppendLine("Attribute max resolver uses each definition's maxStack attribute, with fallback 1 for missing values.");

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

        var path = Path.Combine(directory, "AttributeDrivenStackingExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
