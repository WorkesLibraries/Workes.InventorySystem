using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item instance whose amount changed.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemModified<TKey>
{
    /// <summary>
    /// Gets the item instance after modification.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the storage index of the modified item instance.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates an item-modified event payload.
    /// </summary>
    /// <param name="instance">The item instance after modification.</param>
    /// <param name="index">The storage index of the modified item instance.</param>
    public ItemModified(ItemInstance<TKey> instance, int index)
    {
        Instance = instance;
        Index = index;
    }
}
