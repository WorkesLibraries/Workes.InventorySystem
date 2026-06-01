namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines which point of an item's footprint is addressed by an explicit multi-cell grid placement context.
/// </summary>
public enum GridAnchor
{
    /// <summary>
    /// The context coordinate is the footprint's top-left cell.
    /// </summary>
    TopLeft,

    /// <summary>
    /// The context coordinate is the footprint's top-right cell.
    /// </summary>
    TopRight,

    /// <summary>
    /// The context coordinate is the footprint's bottom-left cell.
    /// </summary>
    BottomLeft,

    /// <summary>
    /// The context coordinate is the footprint's bottom-right cell.
    /// </summary>
    BottomRight
}
