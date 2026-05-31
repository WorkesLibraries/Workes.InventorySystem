using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Limits how many different item definitions may exist in the inventory in total.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class MaxUniqueItemsRule<TKey> : InventorySnapshotRulePolicy<TKey>
{
    private readonly int _maxUniqueDefinitions;

    /// <summary>
    /// Creates a maximum-unique-definitions rule.
    /// </summary>
    /// <param name="maxUniqueDefinitions">The maximum number of item definitions that may have positive projected quantity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxUniqueDefinitions"/> is less than or equal to zero.</exception>
    public MaxUniqueItemsRule(int maxUniqueDefinitions)
    {
        if (maxUniqueDefinitions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUniqueDefinitions), "Max unique definitions must be greater than zero.");
        _maxUniqueDefinitions = maxUniqueDefinitions;
        Id = $"MaxUniqueItemsRule[{_maxUniqueDefinitions}]";
    }

    /// <inheritdoc />
    protected override bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        var uniqueCount = snapshot.UniqueDefinitionCount;
        if (uniqueCount > _maxUniqueDefinitions)
        {
            error = $"Expected inventory to contain at most {_maxUniqueDefinitions} different item definition(s) after the transaction, but it would contain {uniqueCount}.";
            return false;
        }

        error = null;
        return true;
    }
}
