using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Defines an item type that can produce item instances.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class ItemDefinition<TKey>
{
    private readonly AttributeContainer _attributes = new();

    /// <summary>
    /// Gets the identifier for this item type.
    /// </summary>
    public TKey Id { get; }

    /// <summary>
    /// Gets the schema that constrains this item definition.
    /// </summary>
    public ItemSchema<TKey> Schema { get; }

    /// <summary>
    /// Gets the typed attributes defined for this item definition.
    /// </summary>
    public IAttributeView Attributes => _attributes;

    /// <summary>
    /// Gets the tags declared directly on this item definition.
    /// </summary>
    public TagContainer Tags { get; } = new();

    /// <summary>
    /// Creates an item definition using the default schema.
    /// </summary>
    /// <param name="id">The item definition identifier.</param>
    public ItemDefinition(TKey id)
        : this(id, ItemSchema<TKey>.Default)
    {
    }

    /// <summary>
    /// Creates an item definition using the default schema and direct tags.
    /// </summary>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="tags">The tags declared directly on the definition.</param>
    public ItemDefinition(TKey id, params TagKey[] tags)
        : this(id, ItemSchema<TKey>.Default, (IEnumerable<TagKey>?)tags)
    {
    }

    /// <summary>
    /// Creates an item definition using a schema.
    /// </summary>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="schema">The schema that constrains this definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    public ItemDefinition(TKey id, ItemSchema<TKey> schema)
    {
        Id = id;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    /// <summary>
    /// Creates an item definition using a schema and direct tags.
    /// </summary>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="schema">The schema that constrains this definition.</param>
    /// <param name="tags">The tags declared directly on the definition.</param>
    public ItemDefinition(TKey id, ItemSchema<TKey> schema, params TagKey[] tags)
        : this(id, schema, (IEnumerable<TagKey>?)tags)
    {
    }

    /// <summary>
    /// Creates an item definition using a schema and enumerable direct tags.
    /// </summary>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="schema">The schema that constrains this definition.</param>
    /// <param name="tags">The tags declared directly on the definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    protected ItemDefinition(TKey id, ItemSchema<TKey> schema, IEnumerable<TagKey>? tags)
        : this(id, schema)
    {
        DefineTags(tags);
    }

    /// <summary>
    /// Validates this definition against its schema.
    /// </summary>
    /// <exception cref="InvalidOperationException">The definition contains disallowed attributes or is missing required attributes.</exception>
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

    /// <summary>
    /// Defines a typed attribute for this item definition.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value.</param>
    protected void DefineAttribute<T>(AttributeKey<T> key, T value)
    {
        _attributes.Set(key, value);
    }

    /// <summary>
    /// Defines a direct tag for this item definition.
    /// </summary>
    /// <param name="tag">The tag to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <see langword="null"/>.</exception>
    protected void DefineTag(TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        Tags.Add(tag);
    }

    /// <summary>
    /// Defines multiple direct tags for this item definition.
    /// </summary>
    /// <param name="tags">The tags to add. A <see langword="null"/> collection is ignored.</param>
    protected void DefineTags(IEnumerable<TagKey>? tags)
    {
        if (tags == null)
            return;

        foreach (var tag in tags)
            DefineTag(tag);
    }

    /// <summary>
    /// Determines whether this definition directly declares a tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns><see langword="true"/> when the direct tag is present; otherwise, <see langword="false"/>.</returns>
    public bool HasTag(TagKey tag)
    {
        return Tags.Has(tag);
    }
}
