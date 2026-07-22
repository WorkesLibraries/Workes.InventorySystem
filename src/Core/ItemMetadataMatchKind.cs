namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes how an operation matches item-instance metadata when selecting existing items.
/// </summary>
public enum ItemMetadataMatchKind
{
    /// <summary>The target instance metadata must be structurally empty.</summary>
    Empty,

    /// <summary>The target instance metadata must structurally equal the selector metadata.</summary>
    Exact,

    /// <summary>The target instance metadata is ignored.</summary>
    Any
}
