using System;
using System.Collections.Generic;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

public sealed class ItemSchema<TKey>
{
    private readonly Dictionary<object, SchemaAttribute> _attributes = new();
    private readonly List<TagKey> _directTags = new();

    public static ItemSchema<TKey> Default { get; } = new ItemSchema<TKey>("default");

    public string Id { get; }
    public ItemSchema<TKey>? Parent { get; private set; }
    public bool Frozen { get; private set; }

    public IReadOnlyCollection<TagKey> DirectTags => _directTags.AsReadOnly();
    public IReadOnlyCollection<SchemaAttribute> DirectAttributes => _attributes.Values;

    private ItemSchema(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Schema id cannot be null or empty.", nameof(id));

        Id = id;
    }

    public static ItemSchema<TKey> Create(string id)
    {
        return new ItemSchema<TKey>(id);
    }

    public ItemSchema<TKey> WithParent(ItemSchema<TKey> parent)
    {
        EnsureMutable();
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        return this;
    }

    public ItemSchema<TKey> RequireAttribute<T>(AttributeKey<T> key, bool inherited = true)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        EnsureMutable();
        if (_attributes.ContainsKey(key))
            throw new InvalidOperationException($"Schema '{Id}' already defines attribute '{key}'.");

        _attributes.Add(key, new SchemaAttribute(key, inherited));
        return this;
    }

    public ItemSchema<TKey> Require<T>(AttributeKey<T> key)
    {
        return RequireAttribute(key);
    }

    public ItemSchema<TKey> AddTag(TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        EnsureMutable();
        if (!_directTags.Contains(tag))
            _directTags.Add(tag);
        return this;
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
