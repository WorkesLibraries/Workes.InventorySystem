using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

public interface IRulePolicy<TKey>
{
    /// <summary>
    /// Stable identity for this rule instance.
    /// Used by <see cref="RuleContainer{TKey}"/> to add/replace/remove rules at runtime.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Validates whether a normalized transaction can be applied.
    /// Rules should prefer transaction-only checks for performance.
    /// Rules that need an inventory-wide view can use <see cref="InventoryRuleSnapshot{TKey}"/>
    /// directly or inherit from <see cref="InventorySnapshotRulePolicy{TKey}"/>.
    /// </summary>
    bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error);
}
