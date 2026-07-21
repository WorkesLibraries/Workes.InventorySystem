using System.IO;
using System.Linq;
using System.Text;
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
    private const string Width = "example-footprint-width";
    private const string Height = "example-footprint-height";
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("example-footprint").Require<int>(Width).Require<int>(Height);

    [Test]
    public void PlacesRectangularItemsAndRejectsOverlaps()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var chest = new FootprintDefinition("chest", 2, 2);
        var crate = new FootprintDefinition("crate", 1, 1);
        var layout = new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(
            4,
            3,
            new AttributeGridFootprintProvider<string>(Width, Height));
        var inventory = CreateInventory(layout, table, chest, crate);

        Assert.That(inventory.TryAdd(table, out var failure, 1, MultiCellGridLayoutContext<string>.Single(0, 0)), Is.True);
        Assert.That(inventory.TryAdd(chest, out failure, 1, MultiCellGridLayoutContext<string>.Single(2, 1)), Is.True);
        var rejectedOverlap = inventory.TryAdd(crate, out failure, 1, MultiCellGridLayoutContext<string>.Single(3, 2));

        Assert.That(rejectedOverlap, Is.False);
        var chestContexts = inventory.GetLayoutContextsForStorageIndex(1)
            .OfType<MultiCellGridLayoutContext<string>>()
            .Select(c => $"({c.X},{c.Y})");
        var builder = new StringBuilder();
        builder.AppendLine(Render(inventory, 4, 3));
        builder.AppendLine();
        builder.AppendLine("crate at (3,2): rejected - " + failure);
        builder.AppendLine("chest occupies: " + string.Join(", ", chestContexts));
        WriteExample("MultiCellGridLayout", "MultiCellGridLayoutExample.txt", builder.ToString());
    }

    private static string Render(Inventory<string> inventory, int width, int height)
    {
        var lines = new string[height];
        for (int y = 0; y < height; y++)
        {
            var values = new string[width];
            for (int x = 0; x < width; x++)
                values[x] = inventory.GetItemAt(MultiCellGridLayoutContext<string>.Single(x, y))?.Definition.Id ?? ".";
            lines[y] = string.Join(" | ", values);
        }

        return string.Join("\n", lines);
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );
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



