namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes the broad subsystem or reason category for an expected inventory-system failure.
/// </summary>
public enum InventoryFailureKind
{
    /// <summary>The failure could not be classified more precisely.</summary>
    Unknown,
    /// <summary>General validation rejected the requested operation.</summary>
    Validation,
    /// <summary>Definition or registry resolution rejected the requested operation.</summary>
    Definition,
    /// <summary>Metadata validation or mutation rejected the requested operation.</summary>
    Metadata,
    /// <summary>Stacking rules or stack-size resolution rejected the requested operation.</summary>
    Stacking,
    /// <summary>Capacity policy validation rejected the requested operation.</summary>
    Capacity,
    /// <summary>Rule validation rejected the requested operation.</summary>
    Rules,
    /// <summary>Layout validation, placement, movement, or sorting rejected the requested operation.</summary>
    Layout,
    /// <summary>Cross-inventory transfer validation rejected the requested operation.</summary>
    Transfer,
    /// <summary>Transaction formulation, validation, or commit rejected the requested operation.</summary>
    Transaction,
    /// <summary>Legacy persistence or portable persistence rejected the requested operation.</summary>
    Persistence,
    /// <summary>Snapshot capture, validation, assessment, or application rejected the requested operation.</summary>
    Snapshot,
    /// <summary>Runtime configuration or parameter mutation rejected the requested operation.</summary>
    Configuration,
    /// <summary>An extension contract rejected the requested operation.</summary>
    Extension
}
