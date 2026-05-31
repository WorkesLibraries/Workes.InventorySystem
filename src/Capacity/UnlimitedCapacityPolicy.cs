using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Capacity;

public class UnlimitedCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out string? error)
    {
        error = null;
        return true;
    }

    public bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out string? error)
    {
        error = null;
        return true;
    }
}
