using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Events;

[TestFixture]
[Category("Example")]
public class UiMetadataChangedEventExampleTests
{
    [Test]
    public void UsesMetadataChangedPayloadsForTargetedUiUpdates()
    {
        var gem = new ItemDefinition<string>("gem");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new SlotLayout<string>(3), gem, potion);
        inventory.Add(gem, amount: 3, context: SlotLayoutContext<string>.Single(0));

        var uiUpdates = new List<string>();
        inventory.Changed += (_, args) =>
        {
            if (args.RequiresFullRefresh)
                uiUpdates.Add("refresh all slots");

            foreach (var change in args.MetadataChanged)
            {
                var slot = ((SlotLayoutContext<string>)change.LayoutContext!).SlotIndex;
                foreach (var entry in DescribeMetadataChanges(change.BeforeMetadata, change.AfterMetadata))
                    uiUpdates.Add($"slot {slot} metadata changed: {entry}");
            }

            foreach (var added in args.Added)
            {
                var slot = ((SlotLayoutContext<string>)added.LayoutContext!).SlotIndex;
                var metadata = FormatMetadata(added.Instance.Metadata.AsReadOnly());
                uiUpdates.Add($"slot {slot} added {added.Instance.Definition.Id} x{added.Instance.Amount} with metadata {metadata}");
            }

            foreach (var modified in args.Modified)
            {
                var slot = ((SlotLayoutContext<string>)modified.AfterLayoutContext!).SlotIndex;
                uiUpdates.Add($"slot {slot} amount changed: {modified.BeforeAmount} -> {modified.AfterAmount}");
            }
        };

        var gemStack = inventory.Items.Single();
        Assert.That(gemStack.Metadata.TrySet("quality", "polished", out var error), Is.True, error);
        Assert.That(gemStack.Metadata.TrySet("owner", "player", out error), Is.True, error);
        Assert.That(gemStack.TrySplitAndSetMetadata(1, "quest-item", true, out var questGem, out error), Is.True, error);

        Assert.That(questGem, Is.Not.Null);
        Assert.That(uiUpdates, Does.Contain("slot 0 metadata changed: quality = polished"));
        Assert.That(uiUpdates, Does.Contain("slot 0 metadata changed: owner = player"));
        Assert.That(uiUpdates.Count(update => update.Contains("metadata changed")), Is.EqualTo(2));
        Assert.That(uiUpdates, Does.Contain("slot 1 added gem x1 with metadata owner=player, quality=polished, quest-item=True"));
        Assert.That(uiUpdates, Does.Contain("slot 0 amount changed: 3 -> 2"));

        var output = BuildOutput(uiUpdates, inventory);
        WriteExample("Events", "UiMetadataChangedEventExample.txt", output);
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

    private static IEnumerable<string> DescribeMetadataChanges(
        IReadOnlyDictionary<string, object?> before,
        IReadOnlyDictionary<string, object?> after)
    {
        foreach (var key in before.Keys.Concat(after.Keys).Distinct().OrderBy(key => key))
        {
            before.TryGetValue(key, out var beforeValue);
            after.TryGetValue(key, out var afterValue);
            if (!Equals(beforeValue, afterValue))
                yield return $"{key} = {afterValue}";
        }
    }

    private static string BuildOutput(IEnumerable<string> uiUpdates, Inventory<string> inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("UI Metadata Changed Event Example");
        builder.AppendLine("=================================");
        builder.AppendLine();
        builder.AppendLine("UI Updates");
        builder.AppendLine("----------");
        foreach (var update in uiUpdates)
            builder.AppendLine(update);
        builder.AppendLine();
        builder.AppendLine("Final Slots");
        builder.AppendLine("-----------");

        for (int slot = 0; slot < 3; slot++)
        {
            var item = inventory.GetItemAt(SlotLayoutContext<string>.Single(slot));
            if (item == null)
            {
                builder.AppendLine($"{slot}: empty");
                continue;
            }

            builder.AppendLine($"{slot}: {item.Definition.Id} x{item.Amount} [{FormatMetadata(item.Metadata.AsReadOnly())}]");
        }

        return builder.ToString();
    }

    private static string FormatMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        return string.Join(", ", metadata.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static void WriteExample(string area, string fileName, string content)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", area);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }
}
