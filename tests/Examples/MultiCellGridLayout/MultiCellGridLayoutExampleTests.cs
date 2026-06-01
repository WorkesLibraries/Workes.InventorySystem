using System.IO;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.MultiCellGridLayout;

[TestFixture]
[Category("Example")]
public class MultiCellGridLayoutExampleTests
{
    private static readonly AttributeKey<int> Width = new("example-footprint-width");
    private static readonly AttributeKey<int> Height = new("example-footprint-height");
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("example-footprint").Require(Width).Require(Height);

    [Test]
    public void PlacesRectangularItemsAcrossMultipleCells()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var crate = new FootprintDefinition("crate", 1, 1);
        var layout = new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(
            3,
            2,
            new AttributeGridFootprintProvider<string>(Width, Height));
        var inventory = CreateInventory(layout, table, crate);

        inventory.TryAdd(table, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(crate, out _, 1, MultiCellGridLayoutContext<string>.Single(2, 1));

        WriteExample("MultiCellGridLayout", "MultiCellGridLayoutExample.txt", Render(inventory));
    }

    private static string Render(Inventory<string> inventory)
    {
        var lines = new string[2];
        for (int y = 0; y < 2; y++)
        {
            var values = new string[3];
            for (int x = 0; x < 3; x++)
                values[x] = inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(x, y))?.Definition.Id ?? ".";
            lines[y] = string.Join(" | ", values);
        }

        return string.Join("\n", lines);
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
