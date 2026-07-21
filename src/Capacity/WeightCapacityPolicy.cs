using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System.ComponentModel;

namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that limits the projected total item weight in an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Per-unit weight is read from a definition attribute. Missing weights
/// contribute zero by default unless strict missing-weight handling is enabled.
/// </remarks>
public sealed class WeightCapacityPolicy<TKey> : IParameterizedCapacityPolicy<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("maxWeight", typeof(double), "Maximum total item weight allowed in the inventory."),
            new InventoryParameterDefinition("treatMissingWeightAsZero", typeof(bool), "Whether definitions missing the weight attribute contribute zero weight.")
        };

    /// <summary>
    /// Gets the definition attribute used as per-unit item weight.
    /// </summary>
    public string WeightAttributeId { get; }

    /// <summary>
    /// Gets the maximum total weight allowed after a transaction.
    /// </summary>
    public double MaxWeight { get; }

    /// <summary>
    /// Gets whether definitions missing the weight attribute contribute zero weight.
    /// </summary>
    public bool TreatMissingWeightAsZero { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates a weight capacity policy.
    /// </summary>
    /// <param name="weightAttributeId">The definition attribute id used as per-unit item weight.</param>
    /// <param name="maxWeight">The maximum total weight allowed after a transaction.</param>
    /// <param name="treatMissingWeightAsZero">Whether missing weight attributes contribute zero weight.</param>
    /// <exception cref="ArgumentException"><paramref name="weightAttributeId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWeight"/> is negative.</exception>
    public WeightCapacityPolicy(
        string weightAttributeId,
        double maxWeight,
        bool treatMissingWeightAsZero = true)
    {
        if (string.IsNullOrWhiteSpace(weightAttributeId))
            throw new ArgumentException("Weight attribute id cannot be null or empty.", nameof(weightAttributeId));
        if (maxWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(maxWeight), "Maximum weight cannot be negative.");

        WeightAttributeId = weightAttributeId;
        MaxWeight = maxWeight;
        TreatMissingWeightAsZero = treatMissingWeightAsZero;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out InventoryFailure? failure)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));
        if (normalizedTransaction == null)
            throw new ArgumentNullException(nameof(normalizedTransaction));

        if (!TryCalculateCurrentWeight(inventory, out double currentWeight, out failure))
            return false;
        if (!TryCalculateWeight(normalizedTransaction.Added, out double addedWeight, out failure))
            return false;
        if (!TryCalculateWeight(normalizedTransaction.Removed, out double removedWeight, out failure))
            return false;

        if (currentWeight + addedWeight - removedWeight > MaxWeight)
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

        if (!TryCalculateCurrentWeight(inventory, out double currentWeight, out failure))
            return false;
        if (!TryGetWeight(instance.Definition, out double itemWeight, out failure))
            return false;

        if (currentWeight + itemWeight * instance.Amount > MaxWeight)
        {
            failure = InventoryFailures.Capacity("Capacity exceeded.");
            return false;
        }

        failure = null;
        return true;
    }

    private bool TryCalculateCurrentWeight(Inventory<TKey> inventory, out double weight, out InventoryFailure? failure)
    {
        weight = 0;
        foreach (var item in inventory.Items)
        {
            if (!TryGetWeight(item.Definition, out double itemWeight, out failure))
                return false;

            weight += itemWeight * item.Amount;
        }

        failure = null;
        return true;
    }

    private bool TryCalculateWeight(
        System.Collections.Generic.IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> entries,
        out double weight,
        out InventoryFailure? failure)
    {
        weight = 0;
        foreach (var (definition, _, amount) in entries)
        {
            if (!TryGetWeight(definition, out double itemWeight, out failure))
                return false;

            weight += itemWeight * amount;
        }

        failure = null;
        return true;
    }

    private bool TryGetWeight(ItemDefinition<TKey> definition, out double weight, out InventoryFailure? failure)
    {
        if (definition.Attributes.TryGet(WeightAttributeId, out weight))
        {
            failure = null;
            return true;
        }

        if (TreatMissingWeightAsZero)
        {
            weight = 0;
            failure = null;
            return true;
        }

        failure = InventoryFailures.Capacity("Item weight attribute missing.");
        return false;
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
        if (parameterId == "maxWeight")
        {
            if (!TryGetDouble(value, out double maxWeight))
            {
                failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter 'maxWeight' expects value type 'Double'.");
                return false;
            }

            if (maxWeight < 0)
            {
                failure = InventoryFailures.Capacity("Maximum weight cannot be negative.");
                return false;
            }

            policy = new WeightCapacityPolicy<TKey>(WeightAttributeId, maxWeight, TreatMissingWeightAsZero);
            failure = null;
            return true;
        }

        if (parameterId == "treatMissingWeightAsZero")
        {
            if (value is not bool treatMissingWeightAsZero)
            {
                failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter 'treatMissingWeightAsZero' expects value type 'Boolean'.");
                return false;
            }

            policy = new WeightCapacityPolicy<TKey>(WeightAttributeId, MaxWeight, treatMissingWeightAsZero);
            failure = null;
            return true;
        }

        failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' is not supported by WeightCapacityPolicy.");
        return false;
    }

    private static bool TryGetDouble(object? value, out double result)
    {
        if (value is double doubleValue)
        {
            result = doubleValue;
            return true;
        }

        if (value is int intValue)
        {
            result = intValue;
            return true;
        }

        if (value is float floatValue)
        {
            result = floatValue;
            return true;
        }

        result = 0;
        return false;
    }
}
