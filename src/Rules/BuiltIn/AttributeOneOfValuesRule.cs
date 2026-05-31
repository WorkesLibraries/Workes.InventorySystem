using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Rules;

public class AttributeOneOfValuesRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly HashSet<TValue> _allowedValues;
    private readonly string _allowedValuesDescription;
    public string Id { get; }

    public AttributeOneOfValuesRule(AttributeKey<TValue> attribute, params TValue[] allowedValues)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        if (allowedValues == null || allowedValues.Length == 0)
            throw new ArgumentException("At least one allowed value must be provided.", nameof(allowedValues));

        _allowedValues = new HashSet<TValue>(allowedValues);
        _allowedValuesDescription = string.Join(", ", allowedValues);
        Id = $"AttributeOneOf[{_attribute}:{_allowedValuesDescription}]";
    }

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
