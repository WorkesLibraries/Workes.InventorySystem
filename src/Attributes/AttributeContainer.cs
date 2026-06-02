using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Attributes;

/// <summary>
/// Mutable store for typed attribute values addressed by <see cref="AttributeKey{T}"/>.
/// </summary>
/// <remarks>
/// This container remains mutable for inventory-level attributes and other general-purpose holders.
/// Item definitions expose their attributes as <see cref="IAttributeView"/> and should write schema attributes through their definition-class constructors.
/// </remarks>
public sealed class AttributeContainer : IAttributeView
{
    private readonly Dictionary<object, object?> _values = new();

    /// <summary>
    /// Stores or replaces a value for the specified typed attribute key.
    /// </summary>
    /// <typeparam name="T">The value type associated with the attribute key.</typeparam>
    /// <param name="key">The typed attribute key to store.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    public void Set<T>(AttributeKey<T> key, T value)
    {
        _values[key] = value;
    }

    /// <inheritdoc />
    public bool TryGet<T>(AttributeKey<T> key, out T value)
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
    public T GetOrDefault<T>(AttributeKey<T> key, T defaultValue = default!)
    {
        return TryGet(key, out T value) ? value : defaultValue;
    }

    /// <inheritdoc />
    public bool Contains<T>(AttributeKey<T> key)
    {
        return _values.ContainsKey(key);
    }

    /// <inheritdoc />
    public IEnumerable<object> GetAllKeys()
    {
        return _values.Keys;
    }
}
