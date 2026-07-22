namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes how a remove operation matches item-instance metadata.
/// </summary>
public enum InventoryItemDeltaMetadataMatch
{
    /// <summary>The target instance metadata must structurally equal the operation metadata.</summary>
    Exact,

    /// <summary>The target instance metadata is ignored.</summary>
    Any
}
