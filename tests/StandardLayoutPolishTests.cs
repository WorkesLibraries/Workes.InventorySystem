using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class StandardLayoutPolishTests
{
    private const string Weight = "weight";
    private const string FootprintWidth = "footprint-width";
    private const string FootprintHeight = "footprint-height";
    private static readonly ItemSchema<string> WeightedSchema = ItemSchema<string>.Create("weighted").Require<double>(Weight);
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("footprint").Require<int>(FootprintWidth).Require<int>(FootprintHeight);

    [Test]
    public void WeightCapacityPolicy_RejectsProjectedWeightOverLimit()
    {
        var apple = new WeightedDefinition("apple", 2.5);
        var inventory = CreateInventory(new EntryLayout<string>(), new WeightCapacityPolicy<string>(Weight, 5), apple);

        Assert.That(inventory.TryAdd(apple, out var firstError, 2), Is.True, firstError?.Message);
        Assert.That(inventory.TryAdd(apple, out var failure, 1), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Capacity exceeded."));
    }

    [Test]
    public void WeightCapacityPolicy_TreatsMissingWeightAsZeroByDefault()
    {
        var feather = new ItemDefinition<string>("feather");
        var inventory = CreateInventory(new EntryLayout<string>(), new WeightCapacityPolicy<string>(Weight, 0), feather);

        Assert.That(inventory.TryAdd(feather, out var failure, 5), Is.True);
    }

    [Test]
    public void WeightCapacityPolicy_StrictMissingWeightRejectsItem()
    {
        var feather = new ItemDefinition<string>("feather");
        var inventory = CreateInventory(new EntryLayout<string>(), new WeightCapacityPolicy<string>(Weight, 10, treatMissingWeightAsZero: false), feather);

        Assert.That(inventory.TryAdd(feather, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Item weight attribute missing."));
    }

    [Test]
    public void TrySortLayout_SlotLayoutCompactsSortedItemsWithoutChangingStorageOrder()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(4), new UnlimitedCapacityPolicy<string>(), sword, apple);
        inventory.TryAdd(sword, out _, 1, SlotLayoutContext<string>.Single(2));
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(3));
        int eventCount = 0;
        inventory.Changed += (_, args) =>
        {
            eventCount++;
            Assert.That(args.Moved, Has.Count.EqualTo(2));
            Assert.That(args.AffectedLayoutContexts, Has.Count.EqualTo(4));
        };

        var result = inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var failure);

        Assert.That(result, Is.True);
        Assert.That(inventory.Items[0].Definition.Id, Is.EqualTo("sword"));
        Assert.That(inventory.Items[1].Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Definition.Id, Is.EqualTo("sword"));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void EquipmentLayout_AutoPlacesIntoFirstCompatibleTaggedSlot()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var helmet = new TaggedDefinition("helmet", armor);
        var layout = new EquipmentLayout<string>(
            new EquipmentSlot<string>("head", armor),
            new EquipmentSlot<string>("main-hand", weapon));
        var inventory = CreateInventory(layout, new UnlimitedCapacityPolicy<string>(), new[] { weapon, armor }, sword, helmet);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);

        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void EquipmentLayout_NonNamespacedMode_AcceptsItemsByRequiredTag()
    {
        var weapon = "gear.weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateNonNamespacedInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand", weapon)),
            new[] { weapon },
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void EquipmentLayout_NonNamespacedMode_AcceptsItemsByParentTag()
    {
        var sword = new TaggedDefinition("sword", "gear.weapon.sword");
        var inventory = CreateNonNamespacedInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand", "gear.weapon")),
            new[] { "gear.weapon.sword" },
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void EquipmentSlot_OptionsExposeAllowedDefinitionIds()
    {
        var sword = new ItemDefinition<string>("sword");
        var slot = new EquipmentSlot<string>(
            "main-hand",
            new EquipmentSlotOptions<string>
            {
                AllowedDefinitionIds = new[] { "sword", "sword" },
                AllowedDefinitions = new[] { sword, null! }
            });

        Assert.That(slot.AllowedDefinitionIds, Is.EqualTo(new[] { "sword" }));
    }

    [Test]
    public void EquipmentSlot_OptionsRejectNullAllowedDefinitionId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EquipmentSlot<string>(
            "main-hand",
            new EquipmentSlotOptions<string>
            {
                AllowedDefinitionIds = new[] { (string)null! }
            }));

        Assert.That(exception!.ParamName, Is.EqualTo("AllowedDefinitionIds"));
    }

    [Test]
    public void EquipmentSlot_OptionsIgnoreNullAllowedDefinition()
    {
        var slot = new EquipmentSlot<string>(
            "main-hand",
            new EquipmentSlotOptions<string>
            {
                AllowedDefinitions = new[] { (ItemDefinition<string>)null! }
            });

        Assert.That(slot.AllowedDefinitionIds, Is.Empty);
    }

    [Test]
    public void SectionDefinition_OptionsRejectNullAllowedDefinitionIdWithParameterName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SectionDefinition<string>(
            "bag",
            1,
            new SectionDefinitionOptions<string>
            {
                AllowedDefinitionIds = new[] { (string)null! }
            }));

        Assert.That(exception!.ParamName, Is.EqualTo("AllowedDefinitionIds"));
    }

    [Test]
    public void EquipmentLayout_AcceptsItemByAllowedDefinition()
    {
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new EquipmentLayout<string>(
                new EquipmentSlot<string>(
                    "main-hand",
                    new EquipmentSlotOptions<string> { AllowedDefinitionIds = new[] { "sword" } })),
            new UnlimitedCapacityPolicy<string>(),
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void EquipmentLayout_RejectsItemWhenDefinitionNotAllowed()
    {
        var sword = new ItemDefinition<string>("sword");
        var helmet = new ItemDefinition<string>("helmet");
        var inventory = CreateInventory(
            new EquipmentLayout<string>(
                new EquipmentSlot<string>(
                    "main-hand",
                    new EquipmentSlotOptions<string> { AllowedDefinitions = new[] { sword } })),
            new UnlimitedCapacityPolicy<string>(),
            sword,
            helmet);

        Assert.That(inventory.TryAdd(helmet, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible equipment slot available."));
    }

    [Test]
    public void EquipmentLayout_AllowsItemWhenEitherTagOrDefinitionMatches()
    {
        var weapon = "gear:weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var specialRing = new ItemDefinition<string>("special_ring");
        var slotOptions = new EquipmentSlotOptions<string>
        {
            RequiredTags = new[] { weapon },
            AllowedDefinitions = new[] { specialRing }
        };
        var swordInventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand", slotOptions)),
            new UnlimitedCapacityPolicy<string>(),
            new[] { weapon },
            sword,
            specialRing);
        var ringInventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand", slotOptions)),
            new UnlimitedCapacityPolicy<string>(),
            new[] { weapon },
            sword,
            specialRing);

        Assert.That(swordInventory.TryAdd(sword, out var swordError), Is.True, swordError?.Message);
        Assert.That(ringInventory.TryAdd(specialRing, out var ringError), Is.True, ringError?.Message);
    }

    [Test]
    public void EquipmentLayout_WithNoTagsOrDefinitions_AllowsAnyDefinition()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("misc")),
            new UnlimitedCapacityPolicy<string>(),
            apple);

        Assert.That(inventory.TryAdd(apple, out var failure), Is.True);
    }

    [Test]
    public void EquipmentLayout_MoveAndSwapRespectAllowedDefinitions()
    {
        var sword = new ItemDefinition<string>("sword");
        var helmet = new ItemDefinition<string>("helmet");
        var inventory = CreateInventory(
            new EquipmentLayout<string>(
                new EquipmentSlot<string>(
                    "main-hand",
                    new EquipmentSlotOptions<string> { AllowedDefinitions = new[] { sword } }),
                new EquipmentSlot<string>(
                    "head",
                    new EquipmentSlotOptions<string> { AllowedDefinitions = new[] { helmet } }),
                new EquipmentSlot<string>("scratch")),
            new UnlimitedCapacityPolicy<string>(),
            sword,
            helmet);

        inventory.Add(sword, context: EquipmentLayoutContext<string>.Single("main-hand"));
        inventory.Add(helmet, context: EquipmentLayoutContext<string>.Single("scratch"));

        Assert.That(inventory.TryMove(
            EquipmentLayoutContext<string>.Single("scratch"),
            EquipmentLayoutContext<string>.Single("head"),
            out var moveError), Is.True);
        Assert.That(inventory.TrySwap(
            EquipmentLayoutContext<string>.Single("main-hand"),
            EquipmentLayoutContext<string>.Single("head"),
            out var swapError), Is.False);
        Assert.That(swapError?.Message, Is.EqualTo("No compatible equipment slot available."));
    }

    [Test]
    public void EquipmentLayout_RejectsIncompatibleExplicitSlot()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("head", armor)),
            new UnlimitedCapacityPolicy<string>(),
            new[] { weapon, armor },
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure, 1, EquipmentLayoutContext<string>.Single("head")), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible equipment slot available."));
    }

    [Test]
    public void EquipmentLayout_MappedTransactionPlacesMultipleItems()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var helmet = new TaggedDefinition("helmet", armor);
        var inventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("head", armor), new EquipmentSlot<string>("main-hand", weapon)),
            new UnlimitedCapacityPolicy<string>(),
            new[] { weapon, armor },
            sword,
            helmet);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(sword, out _, 1);
        builder.TryAdd(helmet, out _, 1);
        var context = EquipmentLayoutContext<string>.Map().Add(0, "main-hand").Add(1, "head").Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var failure), Is.True);
        Assert.That(inventory.TryCommitTransaction(transaction!, out failure), Is.True);

        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("head"))!.Definition.Id, Is.EqualTo("helmet"));
        Assert.That(inventory.Layout.GetItemAt(inventory, EquipmentLayoutContext<string>.Single("main-hand"))!.Definition.Id, Is.EqualTo("sword"));
    }

    [Test]
    public void MultiCellGridLayout_RectangularItemOccupiesMultipleCellsAndEventsExposeAllContexts()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(3, 2, new AttributeGridFootprintProvider<string>(FootprintWidth, FootprintHeight)),
            new UnlimitedCapacityPolicy<string>(),
            table);
        IReadOnlyList<ILayoutContext<string>>? addedContexts = null;
        inventory.Changed += (_, args) => addedContexts = args.Added[0].LayoutContexts;

        Assert.That(inventory.TryAdd(table, out var failure, 1, MultiCellGridLayoutContext<string>.Single(1, 0)), Is.True);

        Assert.That(addedContexts, Is.Not.Null);
        Assert.That(addedContexts!, Has.Count.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(1, 0))!.Definition.Id, Is.EqualTo("table"));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(2, 0))!.Definition.Id, Is.EqualTo("table"));
    }

    [Test]
    public void MultiCellGridLayout_RejectsOverlappingFootprint()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var crate = new FootprintDefinition("crate", 1, 1);
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(3, 2, new AttributeGridFootprintProvider<string>(FootprintWidth, FootprintHeight)),
            new UnlimitedCapacityPolicy<string>(),
            table,
            crate);
        inventory.TryAdd(table, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));

        Assert.That(inventory.TryAdd(crate, out var failure, 1, MultiCellGridLayoutContext<string>.Single(1, 0)), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Grid cells already occupied."));
    }

    [Test]
    public void MultiCellGridLayout_SortRepacksByFootprint()
    {
        var wide = new FootprintDefinition("wide", 2, 1);
        var apple = new FootprintDefinition("apple", 1, 1);
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(3, 2, new AttributeGridFootprintProvider<string>(FootprintWidth, FootprintHeight)),
            new UnlimitedCapacityPolicy<string>(),
            wide,
            apple);
        inventory.TryAdd(wide, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 1));

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var failure), Is.True);

        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(0, 0))!.Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(1, 0))!.Definition.Id, Is.EqualTo("wide"));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(2, 0))!.Definition.Id, Is.EqualTo("wide"));
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        ICapacityPolicy<string> capacityPolicy,
        params ItemDefinition<string>[] definitions)
    {
        return CreateInventory(layout, capacityPolicy, Array.Empty<string>(), definitions);
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        ICapacityPolicy<string> capacityPolicy,
        IEnumerable<string> tags,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            capacityPolicy,
            layout,
            new ItemCatalog<string>()
            );

        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        manager.Catalog.Attributes.Define<double>(Weight);
        manager.Catalog.Attributes.Define<int>(FootprintWidth);
        manager.Catalog.Attributes.Define<int>(FootprintHeight);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static Inventory<string> CreateNonNamespacedInventory(
        IInventoryLayout<string> layout,
        IEnumerable<string> tags,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );

        manager.Catalog.Tags.UseNonNamespacedTagsOnly();
        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        manager.Catalog.Attributes.Define<double>(Weight);
        manager.Catalog.Attributes.Define<int>(FootprintWidth);
        manager.Catalog.Attributes.Define<int>(FootprintHeight);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private sealed class WeightedDefinition : ItemDefinition<string>
    {
        public WeightedDefinition(string id, double weight)
            : base(id, WeightedSchema)
        {
            DefineAttribute(Weight, weight);
        }
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }

    private sealed class FootprintDefinition : ItemDefinition<string>
    {
        public FootprintDefinition(string id, int width, int height)
            : base(id, FootprintSchema)
        {
            DefineAttribute(FootprintWidth, width);
            DefineAttribute(FootprintHeight, height);
        }
    }
}



