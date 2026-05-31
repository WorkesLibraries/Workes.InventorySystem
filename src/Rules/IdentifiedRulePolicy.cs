using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Wraps a rule and overrides its identity for stable dictionary-key based management.
/// </summary>
public sealed class IdentifiedRulePolicy<TKey> : IRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IRulePolicy<TKey> _inner;

    public string Id { get; }

    public IdentifiedRulePolicy(string id, IRulePolicy<TKey> inner)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be null/empty.", nameof(id));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Id = id;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return _inner.CanApply(inventory, transaction, out error);
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error)
    {
        if (_inner is IInventoryStructuralRulePolicy<TKey> structuralRule)
            return structuralRule.CanApply(inventory, transaction, out error);

        error = null;
        return true;
    }
}
