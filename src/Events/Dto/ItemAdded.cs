using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item instance added to an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemAdded<TKey>
{
    /// <summary>
    /// Gets the item instance that was added.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the storage index assigned to the added item instance.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the layout contexts resolved for the added item instance.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> LayoutContexts { get; }

    /// <summary>
    /// Gets the single layout context resolved for the added item instance, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? LayoutContext => LayoutContexts.Count == 1 ? LayoutContexts[0] : null;

    /// <summary>
    /// Creates an item-added event payload.
    /// </summary>
    /// <param name="instance">The item instance that was added.</param>
    /// <param name="index">The storage index assigned to the item instance.</param>
    /// <param name="layoutContext">The layout context resolved for the added item instance, when available.</param>
    public ItemAdded(ItemInstance<TKey> instance, int index, ILayoutContext<TKey>? layoutContext = null)
        : this(instance, index, layoutContext != null ? new[] { layoutContext } : null)
    {
    }

    /// <summary>
    /// Creates an item-added event payload.
    /// </summary>
    /// <param name="instance">The item instance that was added.</param>
    /// <param name="index">The storage index assigned to the item instance.</param>
    /// <param name="layoutContexts">The layout contexts resolved for the added item instance.</param>
    public ItemAdded(ItemInstance<TKey> instance, int index, IEnumerable<ILayoutContext<TKey>>? layoutContexts)
    {
        Instance = instance;
        Index = index;
        LayoutContexts = layoutContexts != null ? layoutContexts.ToList() : new List<ILayoutContext<TKey>>();
    }
}
