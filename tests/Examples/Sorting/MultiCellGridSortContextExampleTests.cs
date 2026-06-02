using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Sorting;

[TestFixture]
[Category("Example")]
public class MultiCellGridSortContextExampleTests
{
    private const string Width = "sort-example-width";
    private const string Height = "sort-example-height";
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("sort-example-footprint").Require<int>(Width).Require<int>(Height);

    [Test]
    public void ComparesItemOrderSortWithCompactSort()
    {
        var small = new FootprintDefinition("small", 1, 1);
        var wide = new FootprintDefinition("wide", 2, 1);
        var tall = new FootprintDefinition("tall", 1, 2);
        var itemOrderInventory = CreateInventory(new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(3, 3, Provider()), small, wide, tall);
        var compactInventory = CreateInventory(new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(3, 3, Provider()), small, wide, tall);
        PlaceInitialItems(itemOrderInventory, small, wide, tall);
        PlaceInitialItems(compactInventory, small, wide, tall);

        itemOrderInventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out _);
        compactInventory.TrySortLayout(MultiCellGridSortContext<string>.Compact((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id)), out _);

        var builder = new StringBuilder();
        builder.AppendLine("Item-order sort");
        builder.AppendLine(Render(itemOrderInventory, 3, 3));
        builder.AppendLine();
        builder.AppendLine("Compact sort");
        builder.AppendLine(Render(compactInventory, 3, 3));
        WriteExample("Sorting", "MultiCellGridSortContextExample.txt", builder.ToString());
    }

    private static void PlaceInitialItems(
        Inventory<string> inventory,
        ItemDefinition<string> small,
        ItemDefinition<string> wide,
        ItemDefinition<string> tall)
    {
        inventory.TryAdd(small, out _, 1, MultiCellGridLayoutContext<string>.Single(2, 2));
        inventory.TryAdd(wide, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 2));
        inventory.TryAdd(tall, out _, 1, MultiCellGridLayoutContext<string>.Single(2, 0));
    }

    private static string Render(Inventory<string> inventory, int width, int height)
    {
        var lines = new string[height];
        for (int y = 0; y < height; y++)
        {
            var cells = new string[width];
            for (int x = 0; x < width; x++)
                cells[x] = inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(x, y))?.Definition.Id ?? ".";
            lines[y] = string.Join(" | ", cells);
        }

        return string.Join("\n", lines);
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
        manager.Catalog.Attributes.Define<int>(Width);
        manager.Catalog.Attributes.Define<int>(Height);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static void WriteExample(string area, string fileName, string content)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", area);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
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



