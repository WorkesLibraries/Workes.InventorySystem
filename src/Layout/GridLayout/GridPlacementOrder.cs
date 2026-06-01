namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines how a grid layout scans cells for automatic placement and sort repacking.
/// </summary>
public enum GridPlacementOrder
{
    /// <summary>
    /// Searches left-to-right across each row before moving to the next row.
    /// </summary>
    RowMajor,

    /// <summary>
    /// Searches top-to-bottom down each column before moving to the next column.
    /// </summary>
    ColumnMajor
}
