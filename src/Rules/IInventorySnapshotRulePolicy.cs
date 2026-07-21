using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Optional interface for rules that can validate using a projected inventory snapshot.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract for rules that need inventory-wide state after
/// a projected transaction. Normal application code should use inventory mutation
/// APIs; the inventory builds snapshots for registered rules during validation.
/// </remarks>
public interface IInventorySnapshotRulePolicy<TKey>
{
    /// <summary>
    /// Validates whether a normalized transaction can be applied using a projected inventory snapshot.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The semantic transaction grouped by item definition and metadata.</param>
    /// <param name="snapshot">A lazy projected view of inventory quantities after the transaction.</param>
    /// <param name="error">A consumer-facing reason when the rule rejects the transaction; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction satisfies the rule; otherwise, <see langword="false"/>.</returns>
    bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out InventoryFailure? error);
}
