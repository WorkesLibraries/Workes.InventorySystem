using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Succeeds when any nested rule succeeds.
/// </summary>
public class OrRule<TKey> : IRulePolicy<TKey>, IInventorySnapshotRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly IRulePolicy<TKey>[] _rules;
    public string Id { get; }

    public OrRule(params IRulePolicy<TKey>[] rules)
    {
        if (rules == null || rules.Length == 0)
            throw new ArgumentException("At least one rule is required.", nameof(rules));
        _rules = rules;
        Id = $"Or[{string.Join("|", Array.ConvertAll(rules, r => r.Id))}]";
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        var failures = new List<string>(_rules.Length);

        // Evaluate cheaper rules first (transaction-only), so we can avoid touching
        // snapshot-based rules when a transaction-only rule already allowed the transaction.
        foreach (var rule in _rules)
        {
            if (rule is IInventorySnapshotRulePolicy<TKey>)
                continue; // snapshot rules are evaluated later

            if (rule.CanApply(inventory, transaction, out var childError))
            {
                error = null;
                return true;
            }

            var ruleName = rule.GetType().Name;
            failures.Add(string.IsNullOrWhiteSpace(childError)
                ? $"'{ruleName}' rejected (no details)."
                : $"'{ruleName}' rejected: {childError}");
        }

        InventoryRuleSnapshot<TKey>? snapshot = null;

        // Now evaluate snapshot-capable rules.
        foreach (var rule in _rules)
        {
            if (rule is not IInventorySnapshotRulePolicy<TKey> snapshotRule)
                continue;

            snapshot ??= new InventoryRuleSnapshot<TKey>(inventory, transaction);
            if (snapshotRule.CanApply(inventory, transaction, snapshot, out var childError))
            {
                error = null;
                return true;
            }

            var ruleName = rule.GetType().Name;
            failures.Add(string.IsNullOrWhiteSpace(childError)
                ? $"'{ruleName}' rejected (no details)."
                : $"'{ruleName}' rejected: {childError}");
        }

        error = "OrRule expected at least one nested rule to allow the transaction, but none did. " +
                $"Failures: {string.Join(" ", failures)}";
        return false;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        var failures = new List<string>(_rules.Length);

        // Transaction-only rules first.
        foreach (var rule in _rules)
        {
            if (rule is IInventorySnapshotRulePolicy<TKey>)
                continue;

            if (rule.CanApply(inventory, transaction, out var childError))
            {
                error = null;
                return true;
            }

            var ruleName = rule.GetType().Name;
            failures.Add(string.IsNullOrWhiteSpace(childError)
                ? $"'{ruleName}' rejected (no details)."
                : $"'{ruleName}' rejected: {childError}");
        }

        // Snapshot-capable rules later.
        foreach (var rule in _rules)
        {
            if (rule is not IInventorySnapshotRulePolicy<TKey> snapshotRule)
                continue;

            if (snapshotRule.CanApply(inventory, transaction, snapshot, out var childError))
            {
                error = null;
                return true;
            }

            var ruleName = rule.GetType().Name;
            failures.Add(string.IsNullOrWhiteSpace(childError)
                ? $"'{ruleName}' rejected (no details)."
                : $"'{ruleName}' rejected: {childError}");
        }

        error = "OrRule expected at least one nested rule to allow the transaction, but none did. " +
                $"Failures: {string.Join(" ", failures)}";
        return false;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error)
    {
        var failures = new List<string>(_rules.Length);
        var normalized = inventory.GenerateNormalizedInventoryTransaction(transaction);

        foreach (var rule in _rules)
        {
            bool allowed = rule is IInventoryStructuralRulePolicy<TKey> structuralRule
                ? structuralRule.CanApply(inventory, transaction, out var childError)
                : rule.CanApply(inventory, normalized, out childError);

            if (allowed)
            {
                error = null;
                return true;
            }

            var ruleName = rule.GetType().Name;
            failures.Add(string.IsNullOrWhiteSpace(childError)
                ? $"'{ruleName}' rejected (no details)."
                : $"'{ruleName}' rejected: {childError}");
        }

        error = "OrRule expected at least one nested rule to allow the transaction, but none did. " +
                $"Failures: {string.Join(" ", failures)}";
        return false;
    }
}
