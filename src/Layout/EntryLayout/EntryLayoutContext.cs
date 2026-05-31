using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for entry-based placement at a specific structural index.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class EntryLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the target entry position.
    /// </summary>
    public int TargetIndex { get; }

    /// <summary>
    /// Creates an entry layout context.
    /// </summary>
    /// <param name="targetIndex">The target entry position.</param>
    public EntryLayoutContext(int targetIndex)
    {
        TargetIndex = targetIndex;
    }
}
