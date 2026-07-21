using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Events;

[TestFixture]
[Category("Example")]
public class UiMoveAnimationEventExampleTests
{
    [Test]
    public void UsesMovedPayloadsForAnimation()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new GridLayout<string>(3, 2), sword, apple, potion);
        inventory.TryAdd(sword, out _, 1, GridLayoutContext<string>.Single(2, 1));
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(1, 1));
        inventory.TryAdd(potion, out _, 1, GridLayoutContext<string>.Single(0, 1));
        var animations = new List<string>();
        inventory.Changed += (_, args) =>
        {
            if (args.Moved.Count == 0)
                return;

            foreach (var move in args.Moved)
            {
                if (move.IsAutomatic)
                {
                    animations.Add($"skip {move.Cause} animation for {move.Instance.Definition.Id}");
                    continue;
                }

                var from = (GridLayoutContext<string>)move.FromPosition!;
                var to = (GridLayoutContext<string>)move.ToPosition!;
                animations.Add($"animate {move.Instance.Definition.Id}: ({from.X},{from.Y}) -> ({to.X},{to.Y})");
            }
        };

        Assert.That(inventory.TryMove(GridLayoutContext<string>.Single(0, 1), GridLayoutContext<string>.Single(0, 0), out var failure), Is.True);
        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out failure), Is.True);

        WriteExample("Events", "UiMoveAnimationEventExample.txt", string.Join("\n", animations));
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );
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
}


