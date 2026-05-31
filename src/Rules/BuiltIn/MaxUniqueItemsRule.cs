using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Limits how many different item definitions may exist in the inventory in total.
/// </summary>
public class MaxUniqueItemsRule<TKey> : InventorySnapshotRulePolicy<TKey>
{
    private readonly int _maxUniqueDefinitions;

    public MaxUniqueItemsRule(int maxUniqueDefinitions)
    {
        if (maxUniqueDefinitions <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUniqueDefinitions), "Max unique definitions must be greater than zero.");
        _maxUniqueDefinitions = maxUniqueDefinitions;
        Id = $"MaxUniqueItemsRule[{_maxUniqueDefinitions}]";
    }

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
