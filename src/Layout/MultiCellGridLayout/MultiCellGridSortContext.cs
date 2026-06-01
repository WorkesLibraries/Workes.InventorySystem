using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Sort context for multi-cell grid item-order or compact-space sorting.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Compact sorting uses a deterministic footprint-packing heuristic; it is not
/// guaranteed to find an optimal bin-packing result.
/// </remarks>
public sealed class MultiCellGridSortContext<TKey> : IInventorySortContext<TKey>
{
    /// <summary>
    /// Gets the primary sorting objective.
    /// </summary>
    public MultiCellGridSortPriority Priority { get; }

    /// <summary>
    /// Gets the item comparer used for item ordering or compact-sort tie-breaking.
    /// </summary>
    public IComparer<ItemInstance<TKey>>? Comparer { get; }

    /// <summary>
    /// Creates a multi-cell grid sort context.
    /// </summary>
    /// <param name="priority">The primary sorting objective.</param>
    /// <param name="comparer">Optional comparer. Required for item-order sorting and used as a tie-breaker for compact sorting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is <see langword="null"/> when <paramref name="priority"/> is <see cref="MultiCellGridSortPriority.ItemOrder"/>.</exception>
    public MultiCellGridSortContext(
        MultiCellGridSortPriority priority,
        IComparer<ItemInstance<TKey>>? comparer = null)
    {
        if (priority == MultiCellGridSortPriority.ItemOrder && comparer == null)
            throw new ArgumentNullException(nameof(comparer));

        Priority = priority;
        Comparer = comparer;
    }

    /// <summary>
    /// Creates an item-order multi-cell grid sort context.
    /// </summary>
    /// <param name="comparer">The comparer used to order items.</param>
    /// <returns>A multi-cell grid sort context.</returns>
    public static MultiCellGridSortContext<TKey> ByItems(IComparer<ItemInstance<TKey>> comparer)
    {
        return new MultiCellGridSortContext<TKey>(MultiCellGridSortPriority.ItemOrder, comparer ?? throw new ArgumentNullException(nameof(comparer)));
    }

    /// <summary>
    /// Creates an item-order multi-cell grid sort context.
    /// </summary>
    /// <param name="comparison">The comparison used to order items.</param>
    /// <returns>A multi-cell grid sort context.</returns>
    public static MultiCellGridSortContext<TKey> ByItems(Comparison<ItemInstance<TKey>> comparison)
    {
        if (comparison == null)
            throw new ArgumentNullException(nameof(comparison));

        return ByItems(Comparer<ItemInstance<TKey>>.Create(comparison));
    }

    /// <summary>
    /// Creates a compact-space multi-cell grid sort context.
    /// </summary>
    /// <param name="tieBreaker">Optional item comparer used when footprint priority ties.</param>
    /// <returns>A multi-cell grid sort context.</returns>
    public static MultiCellGridSortContext<TKey> Compact(IComparer<ItemInstance<TKey>>? tieBreaker = null)
    {
        return new MultiCellGridSortContext<TKey>(MultiCellGridSortPriority.SpaceEfficiency, tieBreaker);
    }

    /// <summary>
    /// Creates a compact-space multi-cell grid sort context.
    /// </summary>
    /// <param name="tieBreaker">Comparison used when footprint priority ties.</param>
    /// <returns>A multi-cell grid sort context.</returns>
    public static MultiCellGridSortContext<TKey> Compact(Comparison<ItemInstance<TKey>> tieBreaker)
    {
        if (tieBreaker == null)
            throw new ArgumentNullException(nameof(tieBreaker));

        return Compact(Comparer<ItemInstance<TKey>>.Create(tieBreaker));
    }
}
