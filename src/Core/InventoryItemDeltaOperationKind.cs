namespace Workes.InventorySystem.Core;

/// <summary>
/// Identifies whether an <see cref="InventoryItemDelta{TKey}"/> operation adds or removes items from the inventory the
/// delta is applied to.
/// </summary>
public enum InventoryItemDeltaOperationKind
{
    /// <summary>Items are added to the inventory the delta is applied to.</summary>
    Add,

    /// <summary>Items are removed from the inventory the delta is applied to.</summary>
    Remove
}
