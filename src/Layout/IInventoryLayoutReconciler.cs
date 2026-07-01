using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Optional capability for layouts whose observable placement or presentation can reflow after inventory mutations.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Inventory invokes this once after an accepted mutation has updated item amounts, metadata, ownership, or layout
/// callbacks. Implementations may reconcile layout-owned state from the final inventory state but must not mutate
/// inventory contents or reject an already validated operation. Normal acceptance remains the responsibility of the
/// layout validation methods. Inventory compares surviving item contexts before and after this callback and reports
/// every changed context as item movement in the same coherent change event.
/// </remarks>
public interface IInventoryLayoutReconciler<TKey> : IInventoryLayout<TKey>
{
    /// <summary>
    /// Reconciles layout-owned state after an accepted inventory mutation.
    /// </summary>
    /// <param name="inventory">The inventory in its final mutated state.</param>
    /// <returns>Supplemental affected-context and full-refresh information.</returns>
    InventoryLayoutReconciliationResult<TKey> ReconcileAfterInventoryMutation(Inventory<TKey> inventory);
}
