using System;
using System.Collections.Generic;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class FunctionalCompletionRegressionTests
{
    [Test]
    public void MultiCellGridLayout_FailedCompactSortIsAtomic()
    {
        var apple = new ItemDefinition<string>("apple");
        var crate = new ItemDefinition<string>("crate");
        var provider = new MutableFootprintProvider();
        var inventory = CreateInventory(new MultiCellGridLayout<string>(2, 2, provider), apple, crate);
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(crate, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));
        provider.Footprints["crate"] = new GridFootprint(3, 3);

        Assert.That(inventory.TrySortLayout(MultiCellGridSortContext<string>.Compact(), out var error), Is.False);
        Assert.That(error?.Message, Is.EqualTo("Not enough empty grid space for sorted layout."));
        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("apple"));
        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("crate"));
    }

    [Test]
    public void MultiCellGridLayout_FailedSortFiresNoChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var crate = new ItemDefinition<string>("crate");
        var provider = new MutableFootprintProvider();
        var inventory = CreateInventory(new MultiCellGridLayout<string>(2, 2, provider), apple, crate);
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(crate, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));
        provider.Footprints["crate"] = new GridFootprint(3, 3);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TrySortLayout(MultiCellGridSortContext<string>.Compact(), out _), Is.False);

        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void MultiCellGridLayout_TransferWithMappedAnchorsPlacesIncomingEntries()
    {
        var table = new ItemDefinition<string>("table");
        var chest = new ItemDefinition<string>("chest");
        var provider = new MutableFootprintProvider();
        provider.Footprints["table"] = new GridFootprint(2, 1);
        provider.Footprints["chest"] = new GridFootprint(2, 2);
        var manager = CreateManager(provider, table, chest);
        var source = manager.CreateInventory(layout: new EntryLayout<string>());
        var target = manager.CreateInventory(layout: new MultiCellGridLayout<string>(5, 3, provider));
        source.TryAdd(table, out _);
        source.TryAdd(chest, out _);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemoveByDefinition(table, 1, ignoreMetadata: true, out _);
        transfer.TryRemoveByDefinition(chest, 1, ignoreMetadata: true, out _);
        var context = MultiCellGridLayoutContext<string>.Map()
            .Add(0, 2, 0, GridAnchor.TopRight)
            .Add(1, 4, 2, GridAnchor.BottomRight)
            .Build();

        Assert.That(transfer.Source.TryCommitTransfer(transfer, target, context, out var error), Is.True);

        Assert.That(Cell(target, 1, 0), Is.EqualTo("table"));
        Assert.That(Cell(target, 2, 0), Is.EqualTo("table"));
        Assert.That(Cell(target, 3, 1), Is.EqualTo("chest"));
        Assert.That(Cell(target, 4, 2), Is.EqualTo("chest"));
    }

    [Test]
    public void MultiCellGridLayout_BottomLeftAnchorPlacesFootprintAboveCoordinate()
    {
        var table = new ItemDefinition<string>("table");
        var provider = new MutableFootprintProvider();
        provider.Footprints["table"] = new GridFootprint(2, 2);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(4, 4, provider), table);

        Assert.That(inventory.TryAdd(table, out var error, 1, MultiCellGridLayoutContext<string>.Single(1, 2, GridAnchor.BottomLeft)), Is.True);

        Assert.That(Cell(inventory, 1, 1), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 2, 2), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 0, 2), Is.Null);
    }

    [Test]
    public void MultiCellGridLayout_PersistencePreservesDefaultAnchor()
    {
        var provider = new MutableFootprintProvider();
        var layout = new MultiCellGridLayout<string>(2, 2, provider, defaultAnchor: GridAnchor.BottomRight);

        var data = (MultiCellGridLayoutPersistentData)layout.GetPersistentData();

        Assert.That(data.DefaultAnchor, Is.EqualTo(GridAnchor.BottomRight));
        Assert.Throws<InvalidOperationException>(() =>
            new MultiCellGridLayout<string>(2, 2, provider, defaultAnchor: GridAnchor.TopLeft).RestorePersistentData(data));
    }

    [Test]
    public void MultiCellGridLayout_ClonePreservesDefaultAnchor()
    {
        var provider = new MutableFootprintProvider();
        var layout = new MultiCellGridLayout<string>(2, 2, provider, defaultAnchor: GridAnchor.BottomRight);

        var clone = (MultiCellGridLayout<string>)layout.Clone();

        Assert.That(clone.DefaultAnchor, Is.EqualTo(GridAnchor.BottomRight));
    }

    [Test]
    public void EquipmentLayout_TrySortReturnsUnsupported()
    {
        var weapon = "gear:weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateInventory(
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand", weapon)),
            new[] { weapon },
            sword);
        inventory.TryAdd(sword, out _);

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error), Is.False);
        Assert.That(error?.Message, Is.EqualTo("Layout does not support sorting."));
    }

    [Test]
    public void MultiCellGridLayout_ColumnMajorAutoPlacement_UsesColumnMajorAnchorScan()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(2, 2, new MutableFootprintProvider(), GridPlacementOrder.ColumnMajor),
            apple,
            sword);

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(sword, out _);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("apple"));
        Assert.That(Cell(inventory, 0, 1), Is.EqualTo("sword"));
        Assert.That(Cell(inventory, 1, 0), Is.Null);
    }

    [Test]
    public void MultiCellGridLayout_ColumnMajorSortRepack_UsesColumnMajorAnchorScan()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(2, 2, new MutableFootprintProvider(), GridPlacementOrder.ColumnMajor),
            sword,
            apple);
        inventory.TryAdd(sword, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 1));
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error), Is.True);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("apple"));
        Assert.That(Cell(inventory, 0, 1), Is.EqualTo("sword"));
        Assert.That(Cell(inventory, 1, 0), Is.Null);
    }

    private static string? Cell(Inventory<string> inventory, int x, int y)
    {
        return inventory.GetItemAt(MultiCellGridLayoutContext<string>.Single(x, y))?.Definition.Id;
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        params ItemDefinition<string>[] definitions)
    {
        return CreateInventory(layout, Array.Empty<string>(), definitions);
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

    private static InventoryManager<string> CreateManager(
        IGridFootprintProvider<string> provider,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new MultiCellGridLayout<string>(5, 5, provider),
            new ItemCatalog<string>()
            );
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    private sealed class MutableFootprintProvider : IGridFootprintProvider<string>
    {
        public Dictionary<string, GridFootprint> Footprints { get; } = new();

        public GridFootprint GetFootprint(ItemDefinition<string> definition)
        {
            return Footprints.TryGetValue(definition.Id, out var footprint)
                ? footprint
                : new GridFootprint(1, 1);
        }
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }
}


