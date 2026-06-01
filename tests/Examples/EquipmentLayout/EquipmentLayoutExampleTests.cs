using System.IO;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.EquipmentLayout;

[TestFixture]
[Category("Example")]
public class EquipmentLayoutExampleTests
{
    [Test]
    public void EquipsTaggedItemsIntoNamedSlots()
    {
        var weapon = TagKey.Parse("gear:weapon");
        var armor = TagKey.Parse("gear:armor");
        var sword = new TaggedDefinition("sword", weapon);
        var helmet = new TaggedDefinition("helmet", armor);
        var layout = new Workes.InventorySystem.Layout.EquipmentLayout<string>(
            new EquipmentSlot<string>("head", armor),
            new EquipmentSlot<string>("main-hand", weapon));
        var inventory = CreateInventory(layout, new[] { weapon, armor }, sword, helmet);

        inventory.TryAdd(sword, out _);
        inventory.TryAdd(helmet, out _);

        WriteExample("EquipmentLayout", "EquipmentLayoutExample.txt",
            $"head: {inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("head"))?.Definition.Id}\n" +
            $"main-hand: {inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))?.Definition.Id}");
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, TagKey[] tags, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout);
        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Registry.Freeze();
        return manager.CreateInventory();
    }

    private static void WriteExample(string area, string fileName, string content)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", area);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params TagKey[] tags)
            : base(id, tags)
        {
        }
    }
}
