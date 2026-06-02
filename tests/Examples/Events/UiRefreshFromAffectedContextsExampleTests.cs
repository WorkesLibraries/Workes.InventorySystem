using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Events;

[TestFixture]
[Category("Example")]
public class UiRefreshFromAffectedContextsExampleTests
{
    private const string Width = "ui-refresh-footprint-width";
    private const string Height = "ui-refresh-footprint-height";
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("ui-refresh-footprint").Require<int>(Width).Require<int>(Height);

    [Test]
    public void RefreshesCellsWithoutInspectingSemanticEventGroups()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var crate = new FootprintDefinition("crate", 1, 1);
        var chest = new FootprintDefinition("chest", 2, 2);
        var inventory = CreateInventory(
            new Workes.InventorySystem.Layout.MultiCellGridLayout<string>(
                4,
                3,
                new AttributeGridFootprintProvider<string>(Width, Height)),
            table,
            crate,
            chest);
        var refreshLog = new List<string>();
        inventory.Changed += (_, args) =>
        {
            if (args.RequiresFullRefresh)
            {
                refreshLog.Add("refresh all addressable cells");
                return;
            }

            var cells = args.AffectedLayoutContexts
                .OfType<MultiCellGridLayoutContext<string>>()
                .Select(c => $"({c.X},{c.Y})");
            refreshLog.Add("refresh cells: " + string.Join(", ", cells));
        };

        Assert.That(inventory.TryAdd(table, out var error, 1, MultiCellGridLayoutContext<string>.Single(1, 0)), Is.True, error);
        Assert.That(inventory.TryAdd(crate, out error, 1, MultiCellGridLayoutContext<string>.Single(0, 2)), Is.True, error);
        inventory.ReplaceContents(new (ItemDefinition<string> definition, int amount, ILayoutContext<string>? context)[]
        {
            (chest, 1, MultiCellGridLayoutContext<string>.Single(0, 0)),
            (crate, 1, MultiCellGridLayoutContext<string>.Single(3, 2))
        });

        WriteExample("Events", "UiRefreshFromAffectedContextsExample.txt", string.Join("\n", refreshLog));
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



