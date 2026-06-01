using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Sorting;

[TestFixture]
[Category("Example")]
public class LayoutSortingExampleTests
{
    [Test]
    public void SortsSlotLayoutForUiRefresh()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(4), sword, apple);
        inventory.TryAdd(sword, out _, 1, SlotLayoutContext<string>.Single(3));
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(2));

        inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error);
        Assert.That(error, Is.Null);

        var lines = Enumerable.Range(0, 4)
            .Select(i => $"slot {i}: {inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(i))?.Definition.Id ?? "empty"}");
        WriteExample("Sorting", "LayoutSortingExample.txt", string.Join(Environment.NewLine, lines));
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
