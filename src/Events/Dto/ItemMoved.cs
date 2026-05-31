using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item moved between two layout positions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemMoved<TKey>
{
    /// <summary>
    /// Gets the item instance that was moved.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the source layout context.
    /// </summary>
    public ILayoutContext<TKey> FromPosition { get; }

    /// <summary>
    /// Gets the destination layout context.
    /// </summary>
    public ILayoutContext<TKey> ToPosition { get; }

    /// <summary>
    /// Creates an item-moved event payload.
    /// </summary>
    /// <param name="instance">The item instance that was moved.</param>
    /// <param name="fromPosition">The source layout context.</param>
    /// <param name="toPosition">The destination layout context.</param>
    public ItemMoved(ItemInstance<TKey> instance, ILayoutContext<TKey> fromPosition, ILayoutContext<TKey> toPosition)
    {
        Instance = instance;
        FromPosition = fromPosition;
        ToPosition = toPosition;
    }
}
