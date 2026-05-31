using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Base class for rules that may need an inventory-wide projected snapshot.
/// Projection is still lazy and only materializes if a derived rule queries it.
/// </summary>
public abstract class InventorySnapshotRulePolicy<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>
{
    public string Id { get; protected set; } = string.Empty;

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        var snapshot = new InventoryRuleSnapshot<TKey>(inventory, transaction);
        return CanApply(inventory, transaction, snapshot, out error);
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        return CanApplyWithSnapshot(inventory, transaction, snapshot, out error);
    }

    protected abstract bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error);
}
