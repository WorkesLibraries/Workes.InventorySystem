using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item instance removed from an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemRemoved<TKey>
{
    /// <summary>
    /// Gets the item instance that was removed.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the storage index the item occupied before removal.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the layout contexts the item occupied before removal.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> LayoutContexts { get; }

    /// <summary>
    /// Gets the single layout context the item occupied before removal, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? LayoutContext => LayoutContexts.Count == 1 ? LayoutContexts[0] : null;

    /// <summary>
    /// Creates an item-removed event payload.
    /// </summary>
    /// <param name="instance">The item instance that was removed.</param>
    /// <param name="index">The storage index the item occupied before removal.</param>
    /// <param name="layoutContext">The layout context the item occupied before removal, when available.</param>
    public ItemRemoved(ItemInstance<TKey> instance, int index, ILayoutContext<TKey>? layoutContext = null)
        : this(instance, index, layoutContext != null ? new[] { layoutContext } : null)
    {
    }

    /// <summary>
    /// Creates an item-removed event payload.
    /// </summary>
    /// <param name="instance">The item instance that was removed.</param>
    /// <param name="index">The storage index the item occupied before removal.</param>
    /// <param name="layoutContexts">The layout contexts the item occupied before removal.</param>
    public ItemRemoved(ItemInstance<TKey> instance, int index, IEnumerable<ILayoutContext<TKey>>? layoutContexts)
    {
        Instance = instance;
        Index = index;
        LayoutContexts = layoutContexts != null ? layoutContexts.ToList() : new List<ILayoutContext<TKey>>();
    }
}
