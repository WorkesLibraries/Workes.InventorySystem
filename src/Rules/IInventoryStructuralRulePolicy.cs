using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Optional interface for rules that need the structural inventory transaction,
/// such as rules based on item instance count rather than item quantity.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract. Normal application code should commit changes
/// through <see cref="Inventory{TKey}"/>; inventory validation invokes structural
/// rules when they are registered.
/// </remarks>
public interface IInventoryStructuralRulePolicy<TKey>
{
    /// <summary>
    /// Validates whether a structural transaction can be applied.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The structural transaction containing storage-index changes.</param>
    /// <param name="error">A consumer-facing reason when the rule rejects the transaction; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction satisfies the rule; otherwise, <see langword="false"/>.</returns>
    bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error);
}
