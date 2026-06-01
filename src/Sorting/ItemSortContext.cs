using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Sorting;

/// <summary>
/// Sort context that orders placed items with an item comparer.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is the default cross-layout sort context. Layout sorting changes layout
/// placement only and never mutates inventory storage order.
/// </remarks>
public sealed class ItemSortContext<TKey> : IInventorySortContext<TKey>
{
    /// <summary>
    /// Gets the comparer used to order item instances.
    /// </summary>
    public IComparer<ItemInstance<TKey>> Comparer { get; }

    /// <summary>
    /// Creates an item-comparer sort context.
    /// </summary>
    /// <param name="comparer">The comparer used to order item instances.</param>
    /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is <see langword="null"/>.</exception>
    public ItemSortContext(IComparer<ItemInstance<TKey>> comparer)
    {
        Comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    /// <summary>
    /// Creates an item-comparer sort context from a comparison delegate.
    /// </summary>
    /// <param name="comparison">The comparison used to order item instances.</param>
    /// <returns>An item sort context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is <see langword="null"/>.</exception>
    public static ItemSortContext<TKey> FromComparison(Comparison<ItemInstance<TKey>> comparison)
    {
        if (comparison == null)
            throw new ArgumentNullException(nameof(comparison));

        return new ItemSortContext<TKey>(Comparer<ItemInstance<TKey>>.Create(comparison));
    }
}
