using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Events.Dto;

public class ItemAdded<TKey>
{
    public ItemInstance<TKey> Instance { get; }
    public int Index { get; }

    public ItemAdded(ItemInstance<TKey> instance, int index)
    {
        Instance = instance;
        Index = index;
    }
}
