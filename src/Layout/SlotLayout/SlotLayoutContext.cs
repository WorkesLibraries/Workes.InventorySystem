using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for fixed-slot placement.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class SlotLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the slot index addressed by this context.
    /// </summary>
    public int SlotIndex { get; }

    /// <summary>
    /// Creates a slot layout context.
    /// </summary>
    /// <param name="slotIndex">The target slot index.</param>
    public SlotLayoutContext(int slotIndex)
    {
        SlotIndex = slotIndex;
    }
}
