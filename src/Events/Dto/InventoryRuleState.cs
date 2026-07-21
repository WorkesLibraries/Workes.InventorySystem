using System;
using Workes.InventorySystem.Rules;

namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Captures one immutable rule-container entry at the time an inventory configuration event is created.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class InventoryRuleState<TKey>
{
    /// <summary>
    /// Creates a rule-state snapshot.
    /// </summary>
    /// <param name="id">The stable rule id.</param>
    /// <param name="policy">The rule policy stored under <paramref name="id"/>.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is <see langword="null"/>.</exception>
    public InventoryRuleState(string id, IRulePolicy<TKey> policy, int priority, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be null or empty.", nameof(id));

        Id = id;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Priority = priority;
        Enabled = enabled;
    }

    /// <summary>Gets the stable rule id.</summary>
    public string Id { get; }

    /// <summary>Gets the rule policy stored under <see cref="Id"/>.</summary>
    public IRulePolicy<TKey> Policy { get; }

    /// <summary>Gets the rule priority. Higher values run first.</summary>
    public int Priority { get; }

    /// <summary>Gets whether the rule participates in validation.</summary>
    public bool Enabled { get; }
}
