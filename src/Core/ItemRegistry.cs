using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Registers explicit item definitions and resolves definition id migrations.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class ItemRegistry<TKey>
{
    private readonly Dictionary<TKey, ItemDefinition<TKey>> _definitions = new();
    private readonly Dictionary<TKey, ItemDefinition<TKey>> _migrations = new();
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

    internal ItemRegistry(Action<ItemDefinition<TKey>> onDefinitionRegistered, Action onFreeze)
    {
        _onDefinitionRegistered = onDefinitionRegistered;
        _onFreeze = onFreeze;
    }

    /// <summary>
    /// Registers an item definition.
    /// </summary>
    /// <remarks>Definition ids are explicit stable identities used by registry lookup, serialization, and migrations.</remarks>
    /// <param name="definition">The item definition to register.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, the id is duplicated, or validation fails.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public void Register(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (_definitions.ContainsKey(definition.Id))
            throw new InvalidOperationException("Duplicate item ID.");

        if (_onDefinitionRegistered == null)
            definition.Validate();
        else
            _onDefinitionRegistered.Invoke(definition);

        _definitions.Add(definition.Id, definition);
    }

    /// <summary>
    /// Registers a migration from an obsolete definition id to a registered replacement definition.
    /// </summary>
    /// <remarks>
    /// Migrations map obsolete explicit ids to canonical registered replacement definitions for save compatibility.
    /// The replacement definition must already be registered in this registry, and detached same-id definitions are
    /// rejected. Multiple obsolete ids can point to the same replacement definition.
    /// </remarks>
    /// <param name="oldId">The obsolete definition id.</param>
    /// <param name="replacementDefinition">The registered replacement definition.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, the migration duplicates an existing mapping, or the replacement definition is not the registered definition instance.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="oldId"/> or <paramref name="replacementDefinition"/> is <see langword="null"/>.</exception>
    public void RegisterMigration(TKey oldId, ItemDefinition<TKey> replacementDefinition)
    {
        if (oldId == null)
            throw new ArgumentNullException(nameof(oldId));

        if (replacementDefinition == null)
            throw new ArgumentNullException(nameof(replacementDefinition));

        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (_definitions.ContainsKey(oldId))
            throw new InvalidOperationException("Can't migrate from a registered definition.");

        if (_migrations.ContainsKey(oldId))
            throw new InvalidOperationException("Migration from this ID already exists.");

        if (!_definitions.TryGetValue(replacementDefinition.Id, out var registeredDefinition))
            throw new InvalidOperationException(
                $"Migration replacement definition '{replacementDefinition.Id}' is not registered in this item registry.");

        if (!ReferenceEquals(registeredDefinition, replacementDefinition))
            throw new InvalidOperationException(
                $"Migration replacement definition '{replacementDefinition.Id}' is not the registered definition instance for this item registry.");

        _migrations[oldId] = replacementDefinition;
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
    /// Resolves a registered definition id or obsolete id registered through migrations.
    /// </summary>
    /// <param name="id">The current or migrated definition id.</param>
    /// <returns>The resolved registered definition.</returns>
    /// <exception cref="InvalidOperationException">No registered definition can be resolved.</exception>
    public ItemDefinition<TKey> Resolve(TKey id)
    {
        if (_migrations.TryGetValue(id, out var migratedDefinition))
            return migratedDefinition;

        if (!_definitions.TryGetValue(id, out var definition))
            throw new InvalidOperationException(
                $"Item definition '{id}' could not be resolved.");

        return definition;
    }

    internal void Freeze()
    {
        if (_frozen)
            return;

        _onFreeze?.Invoke();
        _frozen = true;
    }
}
