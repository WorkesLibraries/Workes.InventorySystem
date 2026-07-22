using System;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes how a delta application plan should place an added operation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryPlacementDecision<TKey>
{
    private InventoryPlacementDecision(ILayoutContext<TKey>? context, InventoryFailure? failure)
    {
        Context = context;
        Failure = failure;
    }

    /// <summary>Gets the strict placement context, or <see langword="null"/> for auto-placement.</summary>
    public ILayoutContext<TKey>? Context { get; }

    /// <summary>Gets the failure when this decision rejects the addition.</summary>
    public InventoryFailure? Failure { get; }

    /// <summary>Gets whether this decision rejects the addition.</summary>
    public bool IsRejected => Failure != null;

    /// <summary>Requests default inventory auto-placement.</summary>
    public static InventoryPlacementDecision<TKey> Auto() => new(null, null);

    /// <summary>Requests strict placement at a layout context.</summary>
    public static InventoryPlacementDecision<TKey> Place(ILayoutContext<TKey> context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        return new InventoryPlacementDecision<TKey>(context, null);
    }

    /// <summary>Rejects the addition with a structured failure.</summary>
    public static InventoryPlacementDecision<TKey> Reject(InventoryFailure failure)
    {
        if (failure == null)
            throw new ArgumentNullException(nameof(failure));
        return new InventoryPlacementDecision<TKey>(null, failure);
    }
}
