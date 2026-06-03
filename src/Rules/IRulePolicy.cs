using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Defines a rule that can accept or reject semantic inventory transactions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract for custom rules. Application code should add
/// or change rules through <see cref="RuleContainer{TKey}"/> during setup or
/// inventory-owned rule mutation methods such as <see cref="Inventory{TKey}.TrySetRule(string, IRulePolicy{TKey}, out string?)"/>.
/// Inventory transaction methods invoke rules as part of validation.
/// </remarks>
public interface IRulePolicy<TKey>
{
    /// <summary>
    /// Stable identity for this rule instance.
    /// Used by <see cref="RuleContainer{TKey}"/> to add/replace/remove rules at runtime.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Validates whether a normalized transaction can be applied.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The semantic transaction grouped by item definition and metadata.</param>
    /// <param name="error">A consumer-facing reason when the rule rejects the transaction; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction satisfies the rule; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Rules should prefer transaction-only checks for performance. Rules that need an inventory-wide view can use
    /// <see cref="InventoryRuleSnapshot{TKey}"/> directly or inherit from <see cref="InventorySnapshotRulePolicy{TKey}"/>.
    /// </remarks>
    bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error);
}
