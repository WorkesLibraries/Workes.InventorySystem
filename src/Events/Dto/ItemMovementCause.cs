namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Identifies why an item instance changed layout contexts.
/// </summary>
public enum ItemMovementCause
{
    /// <summary>
    /// The item was the direct target of an inventory move operation.
    /// </summary>
    ExplicitMove = 0,

    /// <summary>
    /// The layout moved the item while applying sorting instructions.
    /// </summary>
    Sort = 1,

    /// <summary>
    /// The layout moved the item while compacting placement through repack.
    /// </summary>
    Repack = 2,

    /// <summary>
    /// The item moved automatically as collateral layout reflow caused by another mutation.
    /// </summary>
    LayoutReflow = 3
}
