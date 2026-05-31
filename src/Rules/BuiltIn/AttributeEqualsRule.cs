using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute equal to an expected value.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributeEqualsRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly TValue _expectedValue;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute equality rule.
    /// </summary>
    /// <param name="attribute">The required attribute key.</param>
    /// <param name="expectedValue">The required attribute value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/>.</exception>
    public AttributeEqualsRule(AttributeKey<TValue> attribute, TValue expectedValue)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        _expectedValue = expectedValue;
        Id = $"AttributeEquals[{_attribute}={_expectedValue}]";
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
