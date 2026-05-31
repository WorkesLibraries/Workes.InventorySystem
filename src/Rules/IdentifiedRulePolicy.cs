using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Wraps a rule and overrides its identity for stable dictionary-key based management.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class IdentifiedRulePolicy<TKey> : IRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IRulePolicy<TKey> _inner;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an identified rule wrapper.
    /// </summary>
    /// <param name="id">The rule id exposed by the wrapper.</param>
    /// <param name="inner">The wrapped rule.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public IdentifiedRulePolicy(string id, IRulePolicy<TKey> inner)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be null/empty.", nameof(id));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Id = id;
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return _inner.CanApply(inventory, transaction, out error);
    }

    /// <inheritdoc />
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
