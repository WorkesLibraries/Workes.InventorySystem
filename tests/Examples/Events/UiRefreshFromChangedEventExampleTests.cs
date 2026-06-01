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
public class UiRefreshFromChangedEventExampleTests
{
    [Test]
    public void ListenerRefreshesAffectedLayoutContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new GridLayout<string>(2, 2), apple, berry);
        string lastRefresh = string.Empty;
        inventory.Changed += (_, args) =>
        {
            lastRefresh = string.Join(
                "\n",
                args.AffectedLayoutContexts
                    .OfType<GridLayoutContext<string>>()
                    .Select(c => $"refresh ({c.X},{c.Y})"));
        };

        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(1, 0));
        inventory.TryAdd(berry, out _, 1, GridLayoutContext<string>.Single(0, 1));

        WriteExample("Events", "UiRefreshFromChangedEventExample.txt", lastRefresh);
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
}
