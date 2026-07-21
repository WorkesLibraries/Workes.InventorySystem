using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Capacity;

/// <summary>
/// Validates whether an inventory has enough capacity for item additions or transactions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract for custom capacity policies. Normal inventory
/// mutation should go through <see cref="Inventory{TKey}"/>, which invokes capacity
/// validation before committing changes.
/// </remarks>
public interface ICapacityPolicy<TKey>
{
    /// <summary>
    /// Evaluates whether a normalized transaction satisfies the capacity policy before it is committed.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="normalizedTransaction">The transaction grouped by item definition and metadata.</param>
    /// <param name="failure">A consumer-facing reason when the transaction is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction may be applied; otherwise, <see langword="false"/>.</returns>
    bool CanApply(Inventory<TKey> inventory, NormalizedInventoryTransaction<TKey> normalizedTransaction, out InventoryFailure? failure);

    /// <summary>
    /// Evaluates whether one item instance can be added.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the instance.</param>
    /// <param name="instance">The item instance being evaluated.</param>
    /// <param name="failure">A consumer-facing reason when the instance is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the instance may be added; otherwise, <see langword="false"/>.</returns>
    /// <remarks>The inventory uses <see cref="CanApply"/> for transaction formulation; this member remains available for custom code.</remarks>
    bool CanAdd(Inventory<TKey> inventory, ItemInstance<TKey> instance, out InventoryFailure? failure);
}
