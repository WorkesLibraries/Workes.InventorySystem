using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

public class ItemPredicateRule<TKey> : IRulePolicy<TKey>
{
    private readonly Func<ItemDefinition<TKey>, InstanceMetadata?, bool> _predicate;
    private readonly string _errorMessage;
    public string Id { get; }

    public ItemPredicateRule(
        Func<ItemDefinition<TKey>, InstanceMetadata?, bool> predicate,
        string errorMessage = "Expected added items to satisfy the provided predicate",
        string? id = null)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _errorMessage = errorMessage;
        Id = id ?? $"ItemPredicateRule[{_errorMessage}]";
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (definition, metadata, _) in transaction.Added)
        {
            if (!_predicate(definition, metadata))
            {
                error = $"{_errorMessage}. Item '{definition.Id}' did not satisfy the predicate.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
