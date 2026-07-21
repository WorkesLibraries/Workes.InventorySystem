using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using System.ComponentModel;

namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that limits the projected total item amount in an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class MaxTotalItemAmountCapacityPolicy<TKey> : IParameterizedCapacityPolicy<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("maxTotalItemAmount", typeof(int), "Maximum total item amount allowed in the inventory.")
        };

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
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out InventoryFailure? failure)
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
            failure = InventoryFailures.Capacity("Capacity exceeded.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out InventoryFailure? failure)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (inventory.TotalItemCount + instance.Amount > MaxTotalItemAmount)
        {
            failure = InventoryFailures.Capacity("Capacity exceeded.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out ICapacityPolicy<TKey>? policy,
        out InventoryFailure? failure)
    {
        policy = null;
        if (parameterId != "maxTotalItemAmount")
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' is not supported by MaxTotalItemAmountCapacityPolicy.");
            return false;
        }

        if (value is not int maxTotalItemAmount)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter 'maxTotalItemAmount' expects value type 'Int32'.");
            return false;
        }

        if (maxTotalItemAmount < 0)
        {
            failure = InventoryFailures.Capacity("Maximum total item amount cannot be negative.");
            return false;
        }

        policy = new MaxTotalItemAmountCapacityPolicy<TKey>(maxTotalItemAmount);
        failure = null;
        return true;
    }
}
