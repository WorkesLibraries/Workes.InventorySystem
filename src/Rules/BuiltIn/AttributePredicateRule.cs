using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute accepted by a consumer-provided predicate.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributePredicateRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly Func<TValue, bool> _predicate;
    private readonly string _errorMessage;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute predicate rule.
    /// </summary>
    /// <param name="attribute">The required attribute key.</param>
    /// <param name="predicate">The predicate that must accept the attribute value.</param>
    /// <param name="errorMessage">The error message prefix used when validation fails.</param>
    /// <param name="id">Optional rule id override.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attribute"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    public AttributePredicateRule(
        AttributeKey<TValue> attribute,
        Func<TValue, bool> predicate,
        string errorMessage = "Expected item definition attribute to satisfy the provided predicate",
        string? id = null)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _errorMessage = errorMessage;
        Id = id ?? $"AttributePredicate[{_attribute}:{_errorMessage}]";
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.TryGet(_attribute, out var value))
            {
                error = $"{_errorMessage}. Item definition '{definition.Id}' was missing attribute '{_attribute}'.";
                return false;
            }

            if (!_predicate(value))
            {
                error = $"{_errorMessage}. Item definition '{definition.Id}' attribute '{_attribute}' was '{value}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
