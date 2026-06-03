using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added items to satisfy a consumer-provided definition and metadata predicate.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemPredicateRule<TKey> : IRulePolicy<TKey>
{
    private readonly Func<ItemDefinition<TKey>, InstanceMetadata?, bool> _predicate;
    private readonly string _errorMessage;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an item predicate rule.
    /// </summary>
    /// <param name="predicate">The predicate that must accept each added item definition and metadata pair.</param>
    /// <param name="errorMessage">The error message prefix used when validation fails.</param>
    /// <param name="id">Optional rule id override.</param>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    public ItemPredicateRule(
        Func<ItemDefinition<TKey>, InstanceMetadata?, bool> predicate,
        string errorMessage = "Expected added items to satisfy the provided predicate",
        string? id = null)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _errorMessage = errorMessage;
        Id = id ?? $"ItemPredicateRule[{_errorMessage}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
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
