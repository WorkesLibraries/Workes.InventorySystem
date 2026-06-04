using System.IO;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.EquipmentLayout;

[TestFixture]
[Category("Example")]
public class EquipmentDefinitionRestrictionExampleTests
{
    [Test]
    public void EquipmentSlotsCanUseTagsAndDefinitions()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("iron_sword", weapon);
        var familyHeirloom = new ItemDefinition<string>("family_heirloom");
        var helmet = new TaggedDefinition("helmet", armor);

        var swordInventory = CreateInventory(weapon, armor, familyHeirloom, sword, helmet);
        var heirloomInventory = CreateInventory(weapon, armor, familyHeirloom, sword, helmet);
        var helmetInventory = CreateInventory(weapon, armor, familyHeirloom, sword, helmet);

        var swordCommitted = swordInventory.TryAdd(sword, out var swordError, context: EquipmentLayoutContext<string>.Single("main-hand"));
        var heirloomCommitted = heirloomInventory.TryAdd(familyHeirloom, out var heirloomError, context: EquipmentLayoutContext<string>.Single("main-hand"));
        var helmetCommitted = helmetInventory.TryAdd(helmet, out var helmetError, context: EquipmentLayoutContext<string>.Single("main-hand"));

        Assert.That(swordCommitted, Is.True, swordError);
        Assert.That(heirloomCommitted, Is.True, heirloomError);
        Assert.That(helmetCommitted, Is.False);
        Assert.That(helmetError, Is.EqualTo("No compatible equipment slot available."));

        var output =
            "Equipment Definition Restrictions Example\n" +
            "=========================================\n\n" +
            "main-hand accepts gear:weapon or family_heirloom\n" +
            "head accepts gear:armor\n\n" +
            "iron_sword -> main-hand: committed\n" +
            "family_heirloom -> main-hand: committed\n" +
            $"helmet -> main-hand: rejected ({helmetError})";

        WriteOutput(output);
    }

    private static Inventory<string> CreateInventory(
        string weapon,
        string armor,
        ItemDefinition<string> familyHeirloom,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>());

        manager.Catalog.Tags.Define(weapon);
        manager.Catalog.Tags.Define(armor);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();

        return manager.CreateInventory(layout: new Workes.InventorySystem.Layout.EquipmentLayout<string>(
            new EquipmentSlot<string>(
                "main-hand",
                new EquipmentSlotOptions<string>
                {
                    RequiredTags = new[] { weapon },
                    AllowedDefinitions = new[] { familyHeirloom }
                }),
            new EquipmentSlot<string>("head", armor)));
    }

    private static void WriteOutput(string output)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "EquipmentLayout");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "EquipmentDefinitionRestrictionExample.txt");
        File.WriteAllText(path, output);
        TestContext.Out.WriteLine("Equipment definition restriction example output: " + path);
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }
}
