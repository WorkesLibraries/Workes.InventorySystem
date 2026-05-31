using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that accepts every item and transaction.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class UnlimitedCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    /// <inheritdoc />
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out string? error)
    {
        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out string? error)
    {
        error = null;
        return true;
    }
}
