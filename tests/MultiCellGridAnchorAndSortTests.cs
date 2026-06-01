using System;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class MultiCellGridAnchorAndSortTests
{
    private static readonly AttributeKey<int> Width = new("anchor-width");
    private static readonly AttributeKey<int> Height = new("anchor-height");
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("anchor-footprint").Require(Width).Require(Height);

    [Test]
    public void MultiCellGridLayout_ContextTopRightPlacesFootprintToLeftOfCoordinate()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(4, 2, Provider()), table);

        Assert.That(inventory.TryAdd(table, out var error, 1, MultiCellGridLayoutContext<string>.Single(2, 0, GridAnchor.TopRight)), Is.True, error);

        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 2, 0), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 0, 0), Is.Null);
    }

    [Test]
    public void MultiCellGridLayout_ContextBottomRightPlacesFootprintUpAndLeftOfCoordinate()
    {
        var chest = new FootprintDefinition("chest", 2, 2);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(4, 4, Provider()), chest);

        Assert.That(inventory.TryAdd(chest, out var error, 1, MultiCellGridLayoutContext<string>.Single(2, 2, GridAnchor.BottomRight)), Is.True, error);

        Assert.That(Cell(inventory, 1, 1), Is.EqualTo("chest"));
        Assert.That(Cell(inventory, 2, 2), Is.EqualTo("chest"));
        Assert.That(Cell(inventory, 0, 0), Is.Null);
    }

    [Test]
    public void MultiCellGridLayout_LayoutDefaultAnchorAppliesWhenContextOmitsAnchor()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(4, 2, Provider(), defaultAnchor: GridAnchor.TopRight), table);

        Assert.That(inventory.TryAdd(table, out var error, 1, MultiCellGridLayoutContext<string>.Single(2, 0)), Is.True, error);

        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 2, 0), Is.EqualTo("table"));
    }

    [Test]
    public void MultiCellGridLayout_ContextAnchorOverridesLayoutDefaultAnchor()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(4, 2, Provider(), defaultAnchor: GridAnchor.TopRight), table);

        Assert.That(inventory.TryAdd(table, out var error, 1, MultiCellGridLayoutContext<string>.Single(0, 0, GridAnchor.TopLeft)), Is.True, error);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("table"));
    }

    [Test]
    public void MultiCellGridLayout_MappedContextSupportsPerEntryAnchors()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var chest = new FootprintDefinition("chest", 2, 2);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(5, 3, Provider()), table, chest);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(table, out _);
        builder.TryAdd(chest, out _);
        var context = MultiCellGridLayoutContext<string>.Map()
            .Add(0, 2, 0, GridAnchor.TopRight)
            .Add(1, 4, 2, GridAnchor.BottomRight)
            .Build();

        Assert.That(builder.TryToInventoryTransaction(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("table"));
        Assert.That(Cell(inventory, 3, 1), Is.EqualTo("chest"));
        Assert.That(Cell(inventory, 4, 2), Is.EqualTo("chest"));
    }

    [Test]
    public void TrySortLayout_NullSortContextRejected()
    {
        var apple = new FootprintDefinition("apple", 1, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(2, 2, Provider()), apple);

        Assert.That(inventory.TrySortLayout((IInventorySortContext<string>)null!, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Sort context cannot be null."));
    }

    [Test]
    public void MultiCellGridLayout_CompactSortPlacesLargerFootprintsFirst()
    {
        var small = new FootprintDefinition("small", 1, 1);
        var large = new FootprintDefinition("large", 2, 2);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(3, 3, Provider()), small, large);
        inventory.TryAdd(small, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(large, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 1));

        Assert.That(inventory.TrySortLayout(MultiCellGridSortContext<string>.Compact(), out var error), Is.True, error);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("large"));
        Assert.That(Cell(inventory, 1, 1), Is.EqualTo("large"));
        Assert.That(Cell(inventory, 2, 0), Is.EqualTo("small"));
    }

    [Test]
    public void MultiCellGridLayout_CompactSortUsesComparerAsTieBreakerForEqualFootprints()
    {
        var sword = new FootprintDefinition("sword", 1, 1);
        var apple = new FootprintDefinition("apple", 1, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(2, 2, Provider()), sword, apple);
        inventory.TryAdd(sword, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));

        Assert.That(inventory.TrySortLayout(MultiCellGridSortContext<string>.Compact((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id)), out var error), Is.True, error);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("apple"));
        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("sword"));
    }

    [Test]
    public void MultiCellGridLayout_ItemSortContextSortsByComparerThenRepacks()
    {
        var sword = new FootprintDefinition("sword", 1, 1);
        var apple = new FootprintDefinition("apple", 1, 1);
        var inventory = CreateInventory(new MultiCellGridLayout<string>(2, 2, Provider()), sword, apple);
        inventory.TryAdd(sword, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 1));
        inventory.TryAdd(apple, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 1));

        Assert.That(inventory.TrySortLayout(ItemSortContext<string>.FromComparison((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id)), out var error), Is.True, error);

        Assert.That(Cell(inventory, 0, 0), Is.EqualTo("apple"));
        Assert.That(Cell(inventory, 1, 0), Is.EqualTo("sword"));
    }

    private static string? Cell(Inventory<string> inventory, int x, int y)
    {
        return inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(x, y))?.Definition.Id;
    }

    private static AttributeGridFootprintProvider<string> Provider()
    {
        return new AttributeGridFootprintProvider<string>(Width, Height);
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Registry.Freeze();
        return manager.CreateInventory();
    }

    private sealed class FootprintDefinition : ItemDefinition<string>
    {
        public FootprintDefinition(string id, int width, int height)
            : base(id, FootprintSchema)
        {
            DefineAttribute(Width, width);
            DefineAttribute(Height, height);
        }
    }
}
