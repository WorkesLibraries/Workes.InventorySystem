using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute matching one of the allowed values.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributeOneOfValuesRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly string _attributeId;
    private readonly HashSet<TValue> _allowedValues;
    private readonly string _allowedValuesDescription;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute allowed-values rule.
    /// </summary>
    /// <param name="attributeId">The required attribute id.</param>
    /// <param name="allowedValues">The allowed attribute values.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException"><paramref name="allowedValues"/> is null or empty.</exception>
    public AttributeOneOfValuesRule(string attributeId, params TValue[] allowedValues)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be null or empty.", nameof(attributeId));
        if (allowedValues == null || allowedValues.Length == 0)
            throw new ArgumentException("At least one allowed value must be provided.", nameof(allowedValues));

        _attributeId = attributeId;
        _allowedValues = new HashSet<TValue>(allowedValues);
        _allowedValuesDescription = string.Join(", ", allowedValues);
        Id = $"AttributeOneOf[{_attributeId}:{_allowedValuesDescription}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? failure)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.TryGet<TValue>(_attributeId, out var actualValue))
            {
                failure = InventoryFailures.Definition($"Expected item definition '{definition.Id}' attribute '{_attributeId}' to be one of: {_allowedValuesDescription}, but it was missing.");
                return false;
            }

            if (!_allowedValues.Contains(actualValue))
            {
                failure = InventoryFailures.Definition($"Expected item definition '{definition.Id}' attribute '{_attributeId}' to be one of: {_allowedValuesDescription}, but it was '{actualValue}'.");
                return false;
            }
        }

        failure = null;
        return true;
    }
}
