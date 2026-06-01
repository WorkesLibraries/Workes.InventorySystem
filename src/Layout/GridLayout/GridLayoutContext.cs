using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for fixed-grid placement or transaction added-entry mapping.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class GridLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the horizontal cell coordinate addressed by this context.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical cell coordinate addressed by this context.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to grid cells.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets mapped transaction added-entry indices and their target grid cells.
    /// </summary>
    public IReadOnlyDictionary<int, (int x, int y)> AddedEntryCells { get; }

    /// <summary>
    /// Creates a grid layout context.
    /// </summary>
    /// <param name="x">The horizontal cell coordinate.</param>
    /// <param name="y">The vertical cell coordinate.</param>
    public GridLayoutContext(int x, int y)
    {
        X = x;
        Y = y;
        AddedEntryCells = new ReadOnlyDictionary<int, (int x, int y)>(new Dictionary<int, (int x, int y)>());
    }

    private GridLayoutContext(Dictionary<int, (int x, int y)> addedEntryCells)
    {
        X = -1;
        Y = -1;
        IsMapped = true;
        AddedEntryCells = new ReadOnlyDictionary<int, (int x, int y)>(addedEntryCells);
    }

    /// <summary>
    /// Creates a single-cell placement context.
    /// </summary>
    /// <param name="x">The horizontal cell coordinate.</param>
    /// <param name="y">The vertical cell coordinate.</param>
    /// <returns>A context that addresses one grid cell.</returns>
    public static GridLayoutContext<TKey> Single(int x, int y)
    {
        return new GridLayoutContext<TKey>(x, y);
    }

    /// <summary>
    /// Starts a builder for mapping transaction added-entry indices to grid cells.
    /// </summary>
    /// <returns>A grid layout context builder.</returns>
    public static GridLayoutContextBuilder<TKey> Map()
    {
        return new GridLayoutContextBuilder<TKey>();
    }

    internal static GridLayoutContext<TKey> FromMap(Dictionary<int, (int x, int y)> addedEntryCells)
    {
        return new GridLayoutContext<TKey>(addedEntryCells);
    }
}

/// <summary>
/// Builds a grid layout context that maps transaction added-entry indices to cells.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class GridLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, (int x, int y)> _addedEntryCells = new();

    /// <summary>
    /// Maps one transaction added-entry index to a target grid cell.
    /// </summary>
    /// <param name="addedEntryIndex">The index in <see cref="InventoryTransaction{TKey}.Added"/>.</param>
    /// <param name="x">The horizontal cell coordinate.</param>
    /// <param name="y">The vertical cell coordinate.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="addedEntryIndex"/> is already mapped.</exception>
    public GridLayoutContextBuilder<TKey> Add(int addedEntryIndex, int x, int y)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index must be non-negative.");
        if (_addedEntryCells.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _addedEntryCells.Add(addedEntryIndex, (x, y));
        return this;
    }

    /// <summary>
    /// Creates the mapped grid layout context.
    /// </summary>
    /// <returns>A mapped grid layout context.</returns>
    public GridLayoutContext<TKey> Build()
    {
        return GridLayoutContext<TKey>.FromMap(new Dictionary<int, (int x, int y)>(_addedEntryCells));
    }
}
