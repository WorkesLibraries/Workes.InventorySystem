using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for entry-based placement at a specific structural index.
/// </summary>
public class EntryLayoutContext<TKey> : ILayoutContext<TKey>
{
    public int TargetIndex { get; }

    public EntryLayoutContext(int targetIndex)
    {
        TargetIndex = targetIndex;
    }
}
