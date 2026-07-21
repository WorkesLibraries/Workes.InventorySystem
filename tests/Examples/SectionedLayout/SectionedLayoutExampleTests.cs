using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.SectionedLayout;

[TestFixture]
[Category("Example")]
public class SectionedLayoutExampleTests
{
    [Test]
    public void SectionedBackpack_WritesReadableExample()
    {
        var consumable = "item:consumable";
        var tool = "item:tool";
        var apple = new TaggedDefinition("apple", consumable);
        var rope = new TaggedDefinition("rope", tool);
        var coin = new ItemDefinition<string>("coin");
        var layout = new Workes.InventorySystem.Layout.SectionedLayout<string>(
            new SectionDefinition<string>("hotbar", 2),
            new SectionDefinition<string>("tools", 2, tool),
            new SectionDefinition<string>("bag", 3));
        var inventory = CreateInventory(layout, new[] { consumable, tool }, apple, rope, coin);

        Assert.That(inventory.TryAdd(apple, out var error, 4, SectionedLayoutContext<string>.Single("hotbar", 0)), Is.True);
        Assert.That(inventory.TryAdd(rope, out error), Is.True);
        Assert.That(inventory.TryAdd(coin, out error, 8, SectionedLayoutContext<string>.Single("bag", 1)), Is.True);

        WriteOutput("SectionedBackpack.txt", DescribeSections(inventory));
    }

    [Test]
    public void MappedSectionedTransaction_WritesReadableExample()
    {
        var potion = new ItemDefinition<string>("potion");
        var torch = new ItemDefinition<string>("torch");
        var inventory = CreateInventory(
            new Workes.InventorySystem.Layout.SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 2),
                new SectionDefinition<string>("bag", 2)),
            System.Array.Empty<string>(),
            potion,
            torch);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(potion, out _, 3);
        builder.TryAdd(torch, out _, 2);
        var context = SectionedLayoutContext<string>.Map()
            .Add(0, "hotbar", 1)
            .Add(1, "bag", 0)
            .Build();

        var built = builder.TryBuild(context, out var transaction, out var error);
        var committed = built && inventory.TryCommitTransaction(transaction!, out error);

        Assert.That(committed, Is.True);
        WriteOutput("MappedSectionedTransaction.txt", DescribeSections(inventory));
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        string[] tags,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );

        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static string DescribeSections(Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Sectioned inventory");
        builder.AppendLine("-------------------");

        foreach (var context in inventory.GetAddressableLayoutContexts())
        {
            var sectionContext = (SectionedLayoutContext<string>)context;
            var item = inventory.GetItemAt(sectionContext);
            builder.Append(sectionContext.SectionId);
            builder.Append('[');
            builder.Append(sectionContext.SlotIndex);
            builder.Append("]: ");
            builder.AppendLine(item == null ? "empty" : item.Definition.Id + " x" + item.Amount);
        }

        return builder.ToString();
    }

    private static void WriteOutput(string fileName, string content)
    {
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "SectionedLayout", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content);
        TestContext.Out.WriteLine("Sectioned layout example output: " + outputPath);
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }
}


