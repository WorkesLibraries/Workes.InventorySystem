using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
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
    /// Gets the layout context resolved for the added item instance, when available.
    /// </summary>
    public ILayoutContext<TKey>? LayoutContext { get; }

    /// <summary>
    /// Creates an item-added event payload.
    /// </summary>
    /// <param name="instance">The item instance that was added.</param>
    /// <param name="index">The storage index assigned to the item instance.</param>
    /// <param name="layoutContext">The layout context resolved for the added item instance, when available.</param>
    public ItemAdded(ItemInstance<TKey> instance, int index, ILayoutContext<TKey>? layoutContext = null)
    {
        Instance = instance;
        Index = index;
        LayoutContext = layoutContext;
    }
}
