namespace Workes.InventorySystem.Events;

/// <summary>Identifies the high-level workflow that produced an inventory change.</summary>
public enum InventoryChangeOrigin
{
    /// <summary>A normal inventory operation or transaction.</summary>
    Operation,
    /// <summary>An exact portable snapshot restoration.</summary>
    SnapshotExactRestore,
    /// <summary>A lossless portable snapshot reconciliation.</summary>
    SnapshotReconciliation,
    /// <summary>A potentially lossy portable snapshot salvage.</summary>
    SnapshotSalvage
}
