using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Registers item definitions and resolves definition id migrations.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class ItemRegistry<TKey>
{
    private readonly Dictionary<TKey, ItemDefinition<TKey>> _definitions = new();
    private readonly Dictionary<TKey, TKey> _migrations = new();
    private readonly Action<ItemDefinition<TKey>>? _onDefinitionRegistered;
    private readonly Action? _onFreeze;

    private bool _frozen = false;

    /// <summary>
    /// Gets whether the registry is frozen and can no longer be modified.
    /// </summary>
    public bool Frozen => _frozen;

    /// <summary>
    /// Gets the registered item definitions.
    /// </summary>
    public IEnumerable<ItemDefinition<TKey>> Definitions => _definitions.Values;

    /// <summary>
    /// Creates an item registry.
    /// </summary>
    public ItemRegistry()
    {
    }

    internal ItemRegistry(Action<ItemDefinition<TKey>> onDefinitionRegistered, Action onFreeze)
    {
        _onDefinitionRegistered = onDefinitionRegistered;
        _onFreeze = onFreeze;
    }

    /// <summary>
    /// Registers an item definition.
    /// </summary>
    /// <param name="definition">The item definition to register.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, the id is duplicated, or validation fails.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Registers a migration from an old definition id to a new definition id.
    /// </summary>
    /// <param name="oldId">The obsolete definition id.</param>
    /// <param name="newId">The replacement definition id.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, the migration duplicates an existing mapping, or the mapping creates a loop.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="oldId"/> or <paramref name="newId"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Determines whether a definition id is registered.
    /// </summary>
    /// <param name="id">The definition id to search for.</param>
    /// <returns><see langword="true"/> when the id is registered; otherwise, <see langword="false"/>.</returns>
    public bool Contains(TKey id)
    {
        return _definitions.ContainsKey(id);
    }

    /// <summary>
    /// Attempts to get a registered definition by id.
    /// </summary>
    /// <param name="id">The definition id to search for.</param>
    /// <param name="definition">The registered definition when found.</param>
    /// <returns><see langword="true"/> when the definition is registered; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(TKey id, out ItemDefinition<TKey> definition)
    {
        return _definitions.TryGetValue(id, out definition);
    }

    /// <summary>
    /// Resolves a definition id, following registered migrations.
    /// </summary>
    /// <param name="id">The current or migrated definition id.</param>
    /// <returns>The resolved registered definition.</returns>
    /// <exception cref="InvalidOperationException">No registered definition can be resolved.</exception>
    public ItemDefinition<TKey> Resolve(TKey id)
    {
        while (_migrations.TryGetValue(id, out var migratedId)) // Resolve any migrations recursively
            id = migratedId;

        if (!_definitions.TryGetValue(id, out var definition))
            throw new InvalidOperationException(
                $"Item definition '{id}' could not be resolved.");

        return definition;
    }

    /// <summary>
    /// Freezes the registry so definitions and migrations can no longer be changed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Validation performed by the owning catalog fails.</exception>
    public void Freeze()
    {
        if (_frozen)
            return;

        _onFreeze?.Invoke();
        _frozen = true;
    }
}
