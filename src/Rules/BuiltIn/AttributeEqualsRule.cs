using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Rules;

public class AttributeEqualsRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly TValue _expectedValue;
    public string Id { get; }

    public AttributeEqualsRule(AttributeKey<TValue> attribute, TValue expectedValue)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        _expectedValue = expectedValue;
        Id = $"AttributeEquals[{_attribute}={_expectedValue}]";
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
                error = $"Expected item definition '{definition.Id}' to have attribute '{_attribute}'.";
                return false;
            }

            if (!EqualityComparer<TValue>.Default.Equals(actualValue, _expectedValue))
            {
                error = $"Expected item definition '{definition.Id}' attribute '{_attribute}' to equal '{_expectedValue}', but it was '{actualValue}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
