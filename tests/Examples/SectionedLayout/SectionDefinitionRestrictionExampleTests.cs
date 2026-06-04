using System.IO;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.SectionedLayout;

[TestFixture]
[Category("Example")]
public class SectionDefinitionRestrictionExampleTests
{
    [Test]
    public void SectionsCanUseTagsAndDefinitions()
    {
        var tool = "gear:tool";
        var axe = new TaggedDefinition("axe", tool);
        var lockpick = new ItemDefinition<string>("lockpick");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(tool, lockpick, axe, lockpick, apple);

        var axeCommitted = inventory.TryAdd(axe, out var axeError);
        var lockpickCommitted = inventory.TryAdd(lockpick, out var lockpickError);
        var appleCommitted = inventory.TryAdd(apple, out var appleError);

        Assert.That(axeCommitted, Is.True, axeError);
        Assert.That(lockpickCommitted, Is.True, lockpickError);
        Assert.That(appleCommitted, Is.True, appleError);
        Assert.That(ItemAt(inventory, "tools", 0), Is.EqualTo("axe"));
        Assert.That(ItemAt(inventory, "tools", 1), Is.EqualTo("lockpick"));
        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("apple"));

        var output =
            "Section Definition Restrictions Example\n" +
            "=======================================\n\n" +
            "tools accepts gear:tool or lockpick\n" +
            "bag accepts anything\n\n" +
            "axe -> tools: committed\n" +
            "lockpick -> tools: committed\n" +
            "apple -> bag: committed";

        WriteOutput(output);
    }

    private static Inventory<string> CreateInventory(
        string tool,
        ItemDefinition<string> lockpick,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>());

        manager.Catalog.Tags.Define(tool);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();

        return manager.CreateInventory(layout: new Workes.InventorySystem.Layout.SectionedLayout<string>(
            new SectionDefinition<string>(
                "tools",
                2,
                new SectionDefinitionOptions<string>
                {
                    RequiredTags = new[] { tool },
                    AllowedDefinitions = new[] { lockpick }
                }),
            new SectionDefinition<string>("bag", 2)));
    }

    private static string? ItemAt(Inventory<string> inventory, string sectionId, int slotIndex)
    {
        return inventory.Layout.GetItemAt(inventory, SectionedLayoutContext<string>.Single(sectionId, slotIndex))?.Definition.Id;
    }

    private static void WriteOutput(string output)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "SectionedLayout");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "SectionDefinitionRestrictionExample.txt");
        File.WriteAllText(path, output);
        TestContext.Out.WriteLine("Section definition restriction example output: " + path);
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }
}
