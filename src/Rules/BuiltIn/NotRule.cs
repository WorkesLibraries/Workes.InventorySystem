using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Wraps another rule and inverts its result.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class NotRule<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IRulePolicy<TKey> _inner;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a negating rule wrapper.
    /// </summary>
    /// <param name="inner">The rule whose result should be inverted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public NotRule(IRulePolicy<TKey> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Id = $"Not[{_inner.Id}]";
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        if (_inner is IInventorySnapshotRulePolicy<TKey>)
        {
            var snapshot = new InventoryRuleSnapshot<TKey>(inventory, transaction);
            return CanApply(inventory, transaction, snapshot, out error);
        }

        var allowed = _inner.CanApply(inventory, transaction, out _);
        if (allowed)
        {
            error = $"Expected wrapped rule '{_inner.GetType().Name}' to reject the transaction, but it allowed it.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        var snapshotInner = _inner as IInventorySnapshotRulePolicy<TKey>;
        var allowed = snapshotInner != null
            ? snapshotInner.CanApply(inventory, transaction, snapshot, out _)
            : _inner.CanApply(inventory, transaction, out _);

        if (allowed)
        {
            error = $"Expected wrapped rule '{_inner.GetType().Name}' to reject the transaction, but it allowed it.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error)
    {
        var allowed = _inner is IInventoryStructuralRulePolicy<TKey> structuralRule
            ? structuralRule.CanApply(inventory, transaction, out _)
            : _inner.CanApply(inventory, inventory.GenerateNormalizedInventoryTransaction(transaction), out _);

        if (allowed)
        {
            error = $"Expected wrapped rule '{_inner.GetType().Name}' to reject the transaction, but it allowed it.";
            return false;
        }

        error = null;
        return true;
    }
}
