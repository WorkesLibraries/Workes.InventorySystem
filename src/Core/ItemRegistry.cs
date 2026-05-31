using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace Workes.InventorySystem.Core;

public class ItemRegistry<TKey>
{
    private readonly Dictionary<TKey, ItemDefinition<TKey>> _definitions = new();
    private readonly Dictionary<TKey, TKey> _migrations = new();
    private readonly Action<ItemDefinition<TKey>>? _onDefinitionRegistered;
    private readonly Action? _onFreeze;

    private bool _frozen = false;

    public bool Frozen => _frozen;

    public IEnumerable<ItemDefinition<TKey>> Definitions => _definitions.Values;

    public ItemRegistry()
    {
    }

    internal ItemRegistry(Action<ItemDefinition<TKey>> onDefinitionRegistered, Action onFreeze)
    {
        _onDefinitionRegistered = onDefinitionRegistered;
        _onFreeze = onFreeze;
    }

    public void Register(ItemDefinition<TKey> definition)
    {
        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (definition == null)
            throw new ArgumentNullException("Definition cannot be null");

        if (_definitions.ContainsKey(definition.Id))
            throw new InvalidOperationException("Duplicate item ID.");

        if (_onDefinitionRegistered == null)
            definition.Validate();
        else
            _onDefinitionRegistered.Invoke(definition);

        _definitions.Add(definition.Id, definition);
    }

    public void RegisterMigration(TKey oldId, TKey newId)
    {
        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (oldId == null || newId == null)
            throw new ArgumentNullException("Old or new ID cannot be null");

        if (_migrations.ContainsKey(oldId))
            throw new InvalidOperationException("Migration from this ID already exists.");

        if (_definitions.ContainsKey(oldId))
            throw new InvalidOperationException("Can't migrate from a registered definition.");

        EnsureNoMigrationLoop(newId, oldId);

        _migrations[oldId] = newId;
    }

    private void EnsureNoMigrationLoop(TKey newId, TKey oldId)
    {
        var current = newId;
        while (_migrations.TryGetValue(current, out var migratedId))
        {
            current = migratedId;
            if (EqualityComparer<TKey>.Default.Equals(current, oldId))
                throw new InvalidOperationException("Migration loop detected.");
        }
    }

    public bool Contains(TKey id)
    {
        return _definitions.ContainsKey(id);
    }

    public bool TryGet(TKey id, out ItemDefinition<TKey> definition)
    {
        return _definitions.TryGetValue(id, out definition);
    }

    public ItemDefinition<TKey> Resolve(TKey id)
    {
        while (_migrations.TryGetValue(id, out var migratedId)) // Resolve any migrations recursively
            id = migratedId;

        if (!_definitions.TryGetValue(id, out var definition))
            throw new InvalidOperationException(
                $"Item definition '{id}' could not be resolved.");

        return definition;
    }

    public void Freeze()
    {
        if (_frozen)
            return;

        _onFreeze?.Invoke();
        _frozen = true;
    }
}
