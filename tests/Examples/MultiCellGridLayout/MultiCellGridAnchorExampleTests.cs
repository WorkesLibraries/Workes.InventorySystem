using System.IO;
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
public class MultiCellGridAnchorExampleTests
{
    private const string Width = "anchor-example-width";
    private const string Height = "anchor-example-height";
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("anchor-example-footprint").Require<int>(Width).Require<int>(Height);

    [Test]
    public void PlacesByDefaultAndExplicitAnchors()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var chest = new FootprintDefinition("chest", 2, 2);
        var defaultAnchor = CreateInventory(new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(4, 3, Provider()), table);
        var explicitAnchor = CreateInventory(new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(4, 3, Provider()), table);
        var mappedAnchors = CreateInventory(new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(4, 3, Provider()), table, chest);

        defaultAnchor.TryAdd(table, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));
        explicitAnchor.TryAdd(table, out _, 1, MultiCellGridLayoutContext<string>.Single(2, 0, GridAnchor.TopRight));
        var builder = InventoryTransaction<string>.From(mappedAnchors);
        builder.TryAdd(table, out _);
        builder.TryAdd(chest, out _);
        var placement = MultiCellGridLayoutContext<string>.Map()
            .Add(0, 2, 0, GridAnchor.TopRight)
            .Add(1, 3, 2, GridAnchor.BottomRight)
            .Build();
        Assert.That(builder.TryToInventoryTransaction(placement, out var transaction, out var error), Is.True, error);
        Assert.That(mappedAnchors.TryCommitTransaction(transaction!, out error), Is.True, error);

        var output = new StringBuilder();
        output.AppendLine("Default top-left anchor");
        output.AppendLine(Render(defaultAnchor, 4, 3));
        output.AppendLine();
        output.AppendLine("Explicit top-right anchor");
        output.AppendLine(Render(explicitAnchor, 4, 3));
        output.AppendLine();
        output.AppendLine("Mapped transaction with mixed anchors");
        output.AppendLine(Render(mappedAnchors, 4, 3));
        WriteExample("MultiCellGridLayout", "MultiCellGridAnchorExample.txt", output.ToString());
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



