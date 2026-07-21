using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to have an attribute accepted by a consumer-provided predicate.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class AttributePredicateRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly string _attributeId;
    private readonly Func<TValue, bool> _predicate;
    private readonly string _errorMessage;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an attribute predicate rule.
    /// </summary>
    /// <param name="attributeId">The required attribute id.</param>
    /// <param name="predicate">The predicate that must accept the attribute value.</param>
    /// <param name="errorMessage">The failure message prefix used when validation fails.</param>
    /// <param name="id">Optional rule id override.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    public AttributePredicateRule(
        string attributeId,
        Func<TValue, bool> predicate,
        string errorMessage = "Expected item definition attribute to satisfy the provided predicate",
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be null or empty.", nameof(attributeId));

        _attributeId = attributeId;
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _errorMessage = errorMessage;
        Id = id ?? $"AttributePredicate[{_attributeId}:{_errorMessage}]";
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
            if (!definition.Attributes.TryGet<TValue>(_attributeId, out var value))
            {
                failure = InventoryFailures.Definition($"{_errorMessage}. Item definition '{definition.Id}' was missing attribute '{_attributeId}'.");
                return false;
            }

            if (!_predicate(value))
            {
                failure = InventoryFailures.Definition($"{_errorMessage}. Item definition '{definition.Id}' attribute '{_attributeId}' was '{value}'.");
                return false;
            }
        }

        failure = null;
        return true;
    }
}
