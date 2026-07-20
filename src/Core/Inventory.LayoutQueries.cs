using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

public partial class Inventory<TKey>
{
    /// <summary>
    /// Gets the number of positions addressable by the active layout for this inventory.
    /// </summary>
    /// <returns>The active layout position count.</returns>
    public int GetLayoutPositionCount() => _layout.GetPositionCount(this);

    /// <summary>
    /// Gets every context addressable by the active layout for this inventory.
    /// </summary>
    /// <returns>The active layout's addressable contexts.</returns>
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableLayoutContexts() =>
        _layout.GetAddressableContexts(this);

    /// <summary>
    /// Gets the item currently presented at a layout context.
    /// </summary>
    /// <param name="context">The layout-specific context to query.</param>
    /// <returns>The item at <paramref name="context"/>, or <see langword="null"/> when the context is empty or invalid for the active layout.</returns>
    public ItemInstance<TKey>? GetItemAt(ILayoutContext<TKey> context) =>
        _layout.GetItemAt(this, context);

    /// <summary>
    /// Gets every layout context currently occupied by the item at a storage index.
    /// </summary>
    /// <param name="storageIndex">The inventory storage index.</param>
    /// <returns>The active layout contexts for the storage index, or an empty list when the index is not represented.</returns>
    public IReadOnlyList<ILayoutContext<TKey>> GetLayoutContextsForStorageIndex(int storageIndex) =>
        _layout.GetContextsForStorageIndex(this, storageIndex);

    /// <summary>
    /// Attempts to get one layout context currently occupied by the item at a storage index.
    /// </summary>
    /// <param name="storageIndex">The inventory storage index.</param>
    /// <param name="context">The first layout context when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when at least one context was found.</returns>
    public bool TryGetLayoutContextForStorageIndex(int storageIndex, out ILayoutContext<TKey>? context) =>
        _layout.TryGetContextForStorageIndex(this, storageIndex, out context);

    /// <summary>
    /// Gets every layout context currently occupied by an owned item instance.
    /// </summary>
    /// <param name="item">The owned item instance to locate.</param>
    /// <returns>The active layout contexts for <paramref name="item"/>, or an empty list when the item is not owned by this inventory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ILayoutContext<TKey>> GetLayoutContextsForItem(ItemInstance<TKey> item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        var storageIndex = _items.IndexOf(item);
        return storageIndex >= 0
            ? GetLayoutContextsForStorageIndex(storageIndex)
            : Array.Empty<ILayoutContext<TKey>>();
    }

    /// <summary>
    /// Attempts to get one layout context currently occupied by an owned item instance.
    /// </summary>
    /// <param name="item">The owned item instance to locate.</param>
    /// <param name="context">The first layout context when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the item is owned by this inventory and has at least one layout context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public bool TryGetLayoutContextForItem(ItemInstance<TKey> item, out ILayoutContext<TKey>? context)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        var storageIndex = _items.IndexOf(item);
        if (storageIndex < 0)
        {
            context = null;
            return false;
        }

        return TryGetLayoutContextForStorageIndex(storageIndex, out context);
    }
}
