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
public class UiEventIntegrationWorkflowExampleTests
{
    private static readonly AttributeKey<int> Width = new("ui-footprint-width");
    private static readonly AttributeKey<int> Height = new("ui-footprint-height");
    private static readonly ItemSchema<string> FootprintSchema = ItemSchema<string>.Create("ui-footprint").Require(Width).Require(Height);

    [Test]
    public void TracksAffectedCellsForMultiCellInventoryUi()
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

        var log = new List<string>();
        inventory.Changed += (_, args) =>
        {
            if (args.RequiresFullRefresh)
            {
                log.Add("refresh: all");
                return;
            }

            var cells = args.AffectedLayoutContexts
                .OfType<MultiCellGridLayoutContext<string>>()
                .Select(c => $"({c.X},{c.Y})");
            log.Add("refresh: " + string.Join(", ", cells));
        };

        inventory.TryAdd(table, out _, 1, MultiCellGridLayoutContext<string>.Single(1, 0));
        inventory.TryAdd(crate, out _, 1, MultiCellGridLayoutContext<string>.Single(0, 2));
        inventory.ReplaceContents(new (ItemDefinition<string> definition, int amount, ILayoutContext<string>? context)[]
        {
            (chest, 1, (ILayoutContext<string>?)MultiCellGridLayoutContext<string>.Single(0, 0)),
            (crate, 1, (ILayoutContext<string>?)MultiCellGridLayoutContext<string>.Single(3, 2))
        });

        WriteExample("Events", "UiMultiCellRefreshWorkflowExample.txt", string.Join("\n", log));
    }

    [Test]
    public void TracksMovePayloadsForSortedGridUi()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new GridLayout<string>(3, 2), sword, apple, potion);
        inventory.TryAdd(sword, out _, 1, GridLayoutContext<string>.Single(2, 1));
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(1, 1));
        inventory.TryAdd(potion, out _, 1, GridLayoutContext<string>.Single(0, 1));

        string movedSummary = string.Empty;
        inventory.Changed += (_, args) =>
        {
            if (args.Moved.Count == 0)
                return;

            movedSummary = string.Join(
                "\n",
                args.Moved.Select(m =>
                {
                    var from = (GridLayoutContext<string>)m.FromPosition!;
                    var to = (GridLayoutContext<string>)m.ToPosition!;
                    return $"{m.Instance.Definition.Id}: ({from.X},{from.Y}) -> ({to.X},{to.Y})";
                }));
        };

        inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out _);

        WriteExample("Events", "UiSortedGridMovePayloadExample.txt", movedSummary);
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
