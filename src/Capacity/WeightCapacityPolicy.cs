using System;
using System.Linq;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that limits the projected total item weight in an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Per-unit weight is read from a definition attribute. Missing weights
/// contribute zero by default unless strict missing-weight handling is enabled.
/// </remarks>
public sealed class WeightCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    /// <summary>
    /// Gets the definition attribute used as per-unit item weight.
    /// </summary>
    public AttributeKey<double> WeightAttribute { get; }

    /// <summary>
    /// Gets the maximum total weight allowed after a transaction.
    /// </summary>
    public double MaxWeight { get; }

    /// <summary>
    /// Gets whether definitions missing the weight attribute contribute zero weight.
    /// </summary>
    public bool TreatMissingWeightAsZero { get; }

    /// <summary>
    /// Creates a weight capacity policy.
    /// </summary>
    /// <param name="weightAttribute">The definition attribute used as per-unit item weight.</param>
    /// <param name="maxWeight">The maximum total weight allowed after a transaction.</param>
    /// <param name="treatMissingWeightAsZero">Whether missing weight attributes contribute zero weight.</param>
    /// <exception cref="ArgumentNullException"><paramref name="weightAttribute"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWeight"/> is negative.</exception>
    public WeightCapacityPolicy(
        AttributeKey<double> weightAttribute,
        double maxWeight,
        bool treatMissingWeightAsZero = true)
    {
        WeightAttribute = weightAttribute ?? throw new ArgumentNullException(nameof(weightAttribute));
        if (maxWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(maxWeight), "Maximum weight cannot be negative.");

        MaxWeight = maxWeight;
        TreatMissingWeightAsZero = treatMissingWeightAsZero;
    }

    /// <inheritdoc />
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out string? error)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (normalizedTransaction == null)
            throw new ArgumentNullException(nameof(normalizedTransaction));

        if (!TryCalculateCurrentWeight(inventory, out double currentWeight, out error))
            return false;
        if (!TryCalculateWeight(normalizedTransaction.Added, out double addedWeight, out error))
            return false;
        if (!TryCalculateWeight(normalizedTransaction.Removed, out double removedWeight, out error))
            return false;

        if (currentWeight + addedWeight - removedWeight > MaxWeight)
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

        if (!TryCalculateCurrentWeight(inventory, out double currentWeight, out error))
            return false;
        if (!TryGetWeight(instance.Definition, out double itemWeight, out error))
            return false;

        if (currentWeight + itemWeight * instance.Amount > MaxWeight)
        {
            error = "Capacity exceeded.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryCalculateCurrentWeight(Inventory<TKey> inventory, out double weight, out string? error)
    {
        weight = 0;
        foreach (var item in inventory.Items)
        {
            if (!TryGetWeight(item.Definition, out double itemWeight, out error))
                return false;

            weight += itemWeight * item.Amount;
        }

        error = null;
        return true;
    }

    private bool TryCalculateWeight(
        System.Collections.Generic.IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> entries,
        out double weight,
        out string? error)
    {
        weight = 0;
        foreach (var (definition, _, amount) in entries)
        {
            if (!TryGetWeight(definition, out double itemWeight, out error))
                return false;

            weight += itemWeight * amount;
        }

        error = null;
        return true;
    }

    private bool TryGetWeight(ItemDefinition<TKey> definition, out double weight, out string? error)
    {
        if (definition.Attributes.TryGet(WeightAttribute, out weight))
        {
            error = null;
            return true;
        }

        if (TreatMissingWeightAsZero)
        {
            weight = 0;
            error = null;
            return true;
        }

        error = "Item weight attribute missing.";
        return false;
    }
}
