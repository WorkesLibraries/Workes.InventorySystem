using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Events;

[TestFixture]
[Category("Example")]
public class InventoryChangedEventExampleTests
{
    [Test]
    public void SlotInventory_UsesChangedEventsToRefreshAffectedSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(4), apple);
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, e) => events.Add(e);

        inventory.TryAdd(apple, out var addError, 2, SlotLayoutContext<string>.Single(2));
        inventory.TryAdd(apple, out var mergeError, 3, SlotLayoutContext<string>.Single(2));

        Assert.That(addError, Is.Null);
        Assert.That(mergeError, Is.Null);
        Assert.That(events.Count, Is.EqualTo(2));
        Assert.That(((SlotLayoutContext<string>)events[0].Added[0].LayoutContext!).SlotIndex, Is.EqualTo(2));
        Assert.That(events[1].Modified[0].BeforeAmount, Is.EqualTo(2));
        Assert.That(events[1].Modified[0].AfterAmount, Is.EqualTo(5));

        var outputPath = GetOutputPath("SlotInventoryChangedEventExample.txt");
        File.WriteAllText(outputPath, DescribeSlotRefresh(events));
        TestContext.Out.WriteLine("Inventory changed event slot example output: " + outputPath);
    }

    [Test]
    public void ReplaceContents_UsesOneEventForReplacementRefresh()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(3), apple, berry);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        InventoryChangedEventArgs<string>? replacementEvent = null;
        var changedCount = 0;
        inventory.Changed += (_, e) =>
        {
            changedCount++;
            replacementEvent = e;
        };

        inventory.ReplaceContents(new[] { (berry, 2, (ILayoutContext<string>?)SlotLayoutContext<string>.Single(1)) });

        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(replacementEvent!.Cleared, Is.True);
        Assert.That(((SlotLayoutContext<string>)replacementEvent.Removed[0].LayoutContext!).SlotIndex, Is.EqualTo(0));
        Assert.That(((SlotLayoutContext<string>)replacementEvent.Added[0].LayoutContext!).SlotIndex, Is.EqualTo(1));

        var outputPath = GetOutputPath("ReplaceContentsChangedEventExample.txt");
        File.WriteAllText(outputPath, DescribeReplacement(replacementEvent));
        TestContext.Out.WriteLine("Inventory changed event replacement example output: " + outputPath);
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

    private static string DescribeSlotRefresh(IReadOnlyList<InventoryChangedEventArgs<string>> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Slot refresh from Changed events");
        builder.AppendLine("--------------------------------");
        for (int i = 0; i < events.Count; i++)
        {
            var change = events[i];
            builder.AppendLine("Event " + (i + 1));
            foreach (var added in change.Added)
            {
                var slot = ((SlotLayoutContext<string>)added.LayoutContext!).SlotIndex;
                builder.AppendLine("  refresh slot " + slot + ": added " + added.Instance.Definition.Id + " x" + added.Instance.Amount);
            }
            foreach (var modified in change.Modified)
            {
                var slot = ((SlotLayoutContext<string>)modified.AfterLayoutContext!).SlotIndex;
                builder.AppendLine("  refresh slot " + slot + ": amount " + modified.BeforeAmount + " -> " + modified.AfterAmount);
            }
        }

        return builder.ToString();
    }

    private static string DescribeReplacement(InventoryChangedEventArgs<string> change)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Replacement refresh from one Changed event");
        builder.AppendLine("------------------------------------------");
        builder.AppendLine("Cleared before replacement: " + change.Cleared);
        foreach (var removed in change.Removed)
        {
            var slot = ((SlotLayoutContext<string>)removed.LayoutContext!).SlotIndex;
            builder.AppendLine("  clear slot " + slot + ": removed " + removed.Instance.Definition.Id);
        }
        foreach (var added in change.Added)
        {
            var slot = ((SlotLayoutContext<string>)added.LayoutContext!).SlotIndex;
            builder.AppendLine("  refresh slot " + slot + ": added " + added.Instance.Definition.Id + " x" + added.Instance.Amount);
        }

        return builder.ToString();
    }

    private static string GetOutputPath(string fileName)
    {
        var outputPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "Events",
            fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        return outputPath;
    }
}
