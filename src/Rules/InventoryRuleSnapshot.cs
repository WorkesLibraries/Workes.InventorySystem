using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Lazy projected inventory view after applying a normalized transaction.
/// Expensive projection work only happens if a rule asks for state queries.
/// </summary>
public sealed class InventoryRuleSnapshot<TKey>
{
    private readonly Inventory<TKey> _inventory;
    private readonly NormalizedInventoryTransaction<TKey> _transaction;
    private readonly Dictionary<TKey, DefinitionState> _stateById = new();
    private bool _isProjected;

    private sealed class DefinitionState
    {
        public ItemDefinition<TKey> Definition { get; }
        public int Amount { get; set; }

        public DefinitionState(ItemDefinition<TKey> definition, int amount)
        {
            Definition = definition;
            Amount = amount;
        }
    }

    public InventoryRuleSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public int GetQuantity(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        EnsureProjected();
        return _stateById.TryGetValue(definition.Id, out var state)
            ? Math.Max(0, state.Amount)
            : 0;
    }

    public int GetQuantity(TKey definitionId)
    {
        EnsureProjected();
        return _stateById.TryGetValue(definitionId, out var state)
            ? Math.Max(0, state.Amount)
            : 0;
    }

    public int UniqueDefinitionCount
    {
        get
        {
            EnsureProjected();
            var count = 0;
            foreach (var state in _stateById.Values)
            {
                if (state.Amount > 0)
                    count++;
            }

            return count;
        }
    }

    public IEnumerable<(ItemDefinition<TKey> definition, int amount)> GetDefinitions()
    {
        EnsureProjected();
        foreach (var state in _stateById.Values)
        {
            if (state.Amount > 0)
                yield return (state.Definition, state.Amount);
        }
    }

    private void EnsureProjected()
    {
        if (_isProjected)
            return;

        foreach (var item in _inventory.Items)
        {
            AddAmount(item.Definition, item.Amount);
        }

        foreach (var (definition, _, amount) in _transaction.Removed)
        {
            AddAmount(definition, -amount);
        }

        foreach (var (definition, _, amount) in _transaction.Added)
        {
            AddAmount(definition, amount);
        }

        _isProjected = true;
    }

    private void AddAmount(ItemDefinition<TKey> definition, int delta)
    {
        if (_stateById.TryGetValue(definition.Id, out var state))
        {
            state.Amount += delta;
            return;
        }

        _stateById[definition.Id] = new DefinitionState(definition, delta);
    }
}
