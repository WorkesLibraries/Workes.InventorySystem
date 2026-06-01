using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Provides anchor-cell placement instructions for a multi-cell grid layout.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class MultiCellGridLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the anchor x coordinate for a single placement context.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the anchor y coordinate for a single placement context.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the footprint anchor represented by <see cref="X"/> and <see cref="Y"/>, or <see langword="null"/> to use the layout default anchor.
    /// </summary>
    public GridAnchor? Anchor { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to anchor cells.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets added-entry index to anchor coordinate mappings.
    /// </summary>
    public IReadOnlyDictionary<int, (int x, int y, GridAnchor? anchor)> AddedEntryAnchors { get; }

    /// <summary>
    /// Creates a single anchor-cell placement context.
    /// </summary>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    public MultiCellGridLayoutContext(int x, int y)
    {
        X = x;
        Y = y;
        Anchor = null;
        IsMapped = false;
        AddedEntryAnchors = new Dictionary<int, (int x, int y, GridAnchor? anchor)>();
    }

    /// <summary>
    /// Creates a single anchor-cell placement context.
    /// </summary>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    /// <param name="anchor">The footprint anchor represented by the coordinate.</param>
    public MultiCellGridLayoutContext(int x, int y, GridAnchor anchor)
    {
        X = x;
        Y = y;
        Anchor = anchor;
        IsMapped = false;
        AddedEntryAnchors = new Dictionary<int, (int x, int y, GridAnchor? anchor)>();
    }

    private MultiCellGridLayoutContext(IReadOnlyDictionary<int, (int x, int y, GridAnchor? anchor)> addedEntryAnchors)
    {
        X = -1;
        Y = -1;
        Anchor = null;
        IsMapped = true;
        AddedEntryAnchors = addedEntryAnchors;
    }

    /// <summary>
    /// Creates a single anchor-cell placement context.
    /// </summary>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    /// <returns>A single placement context.</returns>
    public static MultiCellGridLayoutContext<TKey> Single(int x, int y) => new(x, y);

    /// <summary>
    /// Creates a single anchor-cell placement context.
    /// </summary>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    /// <param name="anchor">The footprint anchor represented by the coordinate.</param>
    /// <returns>A single placement context.</returns>
    public static MultiCellGridLayoutContext<TKey> Single(int x, int y, GridAnchor anchor) => new(x, y, anchor);

    /// <summary>
    /// Creates a builder for transaction added-entry anchor mappings.
    /// </summary>
    /// <returns>A mapping builder.</returns>
    public static MultiCellGridLayoutContextBuilder<TKey> Map() => new();

    internal static MultiCellGridLayoutContext<TKey> FromMap(IReadOnlyDictionary<int, (int x, int y, GridAnchor? anchor)> map)
    {
        return new MultiCellGridLayoutContext<TKey>(map);
    }
}

/// <summary>
/// Builds multi-cell grid mappings from transaction added-entry indices to anchor coordinates.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class MultiCellGridLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, (int x, int y, GridAnchor? anchor)> _map = new();

    /// <summary>
    /// Maps an added-entry index to an anchor coordinate.
    /// </summary>
    /// <param name="addedEntryIndex">The transaction added-entry index.</param>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="addedEntryIndex"/> is already mapped.</exception>
    public MultiCellGridLayoutContextBuilder<TKey> Add(int addedEntryIndex, int x, int y)
    {
        return AddCore(addedEntryIndex, x, y, null);
    }

    /// <summary>
    /// Maps an added-entry index to an anchor coordinate.
    /// </summary>
    /// <param name="addedEntryIndex">The transaction added-entry index.</param>
    /// <param name="x">The anchor x coordinate.</param>
    /// <param name="y">The anchor y coordinate.</param>
    /// <param name="anchor">The footprint anchor represented by the coordinate.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="addedEntryIndex"/> is already mapped.</exception>
    public MultiCellGridLayoutContextBuilder<TKey> Add(int addedEntryIndex, int x, int y, GridAnchor anchor)
    {
        return AddCore(addedEntryIndex, x, y, anchor);
    }

    private MultiCellGridLayoutContextBuilder<TKey> AddCore(int addedEntryIndex, int x, int y, GridAnchor? anchor)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index cannot be negative.");
        if (_map.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _map.Add(addedEntryIndex, (x, y, anchor));
        return this;
    }

    /// <summary>
    /// Builds the mapped multi-cell grid layout context.
    /// </summary>
    /// <returns>A mapped multi-cell grid layout context.</returns>
    public MultiCellGridLayoutContext<TKey> Build()
    {
        return MultiCellGridLayoutContext<TKey>.FromMap(new Dictionary<int, (int x, int y, GridAnchor? anchor)>(_map));
    }
}
