using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes two item instances swapped between layout positions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemSwapped<TKey>
{
    /// <summary>
    /// Gets the first layout context involved in the swap.
    /// </summary>
    public ILayoutContext<TKey> FromPosition { get; }

    /// <summary>
    /// Gets the item instance located at <see cref="FromPosition"/> after the swap.
    /// </summary>
    public ItemInstance<TKey> AfterSwapFromPositionInstance { get; }

    /// <summary>
    /// Gets the second layout context involved in the swap.
    /// </summary>
    public ILayoutContext<TKey> ToPosition { get; }

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
    {
        FromPosition = fromPosition;
        AfterSwapFromPositionInstance = afterSwapFromPositionInstance;
        ToPosition = toPosition;
        AfterSwapToPositionInstance = afterSwapToPositionInstance;
    }
}
