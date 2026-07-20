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
        var weapon = "gear:weapon";
        var shield = "gear:shield";
        var armor = "gear:armor";
        var trinket = "gear:trinket";
        var sword = new TaggedDefinition("iron_sword", weapon);
        var kiteShield = new TaggedDefinition("kite_shield", shield);
        var helmet = new TaggedDefinition("iron_helmet", armor);
        var charm = new TaggedDefinition("lucky_charm", trinket);
        var manager = CreateManager(
            new[] { weapon, shield, armor, trinket },
            sword,
            kiteShield,
            helmet,
            charm);
        var lootBag = manager.CreateInventory(layout: new EntryLayout<string>());
        var equipment = manager.CreateInventory(layout: CreateEquipmentLayout(weapon, shield, armor, trinket));
        lootBag.TryAdd(sword, out _);
        lootBag.TryAdd(kiteShield, out _);
        lootBag.TryAdd(helmet, out _);
        lootBag.TryAdd(charm, out _);
        var transfer = InventoryTransfer.From(lootBag);
        transfer.TryRemoveByDefinition(sword, 1, ignoreMetadata: true, out _);
        transfer.TryRemoveByDefinition(kiteShield, 1, ignoreMetadata: true, out _);
        transfer.TryRemoveByDefinition(helmet, 1, ignoreMetadata: true, out _);
        transfer.TryRemoveByDefinition(charm, 1, ignoreMetadata: true, out _);
        var placement = EquipmentLayoutContext<string>.Map()
            .Add(0, "main-hand")
            .Add(1, "off-hand")
            .Add(2, "head")
            .Add(3, "trinket")
            .Build();

        Assert.That(lootBag.TryCommitTransfer(transfer, equipment, placement, out var error), Is.True, error);

        WriteExample("EquipmentLayout", "EquipmentLoadoutWorkflowExample.txt", RenderLoadout(equipment));
    }

    [Test]
    public void RejectsWrongItemTypeWithoutChangingCurrentLoadout()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("iron_sword", weapon);
        var helmet = new TaggedDefinition("iron_helmet", armor);
        var manager = CreateManager(new[] { weapon, armor }, sword, helmet);
        var equipment = manager.CreateInventory(layout: new Workes.InventorySystem.Layout.EquipmentLayout<string>(
            new EquipmentSlot<string>("head", armor),
            new EquipmentSlot<string>("main-hand", weapon)));
        equipment.TryAdd(sword, out _, 1, EquipmentLayoutContext<string>.Single("main-hand"));
        var before = RenderSlot(equipment, "head") + "\n" + RenderSlot(equipment, "main-hand");

        var accepted = equipment.TryAdd(helmet, out var error, 1, EquipmentLayoutContext<string>.Single("main-hand"));

        Assert.That(accepted, Is.False);
        Assert.That(RenderSlot(equipment, "head") + "\n" + RenderSlot(equipment, "main-hand"), Is.EqualTo(before));
        WriteExample("EquipmentLayout", "EquipmentRejectedPlacementExample.txt",
            $"attempt: iron_helmet -> main-hand\n" +
            $"accepted: {accepted}\n" +
            $"error: {error}\n\n" +
            "unchanged loadout\n" +
            before);
    }

    private static Workes.InventorySystem.Layout.EquipmentLayout<string> CreateEquipmentLayout(
        string weapon,
        string shield,
        string armor,
        string trinket)
    {
        return new Workes.InventorySystem.Layout.EquipmentLayout<string>(
            new EquipmentSlot<string>("head", armor),
            new EquipmentSlot<string>("main-hand", weapon),
            new EquipmentSlot<string>("off-hand", shield),
            new EquipmentSlot<string>("trinket", trinket));
    }

    private static string RenderLoadout(Inventory<string> inventory)
    {
        return RenderSlot(inventory, "head") + "\n" +
               RenderSlot(inventory, "main-hand") + "\n" +
               RenderSlot(inventory, "off-hand") + "\n" +
               RenderSlot(inventory, "trinket");
    }

    private static string RenderSlot(Inventory<string> inventory, string slotId)
    {
        var item = inventory.GetItemAt(EquipmentLayoutContext<string>.Single(slotId));
        return $"{slotId}: {item?.Definition.Id ?? "empty"}";
    }

    private static InventoryManager<string> CreateManager(string[] tags, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );
        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    private static void WriteExample(string area, string fileName, string content)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", area);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }
}


