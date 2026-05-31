using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Optional interface for rules that can validate using a projected inventory snapshot.
/// </summary>
public interface IInventorySnapshotRulePolicy<TKey>
{
    bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error);
}
