using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;

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
    /// Gets why this movement occurred.
    /// </summary>
    public ItemMovementCause Cause { get; }

    /// <summary>
    /// Gets whether the movement was produced automatically rather than by directly moving this item.
    /// </summary>
    public bool IsAutomatic => Cause != ItemMovementCause.ExplicitMove;

    /// <summary>
    /// Gets whether this movement was produced by inventory layout sorting.
    /// </summary>
    /// <remarks>Use <see cref="Cause"/> for new integrations.</remarks>
    [Obsolete("Use Cause == ItemMovementCause.Sort instead.")]
    public bool IsSortResult => Cause == ItemMovementCause.Sort;

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
        : this(
            instance,
            new[] { fromPosition },
            new[] { toPosition },
            isSortResult ? ItemMovementCause.Sort : ItemMovementCause.ExplicitMove)
    {
    }

    /// <summary>
    /// Creates an item-moved event payload with an explicit movement cause.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromPosition">The source layout context.</param>
    /// <param name="toPosition">The destination layout context.</param>
    /// <param name="cause">Why the movement occurred.</param>
    public ItemMoved(
        ItemInstance<TKey> instance,
        ILayoutContext<TKey> fromPosition,
        ILayoutContext<TKey> toPosition,
        ItemMovementCause cause)
        : this(instance, new[] { fromPosition }, new[] { toPosition }, cause)
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
        : this(
            instance,
            fromLayoutContexts,
            toLayoutContexts,
            isSortResult ? ItemMovementCause.Sort : ItemMovementCause.ExplicitMove)
    {
    }

    /// <summary>
    /// Creates an item-moved event payload with an explicit movement cause.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromLayoutContexts">The source layout contexts.</param>
    /// <param name="toLayoutContexts">The destination layout contexts.</param>
    /// <param name="cause">Why the movement occurred.</param>
    public ItemMoved(
        ItemInstance<TKey> instance,
        IEnumerable<ILayoutContext<TKey>>? fromLayoutContexts,
        IEnumerable<ILayoutContext<TKey>>? toLayoutContexts,
        ItemMovementCause cause)
    {
        Instance = instance;
        FromLayoutContexts = fromLayoutContexts != null ? fromLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        ToLayoutContexts = toLayoutContexts != null ? toLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        Cause = cause;
    }
}
