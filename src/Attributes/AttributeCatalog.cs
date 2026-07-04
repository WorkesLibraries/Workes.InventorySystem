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
    /// <returns>The declared attribute metadata.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The id is already declared.</exception>
    public AttributeDefinition Define<T>(string id)
    {
        var key = new AttributeKey<T>(id);

        if (_attributes.TryGetValue(key.Id, out var existing))
            throw new InvalidOperationException(
                $"Attribute '{key.Id}' is already declared with value type '{existing.ValueType.Name}'.");

        var definition = new AttributeDefinition(key.Id, typeof(T), key);
        _attributes.Add(key.Id, definition);
        return definition;
    }

    /// <summary>
    /// Gets a declared typed attribute key by id.
    /// </summary>
    /// <typeparam name="T">The expected attribute value type.</typeparam>
    /// <param name="id">The attribute identifier.</param>
    /// <returns>The declared attribute metadata.</returns>
    /// <exception cref="InvalidOperationException">The attribute is not declared or is declared with a different value type.</exception>
    public AttributeDefinition Get<T>(string id)
    {
        if (!TryGet<T>(id, out var definition) || definition == null)
            throw new InvalidOperationException($"Attribute '{id}' is not declared in this attribute catalog with value type '{typeof(T).Name}'.");

        return definition;
    }

    /// <summary>
    /// Attempts to get a declared typed attribute key by id.
    /// </summary>
    /// <typeparam name="T">The expected attribute value type.</typeparam>
    /// <param name="id">The attribute identifier.</param>
    /// <param name="definition">The declared attribute metadata when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the attribute exists with the requested type; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(string id, out AttributeDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_attributes.TryGetValue(id, out var found))
            return false;

        if (found.ValueType != typeof(T))
            return false;

        definition = found;
        return true;
    }

    /// <summary>
    /// Determines whether a typed attribute id is declared in the catalog.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="id">The attribute id to search for.</param>
    /// <returns><see langword="true"/> when the key is declared with the same value type; otherwise, <see langword="false"/>.</returns>
    public bool Contains<T>(string id)
    {
        return TryGet<T>(id, out _);
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
