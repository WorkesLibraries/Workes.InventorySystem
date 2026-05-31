using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Rules;

public class OnlyAllowItemsRule<TKey> : IRulePolicy<TKey>
{
    private readonly HashSet<ItemDefinition<TKey>> _allowed;
    public string Id { get; }

    public OnlyAllowItemsRule(params ItemDefinition<TKey>[] allowed)
    {
        if (allowed == null)
            throw new ArgumentNullException(nameof(allowed));
        foreach (var item in allowed)
        {
            if (item == null)
                throw new ArgumentException("Allowed items cannot contain null.", nameof(allowed));
        }

        _allowed = new HashSet<ItemDefinition<TKey>>(allowed!);
        var allowedDescription = string.Join(", ", allowed.Select(x => $"{x!.Id}"));
        Id = $"OnlyAllowItems[{allowedDescription}]";
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        var allowedDescription = string.Join(", ", _allowed.Select(x => $"{x!.Id}"));
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!_allowed.Contains(definition))
            {
                error = $"Expected transaction to only add allowed items. OnlyAllowItemsRule allows: {allowedDescription}, but it attempted to add '{definition.Id}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
