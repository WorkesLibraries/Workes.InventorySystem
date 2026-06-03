using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Wraps a snapshot-capable rule and overrides its identity.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class IdentifiedSnapshotRulePolicy<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IInventorySnapshotRulePolicy<TKey> _innerSnapshot;
    private readonly IRulePolicy<TKey> _innerRule;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an identified snapshot rule wrapper.
    /// </summary>
    /// <param name="id">The rule id exposed by the wrapper.</param>
    /// <param name="innerSnapshot">The wrapped snapshot-capable rule.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid, or <paramref name="innerSnapshot"/> does not also implement <see cref="IRulePolicy{TKey}"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="innerSnapshot"/> is <see langword="null"/>.</exception>
    public IdentifiedSnapshotRulePolicy(string id, IInventorySnapshotRulePolicy<TKey> innerSnapshot)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be null/empty.", nameof(id));
        _innerSnapshot = innerSnapshot ?? throw new ArgumentNullException(nameof(innerSnapshot));
        _innerRule = innerSnapshot as IRulePolicy<TKey>
            ?? throw new ArgumentException("Snapshot rule must also implement IRulePolicy.", nameof(innerSnapshot));
        Id = id;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return _innerRule.CanApply(inventory, transaction, out error);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        return _innerSnapshot.CanApply(inventory, transaction, snapshot, out error);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
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
