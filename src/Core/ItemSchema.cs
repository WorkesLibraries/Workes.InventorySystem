using System;
using System.Collections.Generic;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Defines attribute and tag requirements shared by item definitions.
/// </summary>
/// <remarks>
/// Custom item definition classes should normally own their schemas with <see cref="CreateFor{TDefinition}(string)"/>.
/// Use <see cref="Create(string)"/> for advanced shared-schema scenarios where no definition type owns the schema.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class ItemSchema<TKey>
{
    private readonly Dictionary<object, SchemaAttribute> _attributes = new();
    private readonly List<string> _directTags = new();

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
    /// Gets the item definition type that owns this schema, or <see langword="null"/> for unowned shared schemas.
    /// </summary>
    /// <remarks>
    /// Owned schemas are intended to be declared as static members on item definition classes. Catalog validation
    /// rejects an owned schema when it is used by an unrelated definition type.
    /// </remarks>
    public Type? OwnerDefinitionType { get; }

    /// <summary>
    /// Gets whether this schema is frozen and can no longer be modified.
    /// </summary>
    public bool Frozen { get; private set; }

    /// <summary>
    /// Gets tags declared directly on this schema.
    /// </summary>
    public IReadOnlyCollection<string> DirectTags => _directTags.AsReadOnly();

    /// <summary>
    /// Gets attributes required directly by this schema.
    /// </summary>
    public IReadOnlyCollection<SchemaAttribute> DirectAttributes => _attributes.Values;

    private ItemSchema(string id, Type? ownerDefinitionType = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Schema id cannot be null or empty.", nameof(id));

        Id = id;
        OwnerDefinitionType = ownerDefinitionType;
    }

    /// <summary>
    /// Creates a mutable unowned schema with the specified id.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="CreateFor{TDefinition}(string)"/> for schemas that belong to a specific
    /// <see cref="ItemDefinition{TKey}"/> subclass. Unowned schemas remain useful for advanced shared-schema workflows.
    /// </remarks>
    /// <param name="id">The schema identifier.</param>
    /// <returns>The created schema.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public static ItemSchema<TKey> Create(string id)
    {
        return new ItemSchema<TKey>(id);
    }

    /// <summary>
    /// Creates a mutable schema owned by an item definition type.
    /// </summary>
    /// <typeparam name="TDefinition">The item definition type that owns the schema.</typeparam>
    /// <param name="id">The schema identifier.</param>
    /// <returns>The created schema.</returns>
    /// <remarks>
    /// This is the preferred schema authoring path for custom definition classes. Catalog validation allows the
    /// schema only on <typeparamref name="TDefinition"/> or types derived from it.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public static ItemSchema<TKey> CreateFor<TDefinition>(string id)
        where TDefinition : ItemDefinition<TKey>
    {
        return new ItemSchema<TKey>(id, typeof(TDefinition));
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
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        ValidateParentOwnership(parent);
        Parent = parent;
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
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The schema is frozen.</exception>
    /// <remarks>Tag ids are validated against the owning catalog's tag mode when the catalog is frozen.</remarks>
    public ItemSchema<TKey> AddTag(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Tag id cannot be null or empty.", nameof(id));

        EnsureMutable();
        if (!ContainsDirectTag(id))
            _directTags.Add(id);
        return this;
    }

    internal IEnumerable<string> DirectTagIds => _directTags;

    internal IEnumerable<TagKey> DirectTagKeys(TagCatalog catalog)
    {
        foreach (var tag in _directTags)
            yield return catalog.GetKey(tag);
    }

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

    internal IEnumerable<TagKey> GetResolvedDirectSchemaTags(TagCatalog catalog)
    {
        if (Parent != null)
        {
            foreach (var tag in Parent.GetResolvedDirectSchemaTags(catalog))
                yield return tag;
        }

        foreach (var tag in _directTags)
            yield return catalog.GetKey(tag);
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

    private bool ContainsDirectTag(string id)
    {
        foreach (var tag in _directTags)
        {
            if (string.Equals(tag, id, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ValidateParentOwnership(ItemSchema<TKey> parent)
    {
        if (OwnerDefinitionType == null || parent.OwnerDefinitionType == null)
            return;

        if (parent.OwnerDefinitionType.IsAssignableFrom(OwnerDefinitionType))
            return;

        throw new InvalidOperationException(
            $"Schema '{Id}' is owned by definition type '{OwnerDefinitionType.Name}' and cannot use parent schema '{parent.Id}' owned by definition type '{parent.OwnerDefinitionType.Name}'.");
    }
}
