using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes two item instances swapped between layout contexts.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemSwapped<TKey>
{
    /// <summary>
    /// Gets the first layout contexts involved in the swap.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> FirstLayoutContexts { get; }

    /// <summary>
    /// Gets the second layout contexts involved in the swap.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> SecondLayoutContexts { get; }

    /// <summary>
    /// Gets every layout context affected by the swap.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> AffectedLayoutContexts { get; }

    /// <summary>
    /// Gets the single first layout context involved in the swap, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? FromPosition => FirstLayoutContexts.Count == 1 ? FirstLayoutContexts[0] : null;

    /// <summary>
    /// Gets the item instance located at <see cref="FromPosition"/> after the swap.
    /// </summary>
    public ItemInstance<TKey> AfterSwapFromPositionInstance { get; }

    /// <summary>
    /// Gets the single second layout context involved in the swap, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? ToPosition => SecondLayoutContexts.Count == 1 ? SecondLayoutContexts[0] : null;

    /// <summary>
    /// Gets the item instance located at <see cref="ToPosition"/> after the swap.
    /// </summary>
    public ItemInstance<TKey> AfterSwapToPositionInstance { get; }

    /// <summary>
    /// Creates an item-swapped event payload.
    /// </summary>
    /// <param name="fromPosition">The first layout context involved in the swap.</param>
    /// <param name="toPosition">The second layout context involved in the swap.</param>
    /// <param name="afterSwapFromPositionInstance">The item instance located at <paramref name="fromPosition"/> after the swap.</param>
    /// <param name="afterSwapToPositionInstance">The item instance located at <paramref name="toPosition"/> after the swap.</param>
    public ItemSwapped(ILayoutContext<TKey> fromPosition, ILayoutContext<TKey> toPosition, ItemInstance<TKey> afterSwapFromPositionInstance, ItemInstance<TKey> afterSwapToPositionInstance)
        : this(
            new[] { fromPosition },
            new[] { toPosition },
            afterSwapFromPositionInstance,
            afterSwapToPositionInstance)
    {
    }

    /// <summary>
    /// Creates an item-swapped event payload.
    /// </summary>
    /// <param name="firstLayoutContexts">The first layout contexts involved in the swap.</param>
    /// <param name="secondLayoutContexts">The second layout contexts involved in the swap.</param>
    /// <param name="afterSwapFromPositionInstance">The item instance located at the first contexts after the swap.</param>
    /// <param name="afterSwapToPositionInstance">The item instance located at the second contexts after the swap.</param>
    public ItemSwapped(
        IEnumerable<ILayoutContext<TKey>>? firstLayoutContexts,
        IEnumerable<ILayoutContext<TKey>>? secondLayoutContexts,
        ItemInstance<TKey> afterSwapFromPositionInstance,
        ItemInstance<TKey> afterSwapToPositionInstance)
    {
        FirstLayoutContexts = firstLayoutContexts != null ? firstLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        SecondLayoutContexts = secondLayoutContexts != null ? secondLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        AffectedLayoutContexts = FirstLayoutContexts.Concat(SecondLayoutContexts).Distinct().ToList();
        AfterSwapFromPositionInstance = afterSwapFromPositionInstance;
        AfterSwapToPositionInstance = afterSwapToPositionInstance;
    }
}
