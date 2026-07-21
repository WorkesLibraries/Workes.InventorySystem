using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events.Dto;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Stores and evaluates inventory rules by id, priority, and enabled state.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Enabled rules run in descending priority order. Rules with equal priority run in insertion order. When a structural
/// transaction is supplied, structural rules are evaluated after semantic and snapshot-capable rules.
/// </remarks>
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

    /// <summary>
    /// Creates an empty rule container.
    /// </summary>
    public RuleContainer() { }

    internal RuleContainer<TKey> Clone()
    {
        var clone = new RuleContainer<TKey>();
        clone._sequence = _sequence;
        foreach (var rule in _rules)
            clone._rules.Add(rule.Key, rule.Value);
        return clone;
    }

    internal void ReplaceWith(RuleContainer<TKey> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        _rules.Clear();
        foreach (var rule in source._rules)
            _rules.Add(rule.Key, rule.Value);
        _sequence = source._sequence;
    }

    internal InventoryRuleState<TKey>? GetRuleStateSnapshot(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _rules.TryGetValue(id, out var entry)
            ? new InventoryRuleState<TKey>(id, entry.Rule, entry.Priority, entry.Enabled)
            : null;
    }

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

    /// <summary>
    /// Evaluates enabled rules against a normalized transaction.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The semantic transaction grouped by item definition and metadata.</param>
    /// <param name="error">A consumer-facing rejection reason wrapped with the failing rule id and type; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every enabled rule allows the transaction; otherwise, <see langword="false"/>.</returns>
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        return CanApply(inventory, transaction, structuralTransaction: null, out error);
    }

    /// <summary>
    /// Evaluates enabled rules against semantic and optional structural transaction data.
    /// </summary>
    /// <param name="inventory">The inventory that would receive the transaction.</param>
    /// <param name="transaction">The semantic transaction grouped by item definition and metadata.</param>
    /// <param name="structuralTransaction">Optional structural transaction containing storage-index changes.</param>
    /// <param name="error">A consumer-facing rejection reason wrapped with the failing rule id and type; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every enabled rule allows the transaction; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Gets the current rules by rule id.
    /// </summary>
    public IReadOnlyDictionary<string, IRulePolicy<TKey>> Rules =>
        _rules.ToDictionary(kv => kv.Key, kv => kv.Value.Rule, StringComparer.Ordinal);

    /// <summary>
    /// Gets or replaces a rule by id.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <returns>The rule associated with <paramref name="id"/>.</returns>
    public IRulePolicy<TKey> this[string id]
    {
        get => _rules[id].Rule;
        set => Set(id, value);
    }

    /// <summary>
    /// Adds or replaces a rule using default priority and enabled state.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to store.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Adds or replaces a rule with explicit priority and enabled state.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to store.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Removes a rule by id.
    /// </summary>
    /// <param name="id">The rule id to remove.</param>
    /// <returns><see langword="true"/> when a rule was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        return _rules.Remove(id);
    }

    /// <summary>
    /// Changes whether a rule participates in validation.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="enabled">The new enabled state.</param>
    /// <returns><see langword="true"/> when the rule exists and was updated; otherwise, <see langword="false"/>.</returns>
    public bool TrySetEnabled(string id, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_rules.TryGetValue(id, out var entry))
            return false;

        _rules[id] = new RuleEntry(entry.Rule, entry.Priority, enabled, entry.Sequence);
        return true;
    }

    /// <summary>
    /// Changes a rule priority.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="priority">The new priority. Higher values run first.</param>
    /// <returns><see langword="true"/> when the rule exists and was updated; otherwise, <see langword="false"/>.</returns>
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
    /// <summary>
    /// Adds a rule using default priority and enabled state.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid or the id already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public void Add(string id, IRulePolicy<TKey> rule)
    {
        Add(id, rule, priority: 0, enabled: true);
    }

    /// <summary>
    /// Adds an enabled rule with explicit priority.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid or the id already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public void Add(string id, IRulePolicy<TKey> rule, int priority)
    {
        Add(id, rule, priority, enabled: true);
    }

    /// <summary>
    /// Adds a rule with explicit enabled state and default priority.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid or the id already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public void Add(string id, IRulePolicy<TKey> rule, bool enabled)
    {
        Add(id, rule, priority: 0, enabled);
    }

    /// <summary>
    /// Adds a rule with explicit priority and enabled state.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid or the id already exists.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
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
