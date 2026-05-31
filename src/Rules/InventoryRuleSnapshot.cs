using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Lazy projected inventory view after applying a normalized transaction.
/// Expensive projection work only happens if a rule asks for state queries.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
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

    /// <summary>
    /// Creates a lazy projected inventory snapshot.
    /// </summary>
    /// <param name="inventory">The inventory to project from.</param>
    /// <param name="transaction">The normalized transaction to project.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> or <paramref name="transaction"/> is <see langword="null"/>.</exception>
    public InventoryRuleSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Gets the projected quantity for an item definition.
    /// </summary>
    /// <param name="definition">The item definition to query.</param>
    /// <returns>The projected quantity, never less than zero.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public int GetQuantity(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        EnsureProjected();
        return _stateById.TryGetValue(definition.Id, out var state)
            ? Math.Max(0, state.Amount)
            : 0;
    }

    /// <summary>
    /// Gets the projected quantity for an item definition id.
    /// </summary>
    /// <param name="definitionId">The item definition id to query.</param>
    /// <returns>The projected quantity, never less than zero.</returns>
    public int GetQuantity(TKey definitionId)
    {
        EnsureProjected();
        return _stateById.TryGetValue(definitionId, out var state)
            ? Math.Max(0, state.Amount)
            : 0;
    }

    /// <summary>
    /// Gets the number of item definitions with a projected quantity greater than zero.
    /// </summary>
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

    /// <summary>
    /// Returns projected definitions with positive quantities.
    /// </summary>
    /// <returns>Definitions and their projected amounts.</returns>
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
