using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Events.Dto;

public class ItemModified<TKey>
{
    public ItemInstance<TKey> Instance { get; }
    public int Index { get; }

    public ItemModified(ItemInstance<TKey> instance, int index)
    {
        Instance = instance;
        Index = index;
    }
}
