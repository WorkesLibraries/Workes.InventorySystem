using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Attributes;

/// <summary>
/// Declares typed attribute keys that are valid in an item catalog.
/// </summary>
public sealed class AttributeCatalog
{
    private readonly Dictionary<string, AttributeDefinition> _attributes = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets all attributes declared in this catalog.
    /// </summary>
    public IEnumerable<AttributeDefinition> All => _attributes.Values;

    /// <summary>
    /// Defines a typed attribute from its string identifier.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="id">The stable attribute identifier.</param>
    /// <returns>The canonical typed attribute key.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The id is already declared with a different value type.</exception>
    public AttributeKey<T> Define<T>(string id)
    {
        return Define(new AttributeKey<T>(id));
    }

    /// <summary>
    /// Defines a typed attribute key.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="key">The attribute key to define.</param>
    /// <returns>The canonical typed attribute key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The id is already declared with a different value type.</exception>
    public AttributeKey<T> Define<T>(AttributeKey<T> key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (_attributes.TryGetValue(key.Id, out var existing))
        {
            if (existing.ValueType != typeof(T))
                throw new InvalidOperationException($"Attribute '{key.Id}' is already declared with value type '{existing.ValueType.Name}'.");

            return (AttributeKey<T>)existing.Key;
        }

        var definition = new AttributeDefinition(key.Id, typeof(T), key);
        _attributes.Add(key.Id, definition);
        return key;
    }

    /// <summary>
    /// Gets a declared typed attribute key by id.
    /// </summary>
    /// <typeparam name="T">The expected attribute value type.</typeparam>
    /// <param name="id">The attribute identifier.</param>
    /// <returns>The canonical typed attribute key.</returns>
    /// <exception cref="InvalidOperationException">The attribute is not declared or is declared with a different value type.</exception>
    public AttributeKey<T> Get<T>(string id)
    {
        if (!TryGet<T>(id, out var key) || key == null)
            throw new InvalidOperationException($"Attribute '{id}' is not declared in this attribute catalog with value type '{typeof(T).Name}'.");

        return key;
    }

    /// <summary>
    /// Attempts to get a declared typed attribute key by id.
    /// </summary>
    /// <typeparam name="T">The expected attribute value type.</typeparam>
    /// <param name="id">The attribute identifier.</param>
    /// <param name="key">The canonical typed key when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the attribute exists with the requested type; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(string id, out AttributeKey<T>? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_attributes.TryGetValue(id, out var definition))
            return false;

        if (definition.ValueType != typeof(T))
            return false;

        key = (AttributeKey<T>)definition.Key;
        return true;
    }

    /// <summary>
    /// Attempts to get the canonical catalog key for a typed attribute key.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="key">The key to resolve.</param>
    /// <param name="catalogKey">The canonical key when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the attribute exists with the requested type; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(AttributeKey<T> key, out AttributeKey<T>? catalogKey)
    {
        catalogKey = null;
        if (key == null)
            return false;

        return TryGet(key.Id, out catalogKey);
    }

    /// <summary>
    /// Determines whether a typed attribute key is declared in the catalog.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="key">The key to search for.</param>
    /// <returns><see langword="true"/> when the key is declared with the same value type; otherwise, <see langword="false"/>.</returns>
    public bool Contains<T>(AttributeKey<T> key)
    {
        return TryGet(key, out _);
    }

    /// <summary>
    /// Determines whether an attribute key object is declared in the catalog.
    /// </summary>
    /// <param name="key">The attribute key object to search for.</param>
    /// <returns><see langword="true"/> when a matching id and value type are declared; otherwise, <see langword="false"/>.</returns>
    public bool Contains(object key)
    {
        if (key == null)
            return false;

        var keyType = key.GetType();
        if (!keyType.IsGenericType || keyType.GetGenericTypeDefinition() != typeof(AttributeKey<>))
            return false;

        var idProperty = keyType.GetProperty(nameof(AttributeKey<int>.Id));
        var id = idProperty?.GetValue(key) as string;
        if (id == null)
            return false;

        if (!_attributes.TryGetValue(id, out var definition))
            return false;

        return definition.ValueType == keyType.GetGenericArguments()[0];
    }
}
