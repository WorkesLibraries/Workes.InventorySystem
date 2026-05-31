using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;

namespace Workes.InventorySystem.Rules;

public class AttributePredicateRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    private readonly Func<TValue, bool> _predicate;
    private readonly string _errorMessage;
    public string Id { get; }

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
