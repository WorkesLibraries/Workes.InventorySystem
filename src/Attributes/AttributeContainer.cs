using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Attributes;

/// <summary>
/// Mutable store for typed attribute values addressed by string identifier.
/// </summary>
/// <remarks>
/// This container remains mutable for inventory-level attributes and other general-purpose holders.
/// Item definitions expose their attributes as <see cref="IAttributeView"/> and should write schema attributes through their definition-class constructors.
/// </remarks>
public sealed class AttributeContainer : IAttributeView
{
    private readonly Dictionary<object, object?> _values = new();

    /// <summary>
    /// Stores or replaces a value for the specified typed attribute id.
    /// </summary>
    /// <typeparam name="T">The value type associated with the attribute key.</typeparam>
    /// <param name="id">The attribute id to store.</param>
    /// <param name="value">The value to associate with <paramref name="id"/>.</param>
    public void Set<T>(string id, T value)
    {
        Set(new AttributeKey<T>(id), value);
    }

    internal void Set<T>(AttributeKey<T> key, T value)
    {
        _values[key] = value;
    }

    /// <inheritdoc />
    public bool TryGet<T>(string id, out T value)
    {
        return TryGet(new AttributeKey<T>(id), out value);
    }

    internal bool TryGet<T>(AttributeKey<T> key, out T value)
    {
        if (_values.TryGetValue(key, out var obj) && obj is T casted)
        {
            value = casted;
            return true;
        }

        value = default!;
        return false;
    }

    /// <inheritdoc />
    public T GetOrDefault<T>(string id, T defaultValue = default!)
    {
        return TryGet(id, out T value) ? value : defaultValue;
    }

    /// <inheritdoc />
    public bool Contains<T>(string id)
    {
        return _values.ContainsKey(new AttributeKey<T>(id));
    }

    /// <inheritdoc />
    public IEnumerable<object> GetAllKeys()
    {
        return _values.Keys;
    }

    internal IEnumerable<(string id, Type valueType, object? value)> GetSnapshotEntries()
    {
        foreach (var pair in _values)
        {
            if (pair.Key is IAttributeKey key)
                yield return (key.Id, key.ValueType, pair.Value);
        }
    }
}
