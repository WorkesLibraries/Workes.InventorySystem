using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Persistence;

/// <summary>Identifies the snapshot application workflow.</summary>
public enum SnapshotApplicationMode
{
    /// <summary>Preserve stacks, storage order, metadata, and layout placement exactly.</summary>
    Exact,
    /// <summary>Preserve all quantities while adapting stacking and automatic placement.</summary>
    Reconcile,
    /// <summary>Retain a deterministic best-effort subset.</summary>
    Salvage
}

/// <summary>Describes the successful semantic result of snapshot application.</summary>
public enum SnapshotApplicationOutcome
{
    /// <summary>The snapshot was reproduced exactly.</summary>
    Exact,
    /// <summary>All quantities were retained with semantic shape changes.</summary>
    Reconciled,
    /// <summary>Some snapshot quantity was intentionally discarded.</summary>
    Salvaged
}

/// <summary>Classifies a structured snapshot assessment or application issue.</summary>
public enum SnapshotIssueCode
{
    /// <summary>The serializer-facing snapshot graph is invalid.</summary>
    MalformedSnapshot,
    /// <summary>A required key or value codec is unavailable or rejected data.</summary>
    UnsupportedCodec,
    /// <summary>A definition ID cannot be resolved by the current catalog or migrations.</summary>
    UnknownDefinition,
    /// <summary>A saved or proposed stack exceeds the current stack limit.</summary>
    StackLimit,
    /// <summary>The current capacity policy rejected the replacement.</summary>
    Capacity,
    /// <summary>The current rule set rejected the replacement.</summary>
    Rule,
    /// <summary>The current layout cannot represent the proposed state.</summary>
    Layout,
    /// <summary>Successful salvage intentionally discarded quantity.</summary>
    ItemDiscarded,
    /// <summary>Salvage options are invalid or unsupported.</summary>
    InvalidOptions
}

/// <summary>Determines whether salvage may retain part of a snapshot entry.</summary>
public enum SnapshotSalvageQuantityMode
{
    /// <summary>Retain the largest deterministic quantity found for an entry.</summary>
    AllowPartialQuantity,
    /// <summary>Retain either the complete entry quantity or none of it.</summary>
    WholeEntryOnly
}

/// <summary>Determines how salvage treats definition IDs that the current catalog cannot resolve.</summary>
public enum SnapshotUnknownDefinitionHandling
{
    /// <summary>Treat an unknown definition as operation failure.</summary>
    Fail,
    /// <summary>Allow salvage to report and discard unknown entries.</summary>
    Discard
}

/// <summary>Identifies the deterministic placement strategy used by salvage.</summary>
public enum SnapshotSalvagePlacementStrategy
{
    /// <summary>Attempt entries in deterministic order using current automatic placement.</summary>
    GreedyAutomatic
}

/// <summary>One structured reason produced while assessing or applying a snapshot.</summary>
public sealed class SnapshotIssue
{
    internal SnapshotIssue(SnapshotIssueCode code, string message, string? entryId = null, int quantity = 0)
    {
        Code = code;
        Message = message;
        EntryId = entryId;
        Quantity = quantity;
    }

    /// <summary>Gets the stable issue category.</summary>
    public SnapshotIssueCode Code { get; }
    /// <summary>Gets the human-readable reason.</summary>
    public string Message { get; }
    /// <summary>Gets the related snapshot entry ID, when applicable.</summary>
    public string? EntryId { get; }
    /// <summary>Gets the related quantity, when applicable.</summary>
    public int Quantity { get; }
}

/// <summary>Describes snapshot quantity intentionally omitted by successful salvage.</summary>
public sealed class SnapshotItemLoss
{
    internal SnapshotItemLoss(string entryId, int quantity, string reason)
    {
        EntryId = entryId;
        Quantity = quantity;
        Reason = reason;
    }

    /// <summary>Gets the snapshot-local entry ID.</summary>
    public string EntryId { get; }
    /// <summary>Gets the discarded quantity.</summary>
    public int Quantity { get; }
    /// <summary>Gets the reason the quantity could not be retained.</summary>
    public string Reason { get; }
}

/// <summary>Resolved item information supplied to an optional salvage priority comparer.</summary>
public sealed class SnapshotSalvageCandidate<TKey>
{
    internal SnapshotSalvageCandidate(string entryId, ItemDefinition<TKey> definition, int amount, InstanceMetadata metadata)
    {
        EntryId = entryId;
        Definition = definition;
        Amount = amount;
        Metadata = metadata;
    }

    /// <summary>Gets the snapshot-local entry ID.</summary>
    public string EntryId { get; }
    /// <summary>Gets the catalog-resolved definition.</summary>
    public ItemDefinition<TKey> Definition { get; }
    /// <summary>Gets the original snapshot entry amount.</summary>
    public int Amount { get; }
    /// <summary>Gets detached decoded metadata.</summary>
    public InstanceMetadata Metadata { get; }
}

/// <summary>Controls deterministic, lossy snapshot salvage.</summary>
public sealed class SnapshotSalvageOptions<TKey>
{
    /// <summary>
    /// Gets or sets an optional comparer whose greater values are attempted first.
    /// Equal values retain original snapshot storage order.
    /// </summary>
    public IComparer<SnapshotSalvageCandidate<TKey>>? PriorityComparer { get; set; }

    /// <summary>Gets or sets whether partial entry quantities may be retained.</summary>
    public SnapshotSalvageQuantityMode QuantityMode { get; set; } =
        SnapshotSalvageQuantityMode.AllowPartialQuantity;

    /// <summary>Gets or sets how unknown catalog definitions are handled.</summary>
    public SnapshotUnknownDefinitionHandling UnknownDefinitionHandling { get; set; } =
        SnapshotUnknownDefinitionHandling.Fail;

    /// <summary>Gets or sets the deterministic placement strategy.</summary>
    public SnapshotSalvagePlacementStrategy PlacementStrategy { get; set; } =
        SnapshotSalvagePlacementStrategy.GreedyAutomatic;
}

/// <summary>Non-mutating compatibility assessment for one snapshot and current inventory configuration.</summary>
public sealed class SnapshotAssessmentResult
{
    internal SnapshotAssessmentResult(
        bool canRestoreExactly,
        bool canReconcileWithoutLoss,
        bool canSalvage,
        SnapshotApplicationOutcome? bestOutcome,
        IReadOnlyList<SnapshotIssue> issues,
        IReadOnlyList<SnapshotItemLoss> projectedLosses)
    {
        CanRestoreExactly = canRestoreExactly;
        CanReconcileWithoutLoss = canReconcileWithoutLoss;
        CanSalvage = canSalvage;
        BestOutcome = bestOutcome;
        Issues = issues;
        ProjectedLosses = projectedLosses;
    }

    /// <summary>Gets whether exact restoration currently succeeds.</summary>
    public bool CanRestoreExactly { get; }
    /// <summary>Gets whether reconciliation currently retains every quantity.</summary>
    public bool CanReconcileWithoutLoss { get; }
    /// <summary>Gets whether salvage currently produces a valid final inventory.</summary>
    public bool CanSalvage { get; }
    /// <summary>Gets the strongest currently available outcome.</summary>
    public SnapshotApplicationOutcome? BestOutcome { get; }
    /// <summary>Gets structured reasons encountered while evaluating stronger outcomes.</summary>
    public IReadOnlyList<SnapshotIssue> Issues { get; }
    /// <summary>Gets quantity losses projected by the salvage plan.</summary>
    public IReadOnlyList<SnapshotItemLoss> ProjectedLosses { get; }
}

/// <summary>Successful result shared by exact, reconciliation, and salvage application.</summary>
public sealed class SnapshotApplicationResult
{
    internal SnapshotApplicationResult(
        SnapshotApplicationMode mode,
        SnapshotApplicationOutcome outcome,
        IReadOnlyList<SnapshotItemLoss> losses,
        IReadOnlyList<SnapshotIssue> issues,
        int restoredInstanceCount,
        int restoredQuantity)
    {
        Mode = mode;
        Outcome = outcome;
        Losses = losses;
        Issues = issues;
        RestoredInstanceCount = restoredInstanceCount;
        RestoredQuantity = restoredQuantity;
    }

    /// <summary>Gets the workflow requested by the caller.</summary>
    public SnapshotApplicationMode Mode { get; }
    /// <summary>Gets the semantic result that was committed.</summary>
    public SnapshotApplicationOutcome Outcome { get; }
    /// <summary>Gets quantity discarded by successful salvage.</summary>
    public IReadOnlyList<SnapshotItemLoss> Losses { get; }
    /// <summary>Gets structured informational issues, including successful salvage losses.</summary>
    public IReadOnlyList<SnapshotIssue> Issues { get; }
    /// <summary>Gets the final number of item instances.</summary>
    public int RestoredInstanceCount { get; }
    /// <summary>Gets the final total item quantity.</summary>
    public int RestoredQuantity { get; }
    /// <summary>Gets whether successful application discarded snapshot quantity.</summary>
    public bool HasLosses => Losses.Count > 0;
}
