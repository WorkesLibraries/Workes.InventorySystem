using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Rules;

public class RuleContainer<TKey>
{
    private sealed class RuleEntry
    {
        public IRulePolicy<TKey> Rule { get; }
        public int Priority { get; }
        public bool Enabled { get; }
        public long Sequence { get; }

        public RuleEntry(IRulePolicy<TKey> rule, int priority, bool enabled, long sequence)
        {
            Rule = rule;
            Priority = priority;
            Enabled = enabled;
            Sequence = sequence;
        }
    }

    private readonly Dictionary<string, RuleEntry> _rules = new Dictionary<string, RuleEntry>(StringComparer.Ordinal);
    private long _sequence;

    public RuleContainer() { }

    /*
    public RuleContainer(params IRulePolicy<TKey>[] rules)
    {
        if (rules == null)
            return;

        foreach (var rule in rules)
        {
            if (rule == null)
                continue;
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Rule id cannot be null/empty.", nameof(rules));

            Add(rule.Id, rule);
        }
    }*/

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return CanApply(inventory, transaction, structuralTransaction: null, out error);
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryTransaction<TKey>? structuralTransaction,
        out string? error)
    {
        InventoryRuleSnapshot<TKey>? snapshot = null;

        // Higher priority first. If equal priority, keep insertion order.
        foreach (var entry in _rules.Values
                     .Where(e => e.Enabled)
                     .OrderByDescending(e => e.Priority)
                     .ThenBy(e => e.Sequence))
        {
            var rule = entry.Rule;
            bool allowed;
            if (rule is IInventorySnapshotRulePolicy<TKey> snapshotRule)
            {
                snapshot ??= new InventoryRuleSnapshot<TKey>(inventory, transaction);
                allowed = snapshotRule.CanApply(inventory, transaction, snapshot, out error);
            }
            else
            {
                allowed = rule.CanApply(inventory, transaction, out error);
            }

            if (!allowed)
            {
                var ruleName = rule.GetType().Name;
                var ruleId = rule.Id;
                error = string.IsNullOrWhiteSpace(error)
                    ? $"Rule '{ruleId}' ({ruleName}) rejected the transaction."
                    : $"Rule '{ruleId}' ({ruleName}) rejected the transaction: {error}";
                return false;
            }
        }

        if (structuralTransaction != null)
        {
            foreach (var entry in _rules.Values
                         .Where(e => e.Enabled)
                         .OrderByDescending(e => e.Priority)
                         .ThenBy(e => e.Sequence))
            {
                if (entry.Rule is not IInventoryStructuralRulePolicy<TKey> structuralRule)
                    continue;

                if (structuralRule.CanApply(inventory, structuralTransaction, out error))
                    continue;

                var ruleName = entry.Rule.GetType().Name;
                var ruleId = entry.Rule.Id;
                error = string.IsNullOrWhiteSpace(error)
                    ? $"Rule '{ruleId}' ({ruleName}) rejected the transaction."
                    : $"Rule '{ruleId}' ({ruleName}) rejected the transaction: {error}";
                return false;
            }
        }

        error = null;
        return true;
    }

    public IReadOnlyDictionary<string, IRulePolicy<TKey>> Rules =>
        _rules.ToDictionary(kv => kv.Key, kv => kv.Value.Rule, StringComparer.Ordinal);

    public IRulePolicy<TKey> this[string id]
    {
        get => _rules[id].Rule;
        set => Set(id, value);
    }

    public void Set(string id, IRulePolicy<TKey> rule)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("Rule id cannot be null/empty.", nameof(id));
        if (rule == null)
            throw new System.ArgumentNullException(nameof(rule));

        if (_rules.TryGetValue(id, out var existing))
        {
            _rules[id] = new RuleEntry(WrapRule(id, rule), existing.Priority, existing.Enabled, existing.Sequence);
            return;
        }

        _rules[id] = new RuleEntry(WrapRule(id, rule), 0, true, _sequence++);
    }

    public void Set(string id, IRulePolicy<TKey> rule, int priority, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("Rule id cannot be null/empty.", nameof(id));
        if (rule == null)
            throw new System.ArgumentNullException(nameof(rule));

        if (_rules.TryGetValue(id, out var existing))
            _rules[id] = new RuleEntry(WrapRule(id, rule), priority, enabled, existing.Sequence);
        else
            _rules[id] = new RuleEntry(WrapRule(id, rule), priority, enabled, _sequence++);
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        return _rules.Remove(id);
    }

    public bool TrySetEnabled(string id, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_rules.TryGetValue(id, out var entry))
            return false;

        _rules[id] = new RuleEntry(entry.Rule, entry.Priority, enabled, entry.Sequence);
        return true;
    }

    public bool TrySetPriority(string id, int priority)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_rules.TryGetValue(id, out var entry))
            return false;

        _rules[id] = new RuleEntry(entry.Rule, priority, entry.Enabled, entry.Sequence);
        return true;
    }

    // Collection-initializer friendly overload.
    public void Add(string id, IRulePolicy<TKey> rule)
    {
        Add(id, rule, priority: 0, enabled: true);
    }

    public void Add(string id, IRulePolicy<TKey> rule, int priority)
    {
        Add(id, rule, priority, enabled: true);
    }

    public void Add(string id, IRulePolicy<TKey> rule, bool enabled)
    {
        Add(id, rule, priority: 0, enabled);
    }

    public void Add(string id, IRulePolicy<TKey> rule, int priority, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("Rule id cannot be null/empty.", nameof(id));
        if (rule == null)
            throw new System.ArgumentNullException(nameof(rule));

        if (_rules.ContainsKey(id))
            throw new ArgumentException($"Duplicate rule id '{id}' in RuleContainer.", nameof(rule));

        _rules.Add(id, new RuleEntry(WrapRule(id, rule), priority, enabled, _sequence++));
    }

    private static IRulePolicy<TKey> WrapRule(string id, IRulePolicy<TKey> rule)
    {
        if (rule is IInventorySnapshotRulePolicy<TKey> snapshotRule)
            return new IdentifiedSnapshotRulePolicy<TKey>(id, snapshotRule);
        return new IdentifiedRulePolicy<TKey>(id, rule);
    }
}
