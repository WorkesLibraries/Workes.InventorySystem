using Workes.InventorySystem.Core;
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
    /// Creates an item-removed event payload.
    /// </summary>
    /// <param name="instance">The item instance that was removed.</param>
    /// <param name="index">The storage index the item occupied before removal.</param>
    public ItemRemoved(ItemInstance<TKey> instance, int index)
    {
        Instance = instance;
        Index = index;
    }
}
