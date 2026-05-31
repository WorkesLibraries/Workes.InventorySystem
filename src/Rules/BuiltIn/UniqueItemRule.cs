using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Limits how many instances of each item definition may exist in the inventory.
/// For example, maxInstancesPerItem = 1 enforces classic "unique item" semantics.
/// </summary>
public class UniqueItemRule<TKey> : InventorySnapshotRulePolicy<TKey>, IInventoryStructuralRulePolicy<TKey>
{
    private readonly int _maxInstancesPerItem;

    public UniqueItemRule(int maxInstancesPerItem)
    {
        if (maxInstancesPerItem <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstancesPerItem), "Max instances must be greater than zero.");
        _maxInstancesPerItem = maxInstancesPerItem;
        Id = $"UniqueItemRule[{_maxInstancesPerItem}]";
    }

    protected override bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        error = null;
        return true;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        out string? error)
    {
        var instanceCounts = new Dictionary<TKey, (ItemDefinition<TKey> definition, int count)>();

        foreach (var item in inventory.Items)
            AddCount(instanceCounts, item.Definition, 1);

        foreach (var (_, instance) in transaction.Removed)
            AddCount(instanceCounts, instance.Definition, -1);

        foreach (var (instance, _) in transaction.Added)
            AddCount(instanceCounts, instance.Definition, 1);

        foreach (var state in instanceCounts.Values)
        {
            if (state.count > _maxInstancesPerItem)
            {
                error = $"Expected inventory to contain at most {_maxInstancesPerItem} instance(s) of item '{state.definition.Id}' after the transaction, but it would contain {state.count}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static void AddCount(
        Dictionary<TKey, (ItemDefinition<TKey> definition, int count)> counts,
        ItemDefinition<TKey> definition,
        int delta)
    {
        if (counts.TryGetValue(definition.Id, out var state))
        {
            counts[definition.Id] = (state.definition, state.count + delta);
            return;
        }

        counts[definition.Id] = (definition, delta);
    }
}
