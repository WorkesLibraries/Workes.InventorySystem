using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Binds one inventory-local semantic delta, and optionally an application plan, to an inventory participating in a
/// transaction.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryTransactionEntry<TKey>
{
    /// <summary>Gets the inventory this entry targets.</summary>
    public Inventory<TKey> Inventory { get; }

    /// <summary>Gets the one-inventory semantic delta to apply.</summary>
    public InventoryItemDelta<TKey> Delta { get; }

    /// <summary>Gets the optional delta application plan. Plan behavior is introduced by the transaction-application API.</summary>
    public InventoryDeltaApplicationPlan<TKey>? Plan { get; }

    /// <summary>Creates a transaction entry.</summary>
    public InventoryTransactionEntry(
        Inventory<TKey> inventory,
        InventoryItemDelta<TKey> delta,
        InventoryDeltaApplicationPlan<TKey>? plan = null)
    {
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
        Plan = plan;
    }
}
