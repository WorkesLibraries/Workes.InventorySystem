using System;
using System.Collections.Generic;
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
    private bool _autoIncrementEnabled;
    private bool _autoIncrementExhausted;
    private AutoIncrementMode? _autoIncrementMode;
    private TKey _nextAutoIncrementId = default!;

    /// <summary>
    /// Gets whether the registry is frozen and can no longer be modified.
    /// </summary>
    public bool Frozen => _frozen;

    /// <summary>
    /// Gets whether registry-owned auto-increment identity assignment is enabled.
    /// </summary>
    /// <remarks>Auto-increment supports only <see cref="int"/> and <see cref="long"/> identifiers.</remarks>
    public bool AutoIncrementEnabled => _autoIncrementEnabled;

    /// <summary>
    /// Gets the active auto-increment mode, or <see langword="null"/> when auto-increment is disabled.
    /// </summary>
    public AutoIncrementMode? AutoIncrementMode => _autoIncrementMode;

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
    /// <param name="definition">The item definition to register.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, the id is duplicated, or validation fails.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public void Register(ItemDefinition<TKey> definition)
    {
        Register(definition, isAutoIncrementRegistration: false);
    }

    private void Register(ItemDefinition<TKey> definition, bool isAutoIncrementRegistration)
    {
        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (definition == null)
            throw new ArgumentNullException("Definition cannot be null");

        if (_autoIncrementEnabled &&
            _autoIncrementMode == Core.AutoIncrementMode.Strict &&
            !isAutoIncrementRegistration)
            throw new InvalidOperationException("Explicit registration is not allowed when auto-increment is enabled in strict mode.");

        if (_definitions.ContainsKey(definition.Id))
            throw new InvalidOperationException("Duplicate item ID.");

        if (_onDefinitionRegistered == null)
            definition.Validate();
        else
            _onDefinitionRegistered.Invoke(definition);

        _definitions.Add(definition.Id, definition);
        AdvanceAutoIncrementCounterAfterExplicitRegistration(definition.Id, isAutoIncrementRegistration);
    }

    /// <summary>
    /// Enables registry-owned auto-increment identity assignment using the default first id, which is 1.
    /// </summary>
    /// <param name="mode">How explicit registrations should interact with generated ids.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, auto-increment is already enabled, the key type is unsupported, or strict mode is enabled after definitions are registered.</exception>
    public void EnableAutoIncrement(AutoIncrementMode mode = Core.AutoIncrementMode.FollowExplicitRegistrations)
    {
        EnableAutoIncrement(CreateDefaultFirstAutoIncrementId(), mode);
    }

    /// <summary>
    /// Enables registry-owned auto-increment identity assignment using the provided first id.
    /// </summary>
    /// <param name="firstId">The first id to generate. Must be greater than zero.</param>
    /// <param name="mode">How explicit registrations should interact with generated ids.</param>
    /// <exception cref="InvalidOperationException">The registry is frozen, auto-increment is already enabled, the key type is unsupported, or strict mode is enabled after definitions are registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstId"/> is less than or equal to zero.</exception>
    public void EnableAutoIncrement(TKey firstId, AutoIncrementMode mode = Core.AutoIncrementMode.FollowExplicitRegistrations)
    {
        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (_autoIncrementEnabled)
            throw new InvalidOperationException("Auto-increment registration is already enabled for this item registry.");

        EnsureSupportedAutoIncrementKeyType();

        if (!IsPositive(firstId))
            throw new ArgumentOutOfRangeException(nameof(firstId), "Auto-increment first id must be greater than zero.");

        if (mode == Core.AutoIncrementMode.Strict && _definitions.Count > 0)
            throw new InvalidOperationException("Strict auto-increment mode cannot be enabled after definitions have already been registered.");

        _autoIncrementEnabled = true;
        _autoIncrementExhausted = false;
        _autoIncrementMode = mode;
        _nextAutoIncrementId = firstId;

        if (mode == Core.AutoIncrementMode.FollowExplicitRegistrations)
            AdvanceAutoIncrementCounterPastExistingDefinitions();
    }

    /// <summary>
    /// Registers a definition by generating its id first and passing that id to a factory.
    /// </summary>
    /// <param name="factory">Creates the definition for the generated id.</param>
    /// <returns>The registered definition.</returns>
    /// <remarks>
    /// This keeps <see cref="ItemDefinition{TKey}.Id"/> immutable while allowing registry-owned id generation.
    /// Auto-increment supports only <see cref="int"/> and <see cref="long"/> identifiers.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Auto-increment is disabled, the registry is frozen, the factory returns null, the returned definition uses a different id, or the counter overflows.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public ItemDefinition<TKey> RegisterAuto(Func<TKey, ItemDefinition<TKey>> factory)
    {
        if (_frozen)
            throw new InvalidOperationException("Item registry is frozen and cannot be modified.");

        if (!_autoIncrementEnabled)
            throw new InvalidOperationException("Auto-increment registration has not been enabled for this item registry.");

        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        var id = GetNextAvailableAutoIncrementId();
        var definition = factory(id);

        if (definition == null)
            throw new InvalidOperationException("Auto-increment definition factory returned null.");

        if (!EqualityComparer<TKey>.Default.Equals(definition.Id, id))
            throw new InvalidOperationException("Auto-increment definition factory must create a definition with the generated id.");

        Register(definition, isAutoIncrementRegistration: true);
        return definition;
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

    internal void Freeze()
    {
        if (_frozen)
            return;

        _onFreeze?.Invoke();
        _frozen = true;
    }

    private TKey GetNextAvailableAutoIncrementId()
    {
        if (_autoIncrementExhausted)
            throw new InvalidOperationException("Auto-increment item definition id overflowed.");

        var id = _nextAutoIncrementId;
        while (_definitions.ContainsKey(id))
        {
            if (!TryIncrementAutoIncrementId(id, out id))
                throw new InvalidOperationException("Auto-increment item definition id overflowed.");

            _nextAutoIncrementId = id;
        }

        return id;
    }

    private void AdvanceAutoIncrementCounterAfterExplicitRegistration(TKey id, bool isAutoIncrementRegistration)
    {
        if (!_autoIncrementEnabled)
            return;

        if (isAutoIncrementRegistration)
        {
            SetNextAutoIncrementIdAfter(id);
            return;
        }

        if (_autoIncrementMode != Core.AutoIncrementMode.FollowExplicitRegistrations)
            return;

        if (!_autoIncrementExhausted && CompareAutoIncrementIds(id, _nextAutoIncrementId) >= 0)
            SetNextAutoIncrementIdAfter(id);
    }

    private void SetNextAutoIncrementIdAfter(TKey id)
    {
        if (TryIncrementAutoIncrementId(id, out var nextId))
        {
            _nextAutoIncrementId = nextId;
            return;
        }

        _autoIncrementExhausted = true;
    }

    private void AdvanceAutoIncrementCounterPastExistingDefinitions()
    {
        foreach (var id in _definitions.Keys)
        {
            if (CompareAutoIncrementIds(id, _nextAutoIncrementId) >= 0)
                SetNextAutoIncrementIdAfter(id);
        }
    }

    private static void EnsureSupportedAutoIncrementKeyType()
    {
        if (!IsSupportedAutoIncrementKeyType)
            throw new InvalidOperationException("Auto-increment registration supports only Int32 and Int64 item definition identifiers.");
    }

    private static bool IsSupportedAutoIncrementKeyType =>
        typeof(TKey) == typeof(int) ||
        typeof(TKey) == typeof(long);

    private static TKey CreateDefaultFirstAutoIncrementId()
    {
        EnsureSupportedAutoIncrementKeyType();

        if (typeof(TKey) == typeof(int))
            return (TKey)(object)1;

        return (TKey)(object)1L;
    }

    private static bool IsPositive(TKey id)
    {
        if (typeof(TKey) == typeof(int))
            return (int)(object)id! > 0;

        if (typeof(TKey) == typeof(long))
            return (long)(object)id! > 0L;

        return false;
    }

    private static int CompareAutoIncrementIds(TKey left, TKey right)
    {
        if (typeof(TKey) == typeof(int))
            return ((int)(object)left!).CompareTo((int)(object)right!);

        if (typeof(TKey) == typeof(long))
            return ((long)(object)left!).CompareTo((long)(object)right!);

        throw new InvalidOperationException("Auto-increment registration supports only Int32 and Int64 item definition identifiers.");
    }

    private static TKey IncrementAutoIncrementId(TKey id)
    {
        if (TryIncrementAutoIncrementId(id, out var nextId))
            return nextId;

        throw new InvalidOperationException("Auto-increment item definition id overflowed.");
    }

    private static bool TryIncrementAutoIncrementId(TKey id, out TKey nextId)
    {
        try
        {
            if (typeof(TKey) == typeof(int))
            {
                nextId = (TKey)(object)checked((int)(object)id! + 1);
                return true;
            }

            if (typeof(TKey) == typeof(long))
            {
                nextId = (TKey)(object)checked((long)(object)id! + 1L);
                return true;
            }
        }
        catch (OverflowException)
        {
            nextId = default!;
            return false;
        }

        throw new InvalidOperationException("Auto-increment registration supports only Int32 and Int64 item definition identifiers.");
    }
}
