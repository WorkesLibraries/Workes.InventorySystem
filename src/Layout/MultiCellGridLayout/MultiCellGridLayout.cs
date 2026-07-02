using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Persistence;
using System.ComponentModel;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Fixed-size grid layout where each item occupies a rectangular footprint of cells.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// <see cref="PlacementOrder"/> controls context-less placement and sort repacking scan order.
/// Explicit placement contexts are interpreted through <see cref="DefaultAnchor"/> unless they specify their own anchor.
/// </remarks>
public sealed class MultiCellGridLayout<TKey> : IParameterizedRepackableInventoryLayout<TKey>
{
    /// <inheritdoc />
    public IInventoryLayoutSnapshotCodec<TKey> SnapshotCodec => MultiCellGridLayoutSnapshotCodec<TKey>.Instance;
    private readonly List<int?> _cellMap;
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("width", typeof(int), "Number of cells across the grid."),
            new InventoryParameterDefinition("height", typeof(int), "Number of cells down the grid."),
            new InventoryParameterDefinition("placementOrder", typeof(GridPlacementOrder), "Context-less placement and sort repacking scan order."),
            new InventoryParameterDefinition("defaultAnchor", typeof(GridAnchor), "Default anchor for explicit placement contexts that omit an anchor.")
        };

    /// <summary>
    /// Gets the number of cells across the grid.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of cells down the grid.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the scan order used for context-less placement and sort repacking.
    /// </summary>
    public GridPlacementOrder PlacementOrder { get; }

    /// <summary>
    /// Gets the default anchor used when a placement context does not specify one.
    /// </summary>
    public GridAnchor DefaultAnchor { get; }

    /// <summary>
    /// Gets the footprint provider used for item definitions.
    /// </summary>
    public IGridFootprintProvider<TKey> FootprintProvider { get; }

    /// <summary>
    /// Creates a fixed-size multi-cell grid layout.
    /// </summary>
    /// <param name="width">The number of cells across the grid.</param>
    /// <param name="height">The number of cells down the grid.</param>
    /// <param name="footprintProvider">The provider used to resolve item footprints.</param>
    /// <param name="placementOrder">The automatic placement order.</param>
    /// <param name="defaultAnchor">The default anchor for explicit placement contexts.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than or equal to zero.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="footprintProvider"/> is <see langword="null"/>.</exception>
    public MultiCellGridLayout(
        int width,
        int height,
        IGridFootprintProvider<TKey> footprintProvider,
        GridPlacementOrder placementOrder = GridPlacementOrder.RowMajor,
        GridAnchor defaultAnchor = GridAnchor.TopLeft)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Grid width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Grid height must be greater than zero.");

        Width = width;
        Height = height;
        FootprintProvider = footprintProvider ?? throw new ArgumentNullException(nameof(footprintProvider));
        PlacementOrder = placementOrder;
        DefaultAnchor = defaultAnchor;
        _cellMap = new List<int?>(width * height);
        for (int i = 0; i < width * height; i++)
            _cellMap.Add(null);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayout(
        out IInventoryLayout<TKey>? layout,
        out string? error)
    {
        layout = new MultiCellGridLayout<TKey>(Width, Height, FootprintProvider, PlacementOrder, DefaultAnchor);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayoutWithParameter(
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out string? error)
    {
        layout = null;
        if (!TryResolveConfiguration(
                parameterId,
                value,
                out int width,
                out int height,
                out var placementOrder,
                out var defaultAnchor,
                out error))
        {
            return false;
        }

        layout = new MultiCellGridLayout<TKey>(width, height, FootprintProvider, placementOrder, defaultAnchor);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out string? error)
    {
        layout = null;
        if (!TryResolveConfiguration(
                parameterId,
                value,
                out int width,
                out int height,
                out var placementOrder,
                out var defaultAnchor,
                out error))
        {
            return false;
        }

        var newMap = new List<int?>(width * height);
        for (int i = 0; i < width * height; i++)
            newMap.Add(null);

        for (int oldCell = 0; oldCell < _cellMap.Count; oldCell++)
        {
            if (!_cellMap[oldCell].HasValue)
                continue;

            int x = oldCell % Width;
            int y = oldCell / Width;
            if (x >= width || y >= height)
            {
                error = "Cannot resize multi-cell grid layout because an occupied cell would be outside the new bounds.";
                return false;
            }

            newMap[y * width + x] = _cellMap[oldCell];
        }

        var replacement = new MultiCellGridLayout<TKey>(width, height, FootprintProvider, placementOrder, defaultAnchor);
        replacement.RestorePersistentData(new MultiCellGridLayoutPersistentData
        {
            Width = width,
            Height = height,
            PlacementOrder = placementOrder,
            DefaultAnchor = defaultAnchor,
            CellMap = newMap
        });

        layout = replacement;
        error = null;
        return true;
    }

    private bool TryResolveConfiguration(
        string parameterId,
        object? value,
        out int width,
        out int height,
        out GridPlacementOrder placementOrder,
        out GridAnchor defaultAnchor,
        out string? error)
    {
        width = Width;
        height = Height;
        placementOrder = PlacementOrder;
        defaultAnchor = DefaultAnchor;

        if (parameterId == "width")
        {
            if (value is not int widthValue)
            {
                error = "Parameter 'width' expects value type 'Int32'.";
                return false;
            }

            width = widthValue;
        }
        else if (parameterId == "height")
        {
            if (value is not int heightValue)
            {
                error = "Parameter 'height' expects value type 'Int32'.";
                return false;
            }

            height = heightValue;
        }
        else if (parameterId == "placementOrder")
        {
            if (value is not GridPlacementOrder placementOrderValue)
            {
                error = "Parameter 'placementOrder' expects value type 'GridPlacementOrder'.";
                return false;
            }

            placementOrder = placementOrderValue;
        }
        else if (parameterId == "defaultAnchor")
        {
            if (value is not GridAnchor defaultAnchorValue)
            {
                error = "Parameter 'defaultAnchor' expects value type 'GridAnchor'.";
                return false;
            }

            defaultAnchor = defaultAnchorValue;
        }
        else
        {
            error = $"Parameter '{parameterId}' is not supported by MultiCellGridLayout.";
            return false;
        }

        if (width <= 0)
        {
            error = "Grid width must be greater than zero.";
            return false;
        }

        if (height <= 0)
        {
            error = "Grid height must be greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory) => Width * Height;

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_cellMap.Count);
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
            contexts.Add(MultiCellGridLayoutContext<TKey>.Single(ToX(cell), ToY(cell)));
        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (!TryGetSingleContext(context, out var gridContext))
            return null;
        if (!IsInRange(gridContext.X, gridContext.Y))
            return null;

        var storageIndex = _cellMap[ToCellIndex(gridContext.X, gridContext.Y)];
        if (!storageIndex.HasValue || storageIndex.Value < 0 || storageIndex.Value >= inventory.Items.Count)
            return null;

        return inventory.Items[storageIndex.Value];
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        var cells = new List<int>();
        for (int cell = 0; cell < _cellMap.Count; cell++)
        {
            if (_cellMap[cell] == storageIndex)
                cells.Add(cell);
        }
        if (cells.Count == 0)
            return Array.Empty<ILayoutContext<TKey>>();

        cells.Sort((a, b) =>
        {
            int yComparison = ToY(a).CompareTo(ToY(b));
            return yComparison != 0 ? yComparison : ToX(a).CompareTo(ToX(b));
        });

        var contexts = new List<ILayoutContext<TKey>>(cells.Count);
        foreach (int cell in cells)
            contexts.Add(MultiCellGridLayoutContext<TKey>.Single(ToX(cell), ToY(cell)));
        return contexts;
    }

    /// <inheritdoc />
    public bool TryGetContextForStorageIndex(Inventory<TKey> inventory, int storageIndex, out ILayoutContext<TKey>? context)
    {
        var contexts = GetContextsForStorageIndex(inventory, storageIndex);
        context = contexts.Count > 0 ? contexts[0] : null;
        return context != null;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context)
    {
        if (context is MultiCellGridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            if (!IsInRange(gridContext.X, gridContext.Y))
                yield break;

            var storageIndex = _cellMap[ToCellIndex(gridContext.X, gridContext.Y)];
            if (storageIndex.HasValue)
                yield return storageIndex.Value;
            yield break;
        }

        if (context != null)
            yield break;

        var yielded = new HashSet<int>();
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            var storageIndex = _cellMap[cell];
            if (storageIndex.HasValue && yielded.Add(storageIndex.Value))
                yield return storageIndex.Value;
        }
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out string? error)
    {
        return TrySimulatePlacement(inventory, transaction, out _, out error);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
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

        if (context is not MultiCellGridLayoutContext<TKey> gridContext)
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
                    error = "Grid footprint out of range.";
                    return false;
                }
                if (_cellMap[ToCellIndex(gridContext.X, gridContext.Y)] != transaction.AmountDeltas[0].index)
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

        foreach (var pair in gridContext.AddedEntryAnchors)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                error = "Mapped added entry index out of range.";
                return false;
            }
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (gridContext.AddedEntryAnchors.TryGetValue(i, out var mappedAnchor))
            {
                var mappedContext = mappedAnchor.anchor.HasValue
                    ? MultiCellGridLayoutContext<TKey>.Single(mappedAnchor.x, mappedAnchor.y, mappedAnchor.anchor.Value)
                    : MultiCellGridLayoutContext<TKey>.Single(mappedAnchor.x, mappedAnchor.y);
                if (existingContext is MultiCellGridLayoutContext<TKey> existingGridContext &&
                    !existingGridContext.IsMapped &&
                    (existingGridContext.X != mappedAnchor.x ||
                     existingGridContext.Y != mappedAnchor.y ||
                     existingGridContext.Anchor != mappedAnchor.anchor))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not MultiCellGridLayoutContext<TKey>)
                {
                    error = "Invalid context type.";
                    return false;
                }
                added.Add((instance, mappedContext));
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error)
    {
        var footprint = FootprintProvider.GetFootprint(instance.Definition);
        if (context is MultiCellGridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            var (x, y) = ResolveTopLeft(gridContext.X, gridContext.Y, footprint, gridContext.Anchor);
            if (!CanPlaceFootprint(_cellMap, x, y, footprint))
            {
                error = IsFootprintInRange(x, y, footprint)
                    ? "Grid cells already occupied."
                    : "Grid footprint out of range.";
                return false;
            }

            error = null;
            return true;
        }

        if (context != null)
        {
            error = "Invalid context type.";
            return false;
        }

        if (!TryFindFirstAnchor(_cellMap, footprint, out _, out _))
        {
            error = "Not enough empty grid space for new instances.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            error = "Invalid context type.";
            return false;
        }
        if (!IsInRange(fromContext.X, fromContext.Y) || !IsInRange(toContext.X, toContext.Y))
        {
            error = "Grid footprint out of range.";
            return false;
        }

        var storageIndex = _cellMap[ToCellIndex(fromContext.X, fromContext.Y)];
        if (!storageIndex.HasValue)
        {
            error = "Source cell has no item.";
            return false;
        }

        var footprint = FootprintProvider.GetFootprint(inventory.Items[storageIndex.Value].Definition);
        var (targetX, targetY) = ResolveTopLeft(toContext.X, toContext.Y, footprint, toContext.Anchor);
        var simulated = new List<int?>(_cellMap);
        ClearStorageIndex(simulated, storageIndex.Value);
        if (!CanPlaceFootprint(simulated, targetX, targetY, footprint))
        {
            error = IsFootprintInRange(targetX, targetY, footprint)
                ? "Grid cells already occupied."
                : "Grid footprint out of range.";
            return false;
        }

        _cellMap.Clear();
        _cellMap.AddRange(simulated);
        PlaceFootprint(_cellMap, targetX, targetY, footprint, storageIndex.Value);
        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = "Layout does not support swapping multi-cell items.";
        return false;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySort(Inventory<TKey> inventory, IInventorySortContext<TKey> sortContext, out string? error)
    {
        MultiCellGridSortPriority priority;
        IComparer<ItemInstance<TKey>>? comparer;
        if (sortContext is ItemSortContext<TKey> itemSortContext)
        {
            priority = MultiCellGridSortPriority.ItemOrder;
            comparer = itemSortContext.Comparer;
        }
        else if (sortContext is MultiCellGridSortContext<TKey> multiCellSortContext)
        {
            priority = multiCellSortContext.Priority;
            comparer = multiCellSortContext.Comparer;
        }
        else
        {
            error = "Invalid sort context type.";
            return false;
        }

        var occupied = new List<(int storageIndex, int placementIndex)>();
        var seen = new HashSet<int>();
        int placementIndex = 0;
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            if (_cellMap[cell].HasValue && seen.Add(_cellMap[cell]!.Value))
                occupied.Add((_cellMap[cell]!.Value, placementIndex));
            placementIndex++;
        }

        occupied.Sort((a, b) =>
        {
            if (priority == MultiCellGridSortPriority.SpaceEfficiency)
            {
                var aFootprint = FootprintProvider.GetFootprint(inventory.Items[a.storageIndex].Definition);
                var bFootprint = FootprintProvider.GetFootprint(inventory.Items[b.storageIndex].Definition);
                int areaComparison = (bFootprint.Width * bFootprint.Height).CompareTo(aFootprint.Width * aFootprint.Height);
                if (areaComparison != 0)
                    return areaComparison;
                int heightComparison = bFootprint.Height.CompareTo(aFootprint.Height);
                if (heightComparison != 0)
                    return heightComparison;
                int widthComparison = bFootprint.Width.CompareTo(aFootprint.Width);
                if (widthComparison != 0)
                    return widthComparison;
            }

            int comparison = comparer?.Compare(inventory.Items[a.storageIndex], inventory.Items[b.storageIndex]) ?? 0;
            return comparison != 0 ? comparison : a.placementIndex.CompareTo(b.placementIndex);
        });

        var simulated = EmptyMap();
        foreach (var item in occupied)
        {
            var footprint = FootprintProvider.GetFootprint(inventory.Items[item.storageIndex].Definition);
            if (!TryFindFirstAnchor(simulated, footprint, out int x, out int y))
            {
                error = "Not enough empty grid space for sorted layout.";
                return false;
            }

            PlaceFootprint(simulated, x, y, footprint, item.storageIndex);
        }

        _cellMap.Clear();
        _cellMap.AddRange(simulated);
        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        var footprint = FootprintProvider.GetFootprint(inventory.Items[index].Definition);
        int x;
        int y;
        if (context is MultiCellGridLayoutContext<TKey> gridContext && !gridContext.IsMapped)
        {
            (x, y) = ResolveTopLeft(gridContext.X, gridContext.Y, footprint, gridContext.Anchor);
        }
        else if (context == null)
        {
            if (!TryFindFirstAnchor(_cellMap, footprint, out x, out y))
                throw new InvalidOperationException("Not enough empty grid space for new instances.");
        }
        else
        {
            throw new InvalidOperationException("Invalid context type.");
        }

        if (!CanPlaceFootprint(_cellMap, x, y, footprint))
            throw new InvalidOperationException("Grid cells already occupied.");
        PlaceFootprint(_cellMap, x, y, footprint, index);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemRemoved(Inventory<TKey> inventory, int index)
    {
        ClearStorageIndex(_cellMap, index);
        for (int i = 0; i < _cellMap.Count; i++)
        {
            if (_cellMap[i] > index)
                _cellMap[i]--;
        }
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        for (int i = 0; i < _cellMap.Count; i++)
            _cellMap[i] = null;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ILayoutPersistentData GetPersistentData()
    {
        return new MultiCellGridLayoutPersistentData
        {
            Width = Width,
            Height = Height,
            PlacementOrder = PlacementOrder,
            DefaultAnchor = DefaultAnchor,
            CellMap = new List<int?>(_cellMap)
        };
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RestorePersistentData(ILayoutPersistentData? persistentData)
    {
        if (persistentData is not MultiCellGridLayoutPersistentData data ||
            data.Width != Width ||
            data.Height != Height ||
            data.PlacementOrder != PlacementOrder ||
            data.DefaultAnchor != DefaultAnchor ||
            data.CellMap == null ||
            data.CellMap.Count != _cellMap.Count)
        {
            throw new InvalidOperationException("Invalid layout data");
        }

        _cellMap.Clear();
        _cellMap.AddRange(data.CellMap);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IInventoryLayout<TKey> Clone()
    {
        var clone = new MultiCellGridLayout<TKey>(Width, Height, FootprintProvider, PlacementOrder, DefaultAnchor);
        clone.RestorePersistentData(new MultiCellGridLayoutPersistentData
        {
            Width = Width,
            Height = Height,
            PlacementOrder = PlacementOrder,
            DefaultAnchor = DefaultAnchor,
            CellMap = new List<int?>(_cellMap)
        });
        return clone;
    }

    private bool TrySimulatePlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out List<int?>? simulated, out string? error)
    {
        simulated = null;
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

        simulated = new List<int?>(_cellMap);
        var removed = new List<int>(removedIndices);
        removed.Sort((a, b) => b.CompareTo(a));
        foreach (int removedIndex in removed)
        {
            ClearStorageIndex(simulated, removedIndex);
            for (int i = 0; i < simulated.Count; i++)
            {
                if (simulated[i] > removedIndex)
                    simulated[i]--;
            }
        }

        int futureStorageIndex = inventory.Items.Count - removed.Count;
        for (int addedIndex = 0; addedIndex < transaction.Added.Count; addedIndex++)
        {
            var (instance, context) = transaction.Added[addedIndex];
            var footprint = FootprintProvider.GetFootprint(instance.Definition);
            int x;
            int y;
            if (context is MultiCellGridLayoutContext<TKey> gridContext)
            {
                if (gridContext.IsMapped)
                {
                    error = "Invalid context type.";
                    return false;
                }
                (x, y) = ResolveTopLeft(gridContext.X, gridContext.Y, footprint, gridContext.Anchor);
            }
            else if (context == null)
            {
                if (!TryFindFirstAnchor(simulated, footprint, out x, out y))
                {
                    error = "Not enough empty grid space for new instances.";
                    return false;
                }
            }
            else
            {
                error = "Invalid context type.";
                return false;
            }

            if (!CanPlaceFootprint(simulated, x, y, footprint))
            {
                error = IsFootprintInRange(x, y, footprint)
                    ? "Grid cells already occupied."
                    : "Grid footprint out of range.";
                return false;
            }

            PlaceFootprint(simulated, x, y, footprint, futureStorageIndex + addedIndex);
        }

        return true;
    }

    private (int topLeftX, int topLeftY) ResolveTopLeft(int x, int y, GridFootprint footprint, GridAnchor? contextAnchor)
    {
        var anchor = contextAnchor ?? DefaultAnchor;
        return anchor switch
        {
            GridAnchor.TopRight => (x - footprint.Width + 1, y),
            GridAnchor.BottomLeft => (x, y - footprint.Height + 1),
            GridAnchor.BottomRight => (x - footprint.Width + 1, y - footprint.Height + 1),
            _ => (x, y)
        };
    }

    private bool IsInRange(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    private bool IsFootprintInRange(int x, int y, GridFootprint footprint)
    {
        return x >= 0 && y >= 0 && x + footprint.Width <= Width && y + footprint.Height <= Height;
    }

    private bool CanPlaceFootprint(List<int?> map, int x, int y, GridFootprint footprint)
    {
        if (!IsFootprintInRange(x, y, footprint))
            return false;

        for (int dy = 0; dy < footprint.Height; dy++)
            for (int dx = 0; dx < footprint.Width; dx++)
                if (map[ToCellIndex(x + dx, y + dy)].HasValue)
                    return false;

        return true;
    }

    private void PlaceFootprint(List<int?> map, int x, int y, GridFootprint footprint, int storageIndex)
    {
        for (int dy = 0; dy < footprint.Height; dy++)
            for (int dx = 0; dx < footprint.Width; dx++)
                map[ToCellIndex(x + dx, y + dy)] = storageIndex;
    }

    private bool TryFindFirstAnchor(List<int?> map, GridFootprint footprint, out int x, out int y)
    {
        foreach (int cell in EnumerateCellIndicesInPlacementOrder())
        {
            x = ToX(cell);
            y = ToY(cell);
            if (CanPlaceFootprint(map, x, y, footprint))
                return true;
        }

        x = -1;
        y = -1;
        return false;
    }

    private List<int?> EmptyMap()
    {
        var map = new List<int?>(_cellMap.Count);
        for (int i = 0; i < _cellMap.Count; i++)
            map.Add(null);
        return map;
    }

    private static void ClearStorageIndex(List<int?> map, int storageIndex)
    {
        for (int i = 0; i < map.Count; i++)
        {
            if (map[i] == storageIndex)
                map[i] = null;
        }
    }

    private int ToCellIndex(int x, int y) => y * Width + x;

    private int ToX(int cellIndex) => cellIndex % Width;

    private int ToY(int cellIndex) => cellIndex / Width;

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

    private static bool TryGetSingleContext(ILayoutContext<TKey> context, out MultiCellGridLayoutContext<TKey> gridContext)
    {
        if (context is MultiCellGridLayoutContext<TKey> candidate && !candidate.IsMapped)
        {
            gridContext = candidate;
            return true;
        }

        gridContext = null!;
        return false;
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
                if (existingContext is MultiCellGridLayoutContext<TKey> existingGridContext &&
                    context is MultiCellGridLayoutContext<TKey> newGridContext &&
                    !existingGridContext.IsMapped &&
                    (existingGridContext.X != newGridContext.X ||
                     existingGridContext.Y != newGridContext.Y ||
                     existingGridContext.Anchor != newGridContext.Anchor))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not MultiCellGridLayoutContext<TKey>)
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
}
