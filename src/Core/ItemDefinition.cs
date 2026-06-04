using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Defines an item type that can produce item instances.
/// </summary>
/// <remarks>
/// Simple definitions can use the default-schema public constructors. Custom definition classes that need attributes
/// or schema tags should normally declare a static schema with <see cref="ItemSchema{TKey}.CreateFor{TDefinition}(string)"/>
/// and pass it to a protected schema constructor. Do not expose public constructors that accept schemas.
/// </remarks>
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
    /// <remarks>
    /// Custom definition classes should normally use a class-owned schema created with
    /// <see cref="ItemSchema{TKey}.CreateFor{TDefinition}(string)"/>.
    /// </remarks>
    public ItemSchema<TKey> Schema { get; }

    /// <summary>
    /// Gets a read-only view of the typed attributes defined for this item definition.
    /// </summary>
    /// <remarks>Derived definition classes should write schema attributes during construction with <see cref="DefineAttribute{T}(string, T)"/>.</remarks>
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
    public ItemDefinition(TKey id, params string[] tags)
        : this(id, ItemSchema<TKey>.Default, (IEnumerable<string>?)tags)
    {
    }

    /// <summary>
    /// Creates an item definition using a schema.
    /// </summary>
    /// <remarks>
    /// This constructor is protected so definition classes can pass their own class-owned schemas without making
    /// schema selection a normal caller concern.
    /// </remarks>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="schema">The schema that constrains this definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    protected ItemDefinition(TKey id, ItemSchema<TKey> schema)
    {
        Id = id;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    /// <summary>
    /// Creates an item definition using a schema and enumerable direct tags.
    /// </summary>
    /// <remarks>
    /// This constructor is protected so definition classes can pass their own class-owned schemas without making
    /// schema selection a normal caller concern.
    /// </remarks>
    /// <param name="id">The item definition identifier.</param>
    /// <param name="schema">The schema that constrains this definition.</param>
    /// <param name="tags">The tags declared directly on the definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    protected ItemDefinition(TKey id, ItemSchema<TKey> schema, IEnumerable<string>? tags)
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
    /// Defines a typed attribute by string id for this item definition.
    /// </summary>
    /// <typeparam name="T">The attribute value type.</typeparam>
    /// <param name="id">The attribute id.</param>
    /// <param name="value">The attribute value.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    protected void DefineAttribute<T>(string id, T value)
    {
        _attributes.Set(id, value);
    }

    /// <summary>
    /// Defines a direct tag for this item definition.
    /// </summary>
    /// <param name="id">The tag id to add.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not a valid namespaced tag id.</exception>
    protected void DefineTag(string id)
    {
        Tags.Add(id);
    }

    /// <summary>
    /// Defines multiple direct tags for this item definition.
    /// </summary>
    /// <param name="tags">The tags to add. A <see langword="null"/> collection is ignored.</param>
    protected void DefineTags(IEnumerable<string>? tags)
    {
        if (tags == null)
            return;

        foreach (var tag in tags)
            DefineTag(tag);
    }

    /// <summary>
    /// Determines whether this definition directly declares a tag.
    /// </summary>
    /// <param name="id">The tag id to search for.</param>
    /// <returns><see langword="true"/> when the direct tag is present; otherwise, <see langword="false"/>.</returns>
    public bool HasTag(string id)
    {
        return Tags.Has(id);
    }
}
