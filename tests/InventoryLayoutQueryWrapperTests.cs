using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryLayoutQueryWrapperTests
{
    [TestCaseSource(nameof(BuiltInLayoutCases))]
    public void InventoryLayoutQueryWrappers_MatchActiveLayoutQueries(BuiltInLayoutCase testCase)
    {
        var item = new ItemDefinition<string>("item");
        var inventory = CreateInventory(testCase.Layout, item);

        Assert.That(inventory.TryAdd(item, out var error, 1, testCase.Context), Is.True, error);

        Assert.That(inventory.GetLayoutPositionCount(), Is.EqualTo(inventory.Layout.GetPositionCount(inventory)));
        Assert.That(
            inventory.GetAddressableLayoutContexts().Select(testCase.DescribeContext),
            Is.EqualTo(inventory.Layout.GetAddressableContexts(inventory).Select(testCase.DescribeContext)));

        var wrapperItem = inventory.GetItemAt(testCase.Context);
        var rawItem = inventory.Layout.GetItemAt(inventory, testCase.Context);

        Assert.That(wrapperItem, Is.SameAs(rawItem));
        Assert.That(wrapperItem, Is.SameAs(inventory.Items[0]));

        Assert.That(
            inventory.GetLayoutContextsForStorageIndex(0).Select(testCase.DescribeContext),
            Is.EqualTo(inventory.Layout.GetContextsForStorageIndex(inventory, 0).Select(testCase.DescribeContext)));

        Assert.That(inventory.TryGetLayoutContextForStorageIndex(0, out var wrapperContext), Is.True);
        Assert.That(inventory.Layout.TryGetContextForStorageIndex(inventory, 0, out var rawContext), Is.True);
        Assert.That(testCase.DescribeContext(wrapperContext!), Is.EqualTo(testCase.DescribeContext(rawContext!)));

        Assert.That(
            inventory.GetLayoutContextsForItem(inventory.Items[0]).Select(testCase.DescribeContext),
            Is.EqualTo(inventory.GetLayoutContextsForStorageIndex(0).Select(testCase.DescribeContext)));

        Assert.That(inventory.TryGetLayoutContextForItem(inventory.Items[0], out var itemContext), Is.True);
        Assert.That(testCase.DescribeContext(itemContext!), Is.EqualTo(testCase.DescribeContext(wrapperContext!)));
    }

    [Test]
    public void InventoryLayoutQueryWrappers_ReturnEmptyOrFalseForUnknownStorageIndexAndItem()
    {
        var item = new ItemDefinition<string>("item");
        var inventory = CreateInventory(new SlotLayout<string>(2), item);
        var otherInventory = CreateInventory(new SlotLayout<string>(2), item);
        Assert.That(otherInventory.TryAdd(item, out var error), Is.True, error);
        var foreignItem = otherInventory.Items[0];

        Assert.That(inventory.GetLayoutContextsForStorageIndex(99), Is.Empty);
        Assert.That(inventory.TryGetLayoutContextForStorageIndex(99, out var missingContext), Is.False);
        Assert.That(missingContext, Is.Null);
        Assert.That(inventory.GetLayoutContextsForItem(foreignItem), Is.Empty);
        Assert.That(inventory.TryGetLayoutContextForItem(foreignItem, out var foreignContext), Is.False);
        Assert.That(foreignContext, Is.Null);
    }

    [Test]
    public void InventoryLayoutQueryWrappers_MultiCellItemReturnsEveryOccupiedContext()
    {
        var item = new ItemDefinition<string>("item");
        var inventory = CreateInventory(
            new MultiCellGridLayout<string>(3, 2, new FixedFootprintProvider(2, 1)),
            item);

        Assert.That(inventory.TryAdd(item, out var error, 1, MultiCellGridLayoutContext<string>.Single(1, 0)), Is.True, error);

        var contexts = inventory.GetLayoutContextsForItem(inventory.Items[0])
            .Cast<MultiCellGridLayoutContext<string>>()
            .Select(c => (c.X, c.Y))
            .OrderBy(c => c.X)
            .ThenBy(c => c.Y)
            .ToList();

        Assert.That(contexts, Is.EqualTo(new[] { (1, 0), (2, 0) }));
    }

    [Test]
    public void InventoryLayoutQueryWrappers_RejectNullItemLookup()
    {
        var item = new ItemDefinition<string>("item");
        var inventory = CreateInventory(new EntryLayout<string>(), item);

        Assert.Throws<ArgumentNullException>(() => inventory.GetLayoutContextsForItem(null!));
        Assert.Throws<ArgumentNullException>(() => inventory.TryGetLayoutContextForItem(null!, out _));
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();

        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            catalog).CreateInventory();
    }

    private static IEnumerable<BuiltInLayoutCase> BuiltInLayoutCases()
    {
        yield return new BuiltInLayoutCase(
            "Entry",
            new EntryLayout<string>(),
            EntryLayoutContext<string>.Single(0),
            DescribeEntryContext);
        yield return new BuiltInLayoutCase(
            "Slot",
            new SlotLayout<string>(3),
            SlotLayoutContext<string>.Single(1),
            DescribeSlotContext);
        yield return new BuiltInLayoutCase(
            "Grid",
            new GridLayout<string>(3, 2),
            GridLayoutContext<string>.Single(2, 1),
            DescribeGridContext);
        yield return new BuiltInLayoutCase(
            "MultiCellGrid",
            new MultiCellGridLayout<string>(3, 2, new FixedFootprintProvider(1, 1)),
            MultiCellGridLayoutContext<string>.Single(1, 1),
            DescribeMultiCellGridContext);
        yield return new BuiltInLayoutCase(
            "Equipment",
            new EquipmentLayout<string>(new EquipmentSlot<string>("main-hand")),
            EquipmentLayoutContext<string>.Single("main-hand"),
            DescribeEquipmentContext);
        yield return new BuiltInLayoutCase(
            "Sectioned",
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            SectionedLayoutContext<string>.Single("bag", 1),
            DescribeSectionedContext);
    }

    private static string DescribeEntryContext(ILayoutContext<string> context) =>
        $"entry:{((EntryLayoutContext<string>)context).TargetIndex}";

    private static string DescribeSlotContext(ILayoutContext<string> context) =>
        $"slot:{((SlotLayoutContext<string>)context).SlotIndex}";

    private static string DescribeGridContext(ILayoutContext<string> context)
    {
        var grid = (GridLayoutContext<string>)context;
        return $"grid:{grid.X},{grid.Y}";
    }

    private static string DescribeMultiCellGridContext(ILayoutContext<string> context)
    {
        var grid = (MultiCellGridLayoutContext<string>)context;
        return $"multicell:{grid.X},{grid.Y},{grid.Anchor}";
    }

    private static string DescribeEquipmentContext(ILayoutContext<string> context) =>
        $"equipment:{((EquipmentLayoutContext<string>)context).SlotId}";

    private static string DescribeSectionedContext(ILayoutContext<string> context)
    {
        var sectioned = (SectionedLayoutContext<string>)context;
        return $"section:{sectioned.SectionId},{sectioned.SlotIndex}";
    }

    public sealed class BuiltInLayoutCase
    {
        public BuiltInLayoutCase(
            string name,
            IInventoryLayout<string> layout,
            ILayoutContext<string> context,
            Func<ILayoutContext<string>, string> describeContext)
        {
            Name = name;
            Layout = layout;
            Context = context;
            DescribeContext = describeContext;
        }

        public string Name { get; }

        public IInventoryLayout<string> Layout { get; }

        public ILayoutContext<string> Context { get; }

        public Func<ILayoutContext<string>, string> DescribeContext { get; }

        public override string ToString() => Name;
    }

    private sealed class FixedFootprintProvider : IGridFootprintProvider<string>
    {
        private readonly GridFootprint _footprint;

        public FixedFootprintProvider(int width, int height)
        {
            _footprint = new GridFootprint(width, height);
        }

        public GridFootprint GetFootprint(ItemDefinition<string> definition) => _footprint;
    }
}
