using System;

namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes a committed inventory-owned rule configuration mutation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class InventoryRuleConfigurationChanged<TKey>
{
    /// <summary>
    /// Creates a rule configuration change payload.
    /// </summary>
    /// <param name="ruleId">The stable rule id that changed.</param>
    /// <param name="changeKind">The rule mutation kind.</param>
    /// <param name="previousState">The rule state before the change, or <see langword="null"/> for additions.</param>
    /// <param name="currentState">The rule state after the change, or <see langword="null"/> for removals.</param>
    /// <exception cref="ArgumentException"><paramref name="ruleId"/> is null, empty, or whitespace.</exception>
    public InventoryRuleConfigurationChanged(
        string ruleId,
        InventoryRuleConfigurationChangeKind changeKind,
        InventoryRuleState<TKey>? previousState,
        InventoryRuleState<TKey>? currentState)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule id cannot be null or empty.", nameof(ruleId));

        RuleId = ruleId;
        ChangeKind = changeKind;
        PreviousState = previousState;
        CurrentState = currentState;
    }

    /// <summary>Gets the stable rule id that changed.</summary>
    public string RuleId { get; }

    /// <summary>Gets the rule mutation kind.</summary>
    public InventoryRuleConfigurationChangeKind ChangeKind { get; }

    /// <summary>Gets the rule state before the change, or <see langword="null"/> for additions.</summary>
    public InventoryRuleState<TKey>? PreviousState { get; }

    /// <summary>Gets the rule state after the change, or <see langword="null"/> for removals.</summary>
    public InventoryRuleState<TKey>? CurrentState { get; }
}
