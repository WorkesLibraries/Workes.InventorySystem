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
public class EquipmentLoadoutWorkflowExampleTests
{
    [Test]
    public void MapsLootIntoNamedEquipmentSlots()
    {
        var weapon = TagKey.Parse("gear:weapon");
        var shield = TagKey.Parse("gear:shield");
        var armor = TagKey.Parse("gear:armor");
        var trinket = TagKey.Parse("gear:trinket");

        var sword = new TaggedDefinition("iron-sword", weapon);
        var buckler = new TaggedDefinition("buckler", shield);
        var helmet = new TaggedDefinition("iron-helmet", armor);
        var charm = new TaggedDefinition("ember-charm", trinket);

        var equipment = CreateInventory(
            new Workes.InventorySystem.Layout.EquipmentLayout<string>(
                new EquipmentSlot<string>("head", armor),
                new EquipmentSlot<string>("main-hand", weapon),
                new EquipmentSlot<string>("off-hand", shield),
                new EquipmentSlot<string>("trinket", trinket)),
            new[] { weapon, shield, armor, trinket },
            sword,
            buckler,
            helmet,
            charm);

        var builder = InventoryTransaction<string>.From(equipment);
        builder.TryAdd(sword, out _);
        builder.TryAdd(buckler, out _);
        builder.TryAdd(helmet, out _);
        builder.TryAdd(charm, out _);

        var placement = EquipmentLayoutContext<string>.Map()
            .Add(0, "main-hand")
            .Add(1, "off-hand")
            .Add(2, "head")
            .Add(3, "trinket")
            .Build();

        Assert.That(builder.TryToInventoryTransaction(placement, out var transaction, out var error), Is.True, error);
        Assert.That(equipment.TryCommitTransaction(transaction!, out error), Is.True, error);

        WriteExample("EquipmentLayout", "EquipmentLoadoutWorkflowExample.txt",
            RenderSlot(equipment, "head") + "\n" +
            RenderSlot(equipment, "main-hand") + "\n" +
            RenderSlot(equipment, "off-hand") + "\n" +
            RenderSlot(equipment, "trinket"));
    }

    [Test]
    public void RejectsWrongItemTypeWithoutChangingCurrentLoadout()
    {
        var weapon = TagKey.Parse("gear:weapon");
        var armor = TagKey.Parse("gear:armor");
        var sword = new TaggedDefinition("iron-sword", weapon);
        var helmet = new TaggedDefinition("iron-helmet", armor);
        var equipment = CreateInventory(
            new Workes.InventorySystem.Layout.EquipmentLayout<string>(
                new EquipmentSlot<string>("head", armor),
                new EquipmentSlot<string>("main-hand", weapon)),
            new[] { weapon, armor },
            sword,
            helmet);

        equipment.TryAdd(sword, out _, 1, EquipmentLayoutContext<string>.Single("main-hand"));

        var accepted = equipment.TryAdd(helmet, out var error, 1, EquipmentLayoutContext<string>.Single("main-hand"));

        Assert.That(accepted, Is.False);
        WriteExample("EquipmentLayout", "EquipmentRejectedPlacementExample.txt",
            $"accepted: {accepted}\n" +
            $"error: {error}\n" +
            RenderSlot(equipment, "head") + "\n" +
            RenderSlot(equipment, "main-hand"));
    }

    private static string RenderSlot(Inventory<string> inventory, string slotId)
    {
        var item = inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single(slotId));
        return $"{slotId}: {item?.Definition.Id ?? "empty"}";
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
