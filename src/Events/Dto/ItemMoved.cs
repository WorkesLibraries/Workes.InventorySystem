using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Events.Dto;

public class ItemMoved<TKey>
{
    public ItemInstance<TKey> Instance { get; }
    public ILayoutContext<TKey> FromPosition { get; }
    public ILayoutContext<TKey> ToPosition { get; }

    public ItemMoved(ItemInstance<TKey> instance, ILayoutContext<TKey> fromPosition, ILayoutContext<TKey> toPosition)
    {
        Instance = instance;
        FromPosition = fromPosition;
        ToPosition = toPosition;
    }
}
