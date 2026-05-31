using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Optional interface for rules that need the structural inventory transaction,
/// such as rules based on item instance count rather than item quantity.
/// </summary>
public interface IInventoryStructuralRulePolicy<TKey>
{
    bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error);
}
