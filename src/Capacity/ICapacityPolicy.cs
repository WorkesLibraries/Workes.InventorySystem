using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Capacity;

public interface ICapacityPolicy<TKey>
{
    /// <summary>Evaluates the entire transaction in normalized (semantic) form. Use this to enforce capacity; the inventory consults it before committing.</summary>
    bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out string? error);

    /// <summary>Legacy per-instance check; may be used by custom code. The inventory uses <see cref="CanApply"/> for formulation.</summary>
    bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out string? error);
}
