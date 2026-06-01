using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item moved between two layout contexts.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemMoved<TKey>
{
    /// <summary>
    /// Gets the item instance that was moved.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the source layout contexts.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> FromLayoutContexts { get; }

    /// <summary>
    /// Gets the destination layout contexts.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> ToLayoutContexts { get; }

    /// <summary>
    /// Gets the single source layout context, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? FromPosition => FromLayoutContexts.Count == 1 ? FromLayoutContexts[0] : null;

    /// <summary>
    /// Gets the single destination layout context, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? ToPosition => ToLayoutContexts.Count == 1 ? ToLayoutContexts[0] : null;

    /// <summary>
    /// Creates an item-moved event payload.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromPosition">The source layout context.</param>
    /// <param name="toPosition">The destination layout context.</param>
    public ItemMoved(ItemInstance<TKey> instance, ILayoutContext<TKey> fromPosition, ILayoutContext<TKey> toPosition)
        : this(instance, new[] { fromPosition }, new[] { toPosition })
    {
    }

    /// <summary>
    /// Creates an item-moved event payload.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromLayoutContexts">The source layout contexts.</param>
    /// <param name="toLayoutContexts">The destination layout contexts.</param>
    public ItemMoved(
        ItemInstance<TKey> instance,
        IEnumerable<ILayoutContext<TKey>>? fromLayoutContexts,
        IEnumerable<ILayoutContext<TKey>>? toLayoutContexts)
    {
        Instance = instance;
        FromLayoutContexts = fromLayoutContexts != null ? fromLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        ToLayoutContexts = toLayoutContexts != null ? toLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
    }
}
