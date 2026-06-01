using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
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
    /// Gets the layout context the item occupied before removal, when available.
    /// </summary>
    public ILayoutContext<TKey>? LayoutContext { get; }

    /// <summary>
    /// Creates an item-removed event payload.
    /// </summary>
    /// <param name="instance">The item instance that was removed.</param>
    /// <param name="index">The storage index the item occupied before removal.</param>
    /// <param name="layoutContext">The layout context the item occupied before removal, when available.</param>
    public ItemRemoved(ItemInstance<TKey> instance, int index, ILayoutContext<TKey>? layoutContext = null)
    {
        Instance = instance;
        Index = index;
        LayoutContext = layoutContext;
    }
}
