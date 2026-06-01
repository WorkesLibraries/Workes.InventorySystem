namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines the primary objective for multi-cell grid sorting.
/// </summary>
public enum MultiCellGridSortPriority
{
    /// <summary>
    /// Prioritizes the provided item comparer, then repacks in that item order.
    /// </summary>
    ItemOrder,

    /// <summary>
    /// Prioritizes deterministic compact footprint packing, using the comparer only as a tie-breaker.
    /// </summary>
    SpaceEfficiency
}
