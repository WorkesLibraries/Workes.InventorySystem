using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Fixed-size grid layout that places each inventory item instance into one cell.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>Grid contexts must be <see cref="GridLayoutContext{TKey}"/> instances.</remarks>
public class GridLayout<TKey> : IInventoryLayout<TKey>
{
    private readonly List<int?> _cellMap;

    /// <summary>
    /// Gets the number of cells across the grid.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of cells down the grid.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the automatic placement order used for null-context additions.
    /// </summary>
    public GridPlacementOrder PlacementOrder { get; }

    /// <summary>
    /// Creates a fixed-size grid layout.
    /// </summary>
    /// <param name="width">The number of cells across the grid.</param>
    /// <param name="height">The number of cells down the grid.</param>
    /// <param name="placementOrder">The automatic placement order for null-context additions.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than or equal to zero.</exception>
    public GridLayout(
        int width,
        int height,
        GridPlacementOrder placementOrder = GridPlacementOrder.RowMajor)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Grid width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Grid height must be greater than zero.");

        Width = width;
        Height = height;
        PlacementOrder = placementOrder;
        _cellMap = new List<int?>(width * height);
        for (int i = 0; i < width * height; i++)
            _cellMap.Add(null);
    }

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory) => Width * Height;

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_cellMap.Count);
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
            contexts.Add(GridLayoutContext<TKey>.Single(ToX(cell), ToY(cell)));
        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (context is not GridLayoutContext<TKey> gridContext || gridContext.IsMapped)
            return null;
        if (!IsInRange(gridContext.X, gridContext.Y))
            return null;

        var itemIndex = _cellMap[ToCellIndex(gridContext.X, gridContext.Y)];
        if (!itemIndex.HasValue || itemIndex.Value < 0 || itemIndex.Value >= inventory.Items.Count)
            return null;

        return inventory.Items[itemIndex.Value];
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        for (int cell = 0; cell < _cellMap.Count; cell++)
        {
            if (_cellMap[cell] == storageIndex)
                return new List<ILayoutContext<TKey>> { GridLayoutContext<TKey>.Single(ToX(cell), ToY(cell)) };
        }

        return Array.Empty<ILayoutContext<TKey>>();
    }

    /// <inheritdoc />
    public bool TryGetContextForStorageIndex(Inventory<TKey> inventory, int storageIndex, out ILayoutContext<TKey>? context)
    {
        var contexts = GetContextsForStorageIndex(inventory, storageIndex);
        context = contexts.Count > 0 ? contexts[0] : null;
        return context != null;
    }

    /// <inheritdoc />
    public IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context)
    {
        if (context is GridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            if (!IsInRange(gridContext.X, gridContext.Y))
                yield break;

            var itemIndex = _cellMap[ToCellIndex(gridContext.X, gridContext.Y)];
            if (itemIndex.HasValue)
                yield return itemIndex.Value;
            yield break;
        }

        if (context != null)
            yield break;

        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            var itemIndex = _cellMap[cell];
            if (itemIndex.HasValue)
                yield return itemIndex.Value;
        }
    }

    /// <inheritdoc />
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out string? error)
    {
        error = null;

        foreach (var (index, _) in transaction.AmountDeltas)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                error = "Index out of range.";
                return false;
            }
        }

        var removedIndices = new HashSet<int>();
        foreach (var (index, _) in transaction.Removed)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                error = "Index out of range.";
                return false;
            }
            removedIndices.Add(index);
        }

        var simulated = new List<int?>(_cellMap);
        var removed = new List<int>(removedIndices);
        removed.Sort((a, b) => b.CompareTo(a));
        foreach (int removedIndex in removed)
            ApplyRemovalToCellMap(simulated, removedIndex);

        int futureStorageIndex = inventory.Items.Count - removed.Count;
        var explicitCells = new HashSet<int>();
        for (int addedIndex = 0; addedIndex < transaction.Added.Count; addedIndex++)
        {
            var (_, itemContext) = transaction.Added[addedIndex];
            int cell;

            if (itemContext is GridLayoutContext<TKey> itemGridContext)
            {
                if (itemGridContext.IsMapped)
                {
                    error = "Invalid context type.";
                    return false;
                }
                if (!IsInRange(itemGridContext.X, itemGridContext.Y))
                {
                    error = "Grid position out of range.";
                    return false;
                }

                cell = ToCellIndex(itemGridContext.X, itemGridContext.Y);
                if (!explicitCells.Add(cell))
                {
                    error = "Duplicate mapped target cell.";
                    return false;
                }
            }
            else if (itemContext == null)
            {
                cell = FindFirstAvailableCell(simulated);
                if (cell < 0)
                {
                    error = "Not enough empty cells for new instances.";
                    return false;
                }
            }
            else
            {
                error = "Invalid context type.";
                return false;
            }

            if (simulated[cell].HasValue)
            {
                error = "Cell already occupied.";
                return false;
            }

            simulated[cell] = futureStorageIndex + addedIndex;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryApplyPlacementContext(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        ILayoutContext<TKey>? context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out string? error)
    {
        mappedTransaction = null;
        error = null;

        if (context == null)
        {
            mappedTransaction = transaction;
            return true;
        }

        if (context is not GridLayoutContext<TKey> gridContext)
        {
            error = "Invalid context type.";
            return false;
        }

        if (!gridContext.IsMapped)
        {
            if (transaction.Added.Count == 1)
                return TryCreateAddedCopy(transaction, 0, gridContext, out mappedTransaction, out error);

            if (transaction.Added.Count == 0 && transaction.AmountDeltas.Count == 1 && transaction.AmountDeltas[0].delta > 0)
            {
                if (!IsInRange(gridContext.X, gridContext.Y))
                {
                    error = "Grid position out of range.";
                    return false;
                }

                var cellIndex = _cellMap[ToCellIndex(gridContext.X, gridContext.Y)];
                if (!cellIndex.HasValue || cellIndex.Value != transaction.AmountDeltas[0].index)
                {
                    error = "Merge delta does not match the item at the specified grid position.";
                    return false;
                }

                mappedTransaction = transaction;
                return true;
            }

            error = "Transaction placement context can only target one added entry unless it is a mapped context.";
            return false;
        }

        foreach (var pair in gridContext.AddedEntryCells)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                error = "Mapped added entry index out of range.";
                return false;
            }
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        var simulated = new List<int?>(_cellMap);
        var removedIndices = new HashSet<int>();
        foreach (var (index, _) in transaction.Removed)
            removedIndices.Add(index);
        var removed = new List<int>(removedIndices);
        removed.Sort((a, b) => b.CompareTo(a));
        foreach (int removedIndex in removed)
            ApplyRemovalToCellMap(simulated, removedIndex);
        int futureStorageIndex = transaction.Inventory.Items.Count - removed.Count;
        var reservedCells = new HashSet<int>();

        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (gridContext.AddedEntryCells.TryGetValue(i, out var mappedCell))
            {
                var mappedContext = GridLayoutContext<TKey>.Single(mappedCell.x, mappedCell.y);
                if (existingContext is GridLayoutContext<TKey> existingGridContext &&
                    !existingGridContext.IsMapped &&
                    (existingGridContext.X != mappedCell.x || existingGridContext.Y != mappedCell.y))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not GridLayoutContext<TKey>)
                {
                    error = "Invalid context type.";
                    return false;
                }
                if (!IsInRange(mappedCell.x, mappedCell.y))
                {
                    error = "Grid position out of range.";
                    return false;
                }
                var cell = ToCellIndex(mappedCell.x, mappedCell.y);
                if (!reservedCells.Add(cell))
                {
                    error = "Duplicate mapped target cell.";
                    return false;
                }
                if (simulated[cell].HasValue)
                {
                    error = "Cell already occupied.";
                    return false;
                }
                simulated[cell] = futureStorageIndex + i;
                added.Add((instance, mappedContext));
            }
            else if (existingContext is GridLayoutContext<TKey> existingGridContext && !existingGridContext.IsMapped)
            {
                if (!IsInRange(existingGridContext.X, existingGridContext.Y))
                {
                    error = "Grid position out of range.";
                    return false;
                }
                var cell = ToCellIndex(existingGridContext.X, existingGridContext.Y);
                if (!reservedCells.Add(cell))
                {
                    error = "Duplicate mapped target cell.";
                    return false;
                }
                if (simulated[cell].HasValue)
                {
                    error = "Cell already occupied.";
                    return false;
                }
                simulated[cell] = futureStorageIndex + i;
                added.Add((instance, existingContext));
            }
            else if (existingContext != null)
            {
                error = "Invalid context type.";
                return false;
            }
            else
            {
                added.Add((instance, null));
            }
        }

        for (int i = 0; i < added.Count; i++)
        {
            if (added[i].context != null)
                continue;

            int cell = FindFirstAvailableCell(simulated);
            if (cell < 0)
            {
                error = "Not enough empty cells for new instances.";
                return false;
            }

            simulated[cell] = futureStorageIndex + i;
            added[i] = (added[i].instance, GridLayoutContext<TKey>.Single(ToX(cell), ToY(cell)));
        }

        mappedTransaction = new InventoryTransaction<TKey>(
            transaction.Inventory,
            new List<(int index, int delta)>(transaction.AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed),
            added);
        return true;
    }

    private static bool TryCreateAddedCopy(
        InventoryTransaction<TKey> transaction,
        int addedIndex,
        ILayoutContext<TKey> context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out string? error)
    {
        mappedTransaction = null;
        error = null;
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (i == addedIndex)
            {
                if (existingContext is GridLayoutContext<TKey> existingGridContext &&
                    context is GridLayoutContext<TKey> newGridContext &&
                    !existingGridContext.IsMapped &&
                    (existingGridContext.X != newGridContext.X || existingGridContext.Y != newGridContext.Y))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not GridLayoutContext<TKey>)
                {
                    error = "Invalid context type.";
                    return false;
                }
                added.Add((instance, context));
            }
            else
            {
                added.Add((instance, existingContext));
            }
        }

        mappedTransaction = new InventoryTransaction<TKey>(
            transaction.Inventory,
            new List<(int index, int delta)>(transaction.AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed),
            added);
        return true;
    }

    /// <inheritdoc />
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;

        int cell;
        if (context is GridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            if (!IsInRange(gridContext.X, gridContext.Y))
            {
                error = "Grid position out of range.";
                return false;
            }
            cell = ToCellIndex(gridContext.X, gridContext.Y);
        }
        else if (context == null)
        {
            cell = FindFirstAvailableCell(_cellMap);
            if (cell < 0)
            {
                error = "Not enough empty cells for new instances.";
                return false;
            }
        }
        else
        {
            error = "Invalid context type.";
            return false;
        }

        if (_cellMap[cell].HasValue)
        {
            error = "Cell already occupied.";
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            error = "Invalid context type.";
            return false;
        }
        if (!IsInRange(fromContext.X, fromContext.Y) || !IsInRange(toContext.X, toContext.Y))
        {
            error = "Grid position out of range.";
            return false;
        }

        int fromCell = ToCellIndex(fromContext.X, fromContext.Y);
        int toCell = ToCellIndex(toContext.X, toContext.Y);
        if (fromCell == toCell)
        {
            error = "Cannot move item to itself.";
            return false;
        }
        if (!_cellMap[fromCell].HasValue)
        {
            error = "Source cell has no item.";
            return false;
        }
        if (_cellMap[toCell].HasValue)
        {
            error = "Target cell is already occupied.";
            return false;
        }

        _cellMap[toCell] = _cellMap[fromCell];
        _cellMap[fromCell] = null;
        return true;
    }

    /// <inheritdoc />
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            error = "Invalid context type.";
            return false;
        }
        if (!IsInRange(fromContext.X, fromContext.Y) || !IsInRange(toContext.X, toContext.Y))
        {
            error = "Grid position out of range.";
            return false;
        }

        int fromCell = ToCellIndex(fromContext.X, fromContext.Y);
        int toCell = ToCellIndex(toContext.X, toContext.Y);
        if (fromCell == toCell)
        {
            error = "Cannot swap item with itself.";
            return false;
        }
        if (!_cellMap[fromCell].HasValue || !_cellMap[toCell].HasValue)
        {
            error = "One or both of the cells has no item.";
            return false;
        }

        var temp = _cellMap[fromCell];
        _cellMap[fromCell] = _cellMap[toCell];
        _cellMap[toCell] = temp;
        return true;
    }

    /// <inheritdoc />
    public bool TrySort(Inventory<TKey> inventory, IComparer<ItemInstance<TKey>> comparer, out string? error)
    {
        if (comparer == null)
        {
            error = "Comparer cannot be null.";
            return false;
        }

        var occupied = new List<(int storageIndex, int placementIndex)>();
        int placementIndex = 0;
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            if (_cellMap[cell].HasValue)
                occupied.Add((_cellMap[cell]!.Value, placementIndex));
            placementIndex++;
        }

        occupied.Sort((a, b) =>
        {
            int comparison = comparer.Compare(inventory.Items[a.storageIndex], inventory.Items[b.storageIndex]);
            return comparison != 0 ? comparison : a.placementIndex.CompareTo(b.placementIndex);
        });

        for (int i = 0; i < _cellMap.Count; i++)
            _cellMap[i] = null;

        int occupiedIndex = 0;
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            if (occupiedIndex >= occupied.Count)
                break;

            _cellMap[cell] = occupied[occupiedIndex].storageIndex;
            occupiedIndex++;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        int cell;
        if (context is GridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            if (!IsInRange(gridContext.X, gridContext.Y))
                throw new InvalidOperationException("Grid position out of range.");
            cell = ToCellIndex(gridContext.X, gridContext.Y);
        }
        else if (context == null)
        {
            cell = FindFirstAvailableCell(_cellMap);
        }
        else
        {
            throw new InvalidOperationException("Invalid context type.");
        }

        if (cell < 0)
            throw new InvalidOperationException("Not enough empty cells for new instances.");
        _cellMap[cell] = index;
    }

    /// <inheritdoc />
    public void OnItemRemoved(Inventory<TKey> inventory, int index)
    {
        ApplyRemovalToCellMap(_cellMap, index);
    }

    /// <inheritdoc />
    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        for (int i = 0; i < _cellMap.Count; i++)
            _cellMap[i] = null;
    }

    /// <inheritdoc />
    public ILayoutPersistentData GetPersistentData()
    {
        return new GridLayoutPersistentData
        {
            Width = Width,
            Height = Height,
            PlacementOrder = PlacementOrder,
            CellMap = new List<int?>(_cellMap)
        };
    }

    /// <inheritdoc />
    public void RestorePersistentData(ILayoutPersistentData? persistentData)
    {
        if (persistentData is not GridLayoutPersistentData gridData ||
            gridData.Width != Width ||
            gridData.Height != Height ||
            gridData.PlacementOrder != PlacementOrder ||
            gridData.CellMap == null ||
            gridData.CellMap.Count != _cellMap.Count)
        {
            throw new InvalidOperationException("Invalid layout data");
        }

        _cellMap.Clear();
        _cellMap.AddRange(gridData.CellMap);
    }

    /// <inheritdoc />
    public IInventoryLayout<TKey> Clone()
    {
        var clone = new GridLayout<TKey>(Width, Height, PlacementOrder);
        clone.RestorePersistentData(new GridLayoutPersistentData
        {
            Width = Width,
            Height = Height,
            PlacementOrder = PlacementOrder,
            CellMap = new List<int?>(_cellMap)
        });
        return clone;
    }

    private bool IsInRange(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    private int ToCellIndex(int x, int y)
    {
        return y * Width + x;
    }

    private int ToX(int cellIndex)
    {
        return cellIndex % Width;
    }

    private int ToY(int cellIndex)
    {
        return cellIndex / Width;
    }

    private int FindFirstAvailableCell(List<int?> map)
    {
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            if (!map[cell].HasValue)
                return cell;
        }

        return -1;
    }

    private IEnumerable<int> EnumerateCellIndicesInPlacementOrder()
    {
        if (PlacementOrder == GridPlacementOrder.ColumnMajor)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    yield return ToCellIndex(x, y);
            yield break;
        }

        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return ToCellIndex(x, y);
    }

    private static void ApplyRemovalToCellMap(List<int?> cellMap, int removedIndex)
    {
        for (int i = 0; i < cellMap.Count; i++)
        {
            if (cellMap[i] == removedIndex)
                cellMap[i] = null;
            else if (cellMap[i] > removedIndex)
                cellMap[i]--;
        }
    }

    private static bool TryGetSingleContext(ILayoutContext<TKey> context, out GridLayoutContext<TKey> gridContext)
    {
        if (context is GridLayoutContext<TKey> candidate && !candidate.IsMapped)
        {
            gridContext = candidate;
            return true;
        }

        gridContext = null!;
        return false;
    }
}
