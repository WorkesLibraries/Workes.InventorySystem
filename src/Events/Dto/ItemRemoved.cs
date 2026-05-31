using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Events.Dto;

public class ItemRemoved<TKey>
{
    public ItemInstance<TKey> Instance { get; }
    public int Index { get; }

    public ItemRemoved(ItemInstance<TKey> instance, int index)
    {
        Instance = instance;
        Index = index;
    }
}
