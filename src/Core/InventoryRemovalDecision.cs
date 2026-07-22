using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes whether a delta application plan allows a candidate item stack to satisfy a removal.
/// </summary>
public sealed class InventoryRemovalDecision
{
    private InventoryRemovalDecision(bool accepted, InventoryFailure? failure)
    {
        Accepted = accepted;
        Failure = failure;
    }

    /// <summary>Gets whether this candidate may be consumed.</summary>
    public bool Accepted { get; }

    /// <summary>Gets the failure when this decision rejects the complete removal operation.</summary>
    public InventoryFailure? Failure { get; }

    /// <summary>Gets whether this decision rejects the complete removal operation.</summary>
    public bool IsRejected => Failure != null;

    /// <summary>Allows the candidate.</summary>
    public static InventoryRemovalDecision Allow() => new(true, null);

    /// <summary>Skips this candidate while allowing validation to inspect later candidates.</summary>
    public static InventoryRemovalDecision Skip() => new(false, null);

    /// <summary>Rejects the removal with a structured failure.</summary>
    public static InventoryRemovalDecision Reject(InventoryFailure failure)
    {
        if (failure == null)
            throw new ArgumentNullException(nameof(failure));
        return new InventoryRemovalDecision(false, failure);
    }
}
