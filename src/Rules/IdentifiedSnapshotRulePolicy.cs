using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Wraps a snapshot-capable rule and overrides its identity.
/// </summary>
public sealed class IdentifiedSnapshotRulePolicy<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IInventorySnapshotRulePolicy<TKey> _innerSnapshot;
    private readonly IRulePolicy<TKey> _innerRule;

    public string Id { get; }

    public IdentifiedSnapshotRulePolicy(string id, IInventorySnapshotRulePolicy<TKey> innerSnapshot)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be null/empty.", nameof(id));
        _innerSnapshot = innerSnapshot ?? throw new ArgumentNullException(nameof(innerSnapshot));
        _innerRule = innerSnapshot as IRulePolicy<TKey>
            ?? throw new ArgumentException("Snapshot rule must also implement IRulePolicy.", nameof(innerSnapshot));
        Id = id;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return _innerRule.CanApply(inventory, transaction, out error);
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        return _innerSnapshot.CanApply(inventory, transaction, snapshot, out error);
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error)
    {
        if (_innerRule is IInventoryStructuralRulePolicy<TKey> structuralRule)
            return structuralRule.CanApply(inventory, transaction, out error);

        error = null;
        return true;
    }
}
