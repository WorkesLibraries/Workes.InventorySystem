using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new SlotLayout<string>(5), sword, apple, potion);
        inventory.TryAdd(sword, out _, 1, SlotLayoutContext<string>.Single(4));
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(3));
        inventory.TryAdd(potion, out _, 1, SlotLayoutContext<string>.Single(1));
        var beforeSlots = DescribeSlots(inventory, 5);
        var beforeStorageOrder = DescribeStorageOrder(inventory);
        var affectedSlots = new List<int>();
        var moved = new List<string>();
        inventory.Changed += (_, args) =>
        {
            affectedSlots.AddRange(args.AffectedLayoutContexts
                .OfType<SlotLayoutContext<string>>()
                .Select(c => c.SlotIndex));
            moved.AddRange(args.Moved.Select(m =>
            {
                var from = (SlotLayoutContext<string>)m.FromPosition!;
                var to = (SlotLayoutContext<string>)m.ToPosition!;
                return $"{m.Instance.Definition.Id}: {from.SlotIndex} -> {to.SlotIndex}";
            }));
        };

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error), Is.True, error);

        var builder = new StringBuilder();
        builder.AppendLine("Before slots");
        builder.AppendLine(beforeSlots);
        builder.AppendLine("After slots");
        builder.AppendLine(DescribeSlots(inventory, 5));
        builder.AppendLine("Storage order before: " + beforeStorageOrder);
        builder.AppendLine("Storage order after:  " + DescribeStorageOrder(inventory));
        builder.AppendLine("Affected slots: " + string.Join(", ", affectedSlots.Distinct().OrderBy(i => i)));
        builder.AppendLine("Moved payloads");
        foreach (var line in moved)
            builder.AppendLine(line);
        WriteExample("Sorting", "LayoutSortingExample.txt", builder.ToString());
    }

    private static string DescribeSlots(Inventory<string> inventory, int slotCount)
    {
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(0, slotCount)
                .Select(i => $"slot {i}: {inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(i))?.Definition.Id ?? "empty"}"));
    }

    private static string DescribeStorageOrder(Inventory<string> inventory)
    {
        return string.Join(", ", inventory.Items.Select(i => i.Definition.Id));
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout);
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


