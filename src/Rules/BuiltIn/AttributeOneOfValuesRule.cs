using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute matching one of the allowed values.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributeOneOfValuesRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly HashSet<TValue> _allowedValues;
    private readonly string _allowedValuesDescription;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute allowed-values rule.
    /// </summary>
    /// <param name="attribute">The required attribute key.</param>
    /// <param name="allowedValues">The allowed attribute values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="allowedValues"/> is null or empty.</exception>
    public AttributeOneOfValuesRule(AttributeKey<TValue> attribute, params TValue[] allowedValues)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        if (allowedValues == null || allowedValues.Length == 0)
            throw new ArgumentException("At least one allowed value must be provided.", nameof(allowedValues));

        _allowedValues = new HashSet<TValue>(allowedValues);
        _allowedValuesDescription = string.Join(", ", allowedValues);
        Id = $"AttributeOneOf[{_attribute}:{_allowedValuesDescription}]";
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.TryGet(_attribute, out var actualValue))
            {
                error = $"Expected item definition '{definition.Id}' attribute '{_attribute}' to be one of: {_allowedValuesDescription}, but it was missing.";
                return false;
            }

            if (!_allowedValues.Contains(actualValue))
            {
                error = $"Expected item definition '{definition.Id}' attribute '{_attribute}' to be one of: {_allowedValuesDescription}, but it was '{actualValue}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
