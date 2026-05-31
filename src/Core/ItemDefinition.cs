using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Core;

public class ItemDefinition<TKey>
{
    private readonly AttributeContainer _attributes = new();

    public TKey Id { get; }

    public ItemSchema<TKey> Schema { get; }
    public IAttributeView Attributes => _attributes;
    public TagContainer Tags { get; } = new();

    public ItemDefinition(TKey id)
        : this(id, ItemSchema<TKey>.Default)
    {
    }

    public ItemDefinition(TKey id, params TagKey[] tags)
        : this(id, ItemSchema<TKey>.Default, (IEnumerable<TagKey>?)tags)
    {
    }

    public ItemDefinition(TKey id, ItemSchema<TKey> schema)
    {
        Id = id;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public ItemDefinition(TKey id, ItemSchema<TKey> schema, params TagKey[] tags)
        : this(id, schema, (IEnumerable<TagKey>?)tags)
    {
    }

    protected ItemDefinition(TKey id, ItemSchema<TKey> schema, IEnumerable<TagKey>? tags)
        : this(id, schema)
    {
        DefineTags(tags);
    }

    public void Validate()
    {
        var schemaKeys = new HashSet<object>(Schema.GetRequiredAttributeKeys());
        foreach (var actual in _attributes.GetAllKeys())
        {
            if (!schemaKeys.Contains(actual))
                throw new InvalidOperationException($"Attribute '{actual}' is not allowed by schema '{Schema.Id}' for definition '{Id}'.");
        }

        foreach (var required in schemaKeys)
        {
            if (!_attributes.GetAllKeys().Contains(required))
                throw new InvalidOperationException($"Missing required attribute: {required}");
        }
    }

    protected void DefineAttribute<T>(AttributeKey<T> key, T value)
    {
        _attributes.Set(key, value);
    }

    protected void DefineTag(TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        Tags.Add(tag);
    }

    protected void DefineTags(IEnumerable<TagKey>? tags)
    {
        if (tags == null)
            return;

        foreach (var tag in tags)
            DefineTag(tag);
    }

    public bool HasTag(TagKey tag)
    {
        return Tags.Has(tag);
    }
}
