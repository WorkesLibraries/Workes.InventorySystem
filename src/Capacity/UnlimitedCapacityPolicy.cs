using Workes.InventorySystem.Core;
using System.ComponentModel;
namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Capacity policy that accepts every item and transaction.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class UnlimitedCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out InventoryFailure? error)
    {
        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out InventoryFailure? error)
    {
        error = null;
        return true;
    }
}
