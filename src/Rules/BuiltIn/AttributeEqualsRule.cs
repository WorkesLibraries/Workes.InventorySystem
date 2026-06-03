using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute equal to an expected value.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributeEqualsRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly string _attributeId;
    private readonly TValue _expectedValue;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute equality rule.
    /// </summary>
    /// <param name="attributeId">The required attribute id.</param>
    /// <param name="expectedValue">The required attribute value.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeId"/> is null, empty, or whitespace.</exception>
    public AttributeEqualsRule(string attributeId, TValue expectedValue)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be null or empty.", nameof(attributeId));

        _attributeId = attributeId;
        _expectedValue = expectedValue;
        Id = $"AttributeEquals[{_attributeId}={_expectedValue}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.TryGet<TValue>(_attributeId, out var actualValue))
            {
                error = $"Expected item definition '{definition.Id}' to have attribute '{_attributeId}'.";
                return false;
            }

            if (!EqualityComparer<TValue>.Default.Equals(actualValue, _expectedValue))
            {
                error = $"Expected item definition '{definition.Id}' attribute '{_attributeId}' to equal '{_expectedValue}', but it was '{actualValue}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
