using Workes.InventorySystem.Core;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Base class for rules that may need an inventory-wide projected snapshot.
/// Projection is still lazy and only materializes if a derived rule queries it.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension base class. Normal application code should register rules
/// and mutate inventories through <see cref="Inventory{TKey}"/> rather than calling
/// rule validation directly.
/// </remarks>
public abstract class InventorySnapshotRulePolicy<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>
{
    /// <inheritdoc />
    public string Id { get; protected set; } = string.Empty;

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        var snapshot = new InventoryRuleSnapshot<TKey>(inventory, transaction);
        return CanApply(inventory, transaction, snapshot, out error);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        return CanApplyWithSnapshot(inventory, transaction, snapshot, out error);
    }

    /// <summary>
    /// Validates whether a normalized transaction can be applied using a projected inventory snapshot.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The semantic transaction grouped by item definition and metadata.</param>
    /// <param name="snapshot">A lazy projected view of inventory quantities after the transaction.</param>
    /// <param name="error">A consumer-facing reason when the rule rejects the transaction; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction satisfies the rule; otherwise, <see langword="false"/>.</returns>
    protected abstract bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error);
}
