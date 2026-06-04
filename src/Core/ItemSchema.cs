using System;
using System.Collections.Generic;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Defines attribute and tag requirements shared by item definitions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class ItemSchema<TKey>
{
    private readonly Dictionary<object, SchemaAttribute> _attributes = new();
    private readonly List<TagKey> _directTags = new();

    /// <summary>
    /// Gets the default schema used when no explicit schema is supplied.
    /// </summary>
    public static ItemSchema<TKey> Default { get; } = new ItemSchema<TKey>("default");

    /// <summary>
    /// Gets the stable schema identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the parent schema whose inheritable attributes and tags are inherited by this schema.
    /// </summary>
    public ItemSchema<TKey>? Parent { get; private set; }

    /// <summary>
    /// Gets whether this schema is frozen and can no longer be modified.
    /// </summary>
    public bool Frozen { get; private set; }

    /// <summary>
    /// Gets tags declared directly on this schema.
    /// </summary>
    public IReadOnlyCollection<string> DirectTags => _directTags.ConvertAll(tag => tag.Id).AsReadOnly();

    /// <summary>
    /// Gets attributes required directly by this schema.
    /// </summary>
    public IReadOnlyCollection<SchemaAttribute> DirectAttributes => _attributes.Values;

    private ItemSchema(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Schema id cannot be null or empty.", nameof(id));

        Id = id;
    }

    /// <summary>
    /// Creates a mutable schema with the specified id.
    /// </summary>
    /// <param name="id">The schema identifier.</param>
    /// <returns>The created schema.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public static ItemSchema<TKey> Create(string id)
    {
        return new ItemSchema<TKey>(id);
    }

    /// <summary>
    /// Sets the parent schema.
    /// </summary>
    /// <param name="parent">The parent schema.</param>
    /// <returns>This schema for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The schema is frozen.</exception>
    public ItemSchema<TKey> WithParent(ItemSchema<TKey> parent)
    {
        EnsureMutable();
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        return this;
    }

    /// <summary>
    /// Requires a typed attribute by string id on definitions using this schema.
    /// </summary>
    /// <typeparam name="T">The required attribute value type.</typeparam>
    /// <param name="id">The required attribute id.</param>
    /// <param name="inherited">Whether child schemas inherit this requirement.</param>
    /// <returns>This schema for fluent configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The schema is frozen or already defines the attribute.</exception>
    public ItemSchema<TKey> RequireAttribute<T>(string id, bool inherited = true)
    {
        var key = new AttributeKey<T>(id);

        EnsureMutable();
        if (_attributes.ContainsKey(key))
            throw new InvalidOperationException($"Schema '{Id}' already defines attribute '{key}'.");

        _attributes.Add(key, new SchemaAttribute(key, inherited));
        return this;
    }

    /// <summary>
    /// Requires a typed attribute by string id on definitions using this schema.
    /// </summary>
    /// <typeparam name="T">The required attribute value type.</typeparam>
    /// <param name="id">The required attribute id.</param>
    /// <returns>This schema for fluent configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public ItemSchema<TKey> Require<T>(string id)
    {
        return RequireAttribute<T>(id);
    }

    /// <summary>
    /// Adds a direct tag requirement to this schema.
    /// </summary>
    /// <param name="id">The tag id to add.</param>
    /// <returns>This schema for fluent configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not a valid namespaced tag id.</exception>
    /// <exception cref="InvalidOperationException">The schema is frozen.</exception>
    public ItemSchema<TKey> AddTag(string id)
    {
        var tag = TagKey.Parse(id);

        EnsureMutable();
        if (!_directTags.Contains(tag))
            _directTags.Add(tag);
        return this;
    }

    internal IEnumerable<TagKey> DirectTagKeys => _directTags;

    internal IEnumerable<object> GetRequiredAttributeKeys()
    {
        foreach (var attribute in GetResolvedAttributesForDefinition())
            yield return attribute.Key;
    }

    internal IEnumerable<object> GetDirectRequiredAttributeKeys()
    {
        foreach (var attribute in _attributes.Values)
            yield return attribute.Key;
    }

    internal IEnumerable<object> GetDirectAttributeKeys()
    {
        return GetDirectRequiredAttributeKeys();
    }

    internal IEnumerable<SchemaAttribute> GetResolvedAttributesForDefinition()
    {
        foreach (var attribute in ResolveAttributesForDefinition())
            yield return attribute;
    }

    internal IEnumerable<object> GetInheritedAttributeKeysForChildren()
    {
        foreach (var attribute in GetInheritedAttributesForChildren())
            yield return attribute.Key;
    }

    internal IEnumerable<SchemaAttribute> GetInheritedAttributesForChildren()
    {
        foreach (var attribute in ResolveAttributesForChildren())
            yield return attribute;
    }

    internal IEnumerable<TagKey> GetResolvedDirectSchemaTags()
    {
        if (Parent != null)
        {
            foreach (var tag in Parent.GetResolvedDirectSchemaTags())
                yield return tag;
        }

        foreach (var tag in _directTags)
            yield return tag;
    }

    internal void Freeze()
    {
        Frozen = true;
    }

    private List<SchemaAttribute> ResolveAttributesForDefinition()
    {
        var resolved = new List<SchemaAttribute>();
        if (Parent != null)
            resolved.AddRange(Parent.ResolveAttributesForChildren());

        resolved.AddRange(_attributes.Values);
        return resolved;
    }

    private List<SchemaAttribute> ResolveAttributesForChildren()
    {
        var resolved = new List<SchemaAttribute>();
        if (Parent != null)
            resolved.AddRange(Parent.ResolveAttributesForChildren());

        foreach (var attribute in _attributes.Values)
        {
            if (attribute.Inherited)
                resolved.Add(attribute);
        }

        return resolved;
    }

    private void EnsureMutable()
    {
        if (Frozen)
            throw new InvalidOperationException($"Schema '{Id}' is frozen and cannot be modified.");
    }
}
