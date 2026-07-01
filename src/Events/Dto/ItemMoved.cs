using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item that stayed in the inventory but moved between layout contexts, either directly or through
/// layout reflow caused by another mutation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemMoved<TKey>
{
    /// <summary>
    /// Gets the item instance that was moved.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the source layout contexts. Multi-cell layouts may provide more than one context.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> FromLayoutContexts { get; }

    /// <summary>
    /// Gets the destination layout contexts. Multi-cell layouts may provide more than one context.
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
    /// Gets whether this movement was produced by inventory layout sorting rather than direct movement.
    /// </summary>
    public bool IsSortResult { get; }

    /// <summary>
    /// Creates an item-moved event payload.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromPosition">The source layout context.</param>
    /// <param name="toPosition">The destination layout context.</param>
    /// <param name="isSortResult">Whether this movement was produced by sorting.</param>
    public ItemMoved(
        ItemInstance<TKey> instance,
        ILayoutContext<TKey> fromPosition,
        ILayoutContext<TKey> toPosition,
        bool isSortResult = false)
        : this(instance, new[] { fromPosition }, new[] { toPosition }, isSortResult)
    {
    }

    /// <summary>
    /// Creates an item-moved event payload.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromLayoutContexts">The source layout contexts.</param>
    /// <param name="toLayoutContexts">The destination layout contexts.</param>
    /// <param name="isSortResult">Whether this movement was produced by sorting.</param>
    public ItemMoved(
        ItemInstance<TKey> instance,
        IEnumerable<ILayoutContext<TKey>>? fromLayoutContexts,
        IEnumerable<ILayoutContext<TKey>>? toLayoutContexts,
        bool isSortResult = false)
    {
        Instance = instance;
        FromLayoutContexts = fromLayoutContexts != null ? fromLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        ToLayoutContexts = toLayoutContexts != null ? toLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        IsSortResult = isSortResult;
    }
}
