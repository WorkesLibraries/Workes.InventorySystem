using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class GridLayoutTests
{
    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string> layout,
        int maxStack = 10,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>(),
            new RuleContainer<string>()
            );

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    [Test]
    public void Constructor_RejectsNonPositiveWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridLayout<string>(0, 2));
    }

    [Test]
    public void Constructor_RejectsNonPositiveHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridLayout<string>(2, 0));
    }

    [Test]
    public void GetPositionCount_ReturnsWidthTimesHeight()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(3, 2), definitions: apple).CreateInventory();

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(6));
    }

    [Test]
    public void TryAdd_WithSingleContext_PlacesItemAtCoordinate()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(3, 2), definitions: apple).CreateInventory();

        Assert.That(inventory.TryAdd(apple, out var error, 1, GridLayoutContext<string>.Single(2, 1)), Is.True, error);

        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(2, 1))!.Definition.Id, Is.EqualTo("apple"));
    }

    [Test]
    public void GetItemAt_InvalidContext_ReturnsNull()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 2), definitions: apple).CreateInventory();

        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0)), Is.Null);
    }

    [Test]
    public void GetItemAt_EmptyCell_ReturnsNull()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 2), definitions: apple).CreateInventory();

        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(1, 1)), Is.Null);
    }

    [Test]
    public void TryGetContextForStorageIndex_ReturnsGridCoordinate()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(3, 2), definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(2, 1));

        Assert.That(inventory.Layout.TryGetContextForStorageIndex(inventory, 0, out var context), Is.True);
        var gridContext = (GridLayoutContext<string>)context!;
        Assert.That(gridContext.X, Is.EqualTo(2));
        Assert.That(gridContext.Y, Is.EqualTo(1));
    }

    [Test]
    public void GridLayoutContext_Single_StoresCoordinates()
    {
        var context = GridLayoutContext<string>.Single(1, 2);

        Assert.That(context.X, Is.EqualTo(1));
        Assert.That(context.Y, Is.EqualTo(2));
        Assert.That(context.IsMapped, Is.False);
    }

    [Test]
    public void GridLayoutContext_Map_StoresAddedEntryCellMappings()
    {
        var context = GridLayoutContext<string>.Map().Add(0, 2, 0).Add(1, 0, 1).Build();

        Assert.That(context.IsMapped, Is.True);
        Assert.That(context.X, Is.EqualTo(-1));
        Assert.That(context.Y, Is.EqualTo(-1));
        Assert.That(context.AddedEntryCells[0], Is.EqualTo((2, 0)));
        Assert.That(context.AddedEntryCells[1], Is.EqualTo((0, 1)));
    }

    [Test]
    public void GridLayoutContextBuilder_RejectsNegativeAddedEntryIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridLayoutContext<string>.Map().Add(-1, 0, 0));
    }

    [Test]
    public void GridLayoutContextBuilder_RejectsDuplicateAddedEntryIndex()
    {
        var builder = GridLayoutContext<string>.Map().Add(0, 0, 0);

        Assert.Throws<ArgumentException>(() => builder.Add(0, 1, 0));
    }

    [Test]
    public void GridLayout_RowMajorAutoPlacement_FillsLeftToRightThenDown()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new GridLayout<string>(2, 2), 1, apple);
        var inventory = manager.CreateInventory();

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(apple, out _);
        inventory.TryAdd(apple, out _);

        AssertCell(inventory, 0, 0, "apple");
        AssertCell(inventory, 1, 0, "apple");
        AssertCell(inventory, 0, 1, "apple");
    }

    [Test]
    public void GridLayout_ColumnMajorAutoPlacement_FillsTopToBottomThenRight()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 2, GridPlacementOrder.ColumnMajor), 1, apple).CreateInventory();

        inventory.TryAdd(apple, out _);
        inventory.TryAdd(apple, out _);
        inventory.TryAdd(apple, out _);

        AssertCell(inventory, 0, 0, "apple");
        AssertCell(inventory, 0, 1, "apple");
        AssertCell(inventory, 1, 0, "apple");
    }

    [Test]
    public void GridLayout_NullContext_AutoPlacesAfterMappedEntries()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), 1, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        builder.TryAdd(sword, out _);
        var context = GridLayoutContext<string>.Map().Add(1, 0, 0).Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        AssertCell(inventory, 0, 0, "sword");
        AssertCell(inventory, 1, 0, "apple");
    }

    [Test]
    public void GridLayout_NullContext_RejectsWhenFull()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(1, 1), 1, apple).CreateInventory();
        inventory.TryAdd(apple, out _);

        Assert.That(inventory.TryAdd(apple, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Not enough empty cells for new instances."));
    }

    [Test]
    public void GridLayout_RejectsDuplicateMappedTargetCells()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 2), 1, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        builder.TryAdd(sword, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 1, 0).Add(1, 1, 0).Build();

        Assert.That(builder.TryBuild(context, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Duplicate mapped target cell."));
    }

    [Test]
    public void GridLayout_RejectsInvalidMappedTargetCell()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(1, 1), definitions: apple).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 2, 0).Build();

        Assert.That(builder.TryBuild(context, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Grid position out of range."));
    }

    [Test]
    public void GridLayout_PlacesMultiAddTransactionIntoMappedCells()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(3, 2), 10, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 5);
        builder.TryAdd(sword, out _, 2);
        var context = GridLayoutContext<string>.Map().Add(0, 2, 0).Add(1, 0, 1).Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        AssertCell(inventory, 2, 0, "apple");
        AssertCell(inventory, 0, 1, "sword");
    }

    [Test]
    public void GridLayout_MappedAddCanTargetCellFreedBySameTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(1, 1), 1, apple, sword).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryRemove(inventory.Items[0], out _);
        builder.TryAdd(sword, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 0, 0).Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        AssertCell(inventory, 0, 0, "sword");
    }

    [Test]
    public void GridLayout_MappedAddRejectsOccupiedCellNotFreedByTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), 1, apple, sword).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(sword, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 0, 0).Build();

        Assert.That(builder.TryBuild(context, out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Cell already occupied."));
    }

    [Test]
    public void GridLayout_AmountDeltaDoesNotRequirePlacementMapping()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(1, 1), 10, apple).CreateInventory();
        inventory.TryAdd(apple, out _, 5, GridLayoutContext<string>.Single(0, 0));

        Assert.That(inventory.TryAdd(apple, out var error, 2, GridLayoutContext<string>.Single(0, 0)), Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 0))!.Amount, Is.EqualTo(7));
    }

    [Test]
    public void GridLayout_SingleContextForMultiAddTransactionIsRejected()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 2), 1, apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        builder.TryAdd(sword, out _);

        Assert.That(builder.TryBuild(GridLayoutContext<string>.Single(0, 0), out _, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Transaction placement context can only target one added entry unless it is a mapped context."));
    }

    [Test]
    public void GridLayout_MappedAddedIndexDistinguishesSameDefinitionDifferentMetadata()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateManager(new GridLayout<string>(2, 1), 10, gem).CreateInventory();
        var polished = new InstanceMetadata();
        polished.Set("quality", "polished");
        var cracked = new InstanceMetadata();
        cracked.Set("quality", "cracked");
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(gem, 1, null, polished, out _);
        builder.TryAdd(gem, 1, null, cracked, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 1, 0).Add(1, 0, 0).Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var error), Is.True, error);
        Assert.That(inventory.TryCommitTransaction(transaction!, out error), Is.True, error);

        inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(1, 0))!.Metadata.TryGet<string>("quality", out var polishedQuality);
        inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 0))!.Metadata.TryGet<string>("quality", out var crackedQuality);
        Assert.That(polishedQuality, Is.EqualTo("polished"));
        Assert.That(crackedQuality, Is.EqualTo("cracked"));
    }

    [Test]
    public void TransferBuilder_WithMappedGridContext_PlacesIncomingEntriesIntoCells()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new GridLayout<string>(3, 2), 10, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 5);
        source.TryAdd(sword, out _, 2);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemove(source.Find(apple).Single(), 5, out _);
        transfer.TryRemove(source.Find(sword).Single(), 2, out _);
        var context = GridLayoutContext<string>.Map().Add(0, 2, 0).Add(1, 0, 1).Build();

        Assert.That(transfer.Source.TryCommitTransfer(transfer, target, context, out var error), Is.True, error);

        AssertCell(target, 2, 0, "apple");
        AssertCell(target, 0, 1, "sword");
    }

    [Test]
    public void TrySwapInventories_WithMappedGridContexts_PlacesBothDirections()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new GridLayout<string>(3, 2), 10, apple, sword);
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();
        first.TryAdd(apple, out _, 3);
        second.TryAdd(sword, out _, 1);
        var firstContext = GridLayoutContext<string>.Map().Add(0, 2, 1).Build();
        var secondContext = GridLayoutContext<string>.Map().Add(0, 1, 0).Build();

        Assert.That(first.TrySwapWithInventory(second, firstContext, secondContext, out var error), Is.True, error);

        AssertCell(first, 2, 1, "sword");
        AssertCell(second, 1, 0, "apple");
    }

    [Test]
    public void FailedMappedGridTransferLeavesInventoriesUnchangedAndFiresNoEvents()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new GridLayout<string>(2, 1), 1, apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _);
        source.TryAdd(sword, out _);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemove(source.Items[0], 1, out _);
        transfer.TryRemove(source.Items[1], 1, out _);
        int sourceEvents = 0;
        int targetEvents = 0;
        source.Changed += (_, _) => sourceEvents++;
        target.Changed += (_, _) => targetEvents++;
        var context = GridLayoutContext<string>.Map().Add(0, 0, 0).Add(1, 0, 0).Build();

        Assert.That(transfer.Source.TryCommitTransfer(transfer, target, context, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Duplicate mapped target cell."));
        Assert.That(source.TotalItemCount, Is.EqualTo(2));
        Assert.That(target.TotalItemCount, Is.EqualTo(0));
        Assert.That(sourceEvents, Is.EqualTo(0));
        Assert.That(targetEvents, Is.EqualTo(0));
    }

    [Test]
    public void TryMove_WithGridLayout_MovesToEmptyCellAndFiresMovedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        int changed = 0;
        inventory.Changed += (_, e) =>
        {
            changed++;
            Assert.That(e.Moved.Single().Instance.Definition.Id, Is.EqualTo("apple"));
        };

        Assert.That(inventory.TryMove(GridLayoutContext<string>.Single(0, 0), GridLayoutContext<string>.Single(1, 0), out var error), Is.True, error);

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 0)), Is.Null);
        AssertCell(inventory, 1, 0, "apple");
    }

    [Test]
    public void TryMove_WithOccupiedTargetCell_FailsWithoutEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: new[] { apple, sword }).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(sword, out _, 1, GridLayoutContext<string>.Single(1, 0));
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        Assert.That(inventory.TryMove(GridLayoutContext<string>.Single(0, 0), GridLayoutContext<string>.Single(1, 0), out var error), Is.False);
        Assert.That(error, Is.EqualTo("Target cell is already occupied."));
        Assert.That(changed, Is.EqualTo(0));
    }

    [Test]
    public void TrySwap_WithGridLayout_SwapsOccupiedCellsAndFiresSwappedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: new[] { apple, sword }).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        inventory.TryAdd(sword, out _, 1, GridLayoutContext<string>.Single(1, 0));
        int changed = 0;
        inventory.Changed += (_, e) =>
        {
            changed++;
            Assert.That(e.Swapped.Single().AfterSwapFromPositionInstance.Definition.Id, Is.EqualTo("sword"));
        };

        Assert.That(inventory.TrySwap(GridLayoutContext<string>.Single(0, 0), GridLayoutContext<string>.Single(1, 0), out var error), Is.True, error);

        Assert.That(changed, Is.EqualTo(1));
        AssertCell(inventory, 0, 0, "sword");
        AssertCell(inventory, 1, 0, "apple");
    }

    [Test]
    public void TrySwap_WithEmptyCell_FailsWithoutEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        Assert.That(inventory.TrySwap(GridLayoutContext<string>.Single(0, 0), GridLayoutContext<string>.Single(1, 0), out var error), Is.False);
        Assert.That(error, Is.EqualTo("One or both of the items not found in inventory."));
        Assert.That(changed, Is.EqualTo(0));
    }

    [Test]
    public void GetPersistentData_ReturnsDefensiveCellMapCopy()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));

        var data = (GridLayoutPersistentData)inventory.Layout.GetPersistentData();
        data.CellMap.Clear();

        AssertCell(inventory, 0, 0, "apple");
    }

    [Test]
    public void RestorePersistentData_RestoresCellMap()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: apple).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(1, 0));
        var data = inventory.Layout.GetPersistentData();
        var restored = new GridLayout<string>(2, 1);

        restored.RestorePersistentData(data);

        Assert.That(restored.GetItemAt(inventory, GridLayoutContext<string>.Single(1, 0))!.Definition.Id, Is.EqualTo("apple"));
    }

    [Test]
    public void RestorePersistentData_RejectsWrongDataType()
    {
        var layout = new GridLayout<string>(2, 1);

        Assert.Throws<InvalidOperationException>(() => layout.RestorePersistentData(new SlotLayoutPersistentData()));
    }

    [Test]
    public void RestorePersistentData_RejectsMismatchedDimensions()
    {
        var layout = new GridLayout<string>(2, 1);
        var data = new GridLayoutPersistentData
        {
            Width = 1,
            Height = 2,
            PlacementOrder = GridPlacementOrder.RowMajor,
            CellMap = new List<int?> { null, null }
        };

        Assert.Throws<InvalidOperationException>(() => layout.RestorePersistentData(data));
    }

    [Test]
    public void CreateInventory_ClonesDefaultGridLayout_PerInventory()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new GridLayout<string>(2, 1), definitions: apple);
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();

        first.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));

        Assert.That(second.Layout.GetItemAt(second, GridLayoutContext<string>.Single(0, 0)), Is.Null);
    }

    [Test]
    public void ReplaceContents_WithGridLayout_FiresSingleEventWithGridContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: new[] { apple, sword }).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));
        int changed = 0;
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) =>
        {
            changed++;
            captured = e;
        };

        inventory.ReplaceContents(new[] { (sword, 1, (ILayoutContext<string>?)GridLayoutContext<string>.Single(1, 0)) });

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(((GridLayoutContext<string>)captured!.Removed.Single().LayoutContext!).X, Is.EqualTo(0));
        Assert.That(((GridLayoutContext<string>)captured.Added.Single().LayoutContext!).X, Is.EqualTo(1));
    }

    [Test]
    public void ReplaceContents_InvalidGridContextLeavesOriginalInventoryUnchanged()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new GridLayout<string>(2, 1), definitions: new[] { apple, sword }).CreateInventory();
        inventory.TryAdd(apple, out _, 1, GridLayoutContext<string>.Single(0, 0));

        Assert.Throws<InvalidOperationException>(() =>
            inventory.ReplaceContents(new[] { (sword, 1, (ILayoutContext<string>?)GridLayoutContext<string>.Single(9, 0)) }));

        AssertCell(inventory, 0, 0, "apple");
        Assert.That(inventory.InstanceCount, Is.EqualTo(1));
    }

    private static void AssertCell(Inventory<string> inventory, int x, int y, string expectedDefinitionId)
    {
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(x, y))!.Definition.Id, Is.EqualTo(expectedDefinitionId));
    }
}


