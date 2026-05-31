using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

public class SlotLayoutContext<TKey> : ILayoutContext<TKey>
{
    public int SlotIndex { get; }

    public SlotLayoutContext(int slotIndex)
    {
        SlotIndex = slotIndex;
    }
}
