using System;
using System.Linq;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that limits the projected total item amount in an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class MaxTotalItemAmountCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    /// <summary>
    /// Creates a capacity policy with a maximum total item amount.
    /// </summary>
    /// <param name="maxTotalItemAmount">The maximum total amount allowed after a transaction.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTotalItemAmount"/> is less than zero.</exception>
    public MaxTotalItemAmountCapacityPolicy(int maxTotalItemAmount)
    {
        if (maxTotalItemAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalItemAmount), "Maximum total item amount cannot be negative.");

        MaxTotalItemAmount = maxTotalItemAmount;
    }

    /// <summary>
    /// Gets the maximum total item amount allowed after a transaction.
    /// </summary>
    public int MaxTotalItemAmount { get; }

    /// <inheritdoc />
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out string? error)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (normalizedTransaction == null)
            throw new ArgumentNullException(nameof(normalizedTransaction));

        int added = normalizedTransaction.Added.Sum(i => i.amount);
        int removed = normalizedTransaction.Removed.Sum(i => i.amount);
        int projected = inventory.TotalItemCount + added - removed;

        if (projected > MaxTotalItemAmount)
        {
            error = "Capacity exceeded.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out string? error)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (inventory.TotalItemCount + instance.Amount > MaxTotalItemAmount)
        {
            error = "Capacity exceeded.";
            return false;
        }

        error = null;
        return true;
    }
}
