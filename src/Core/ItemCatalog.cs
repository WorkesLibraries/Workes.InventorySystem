using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Owns item definitions, schemas, tags, and attributes for a family of inventories.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class ItemCatalog<TKey>
{
    /// <summary>
    /// Gets the registry of item definitions.
    /// </summary>
    public ItemRegistry<TKey> Registry { get; }

    /// <summary>
    /// Gets the registry of item schemas referenced by definitions.
    /// </summary>
    public ItemSchemaRegistry<TKey> Schemas { get; } = new();

    /// <summary>
    /// Gets the catalog of declared tags.
    /// </summary>
    public TagCatalog Tags { get; } = new();

    /// <summary>
    /// Gets the catalog of declared definition attributes.
    /// </summary>
    public AttributeCatalog Attributes { get; } = new();

    /// <summary>
    /// Gets whether the item registry and schema catalog are frozen.
    /// </summary>
    public bool Frozen => Registry.Frozen;

    /// <summary>
    /// Creates an item catalog.
    /// </summary>
    public ItemCatalog()
    {
        Registry = new ItemRegistry<TKey>(RegisterDefinitionSchemas, FreezeCatalogState);
    }

    /// <summary>
    /// Freezes the catalog after validating definitions, schemas, and referenced tags.
    /// </summary>
    /// <exception cref="InvalidOperationException">The catalog contains invalid definitions, schemas, or undeclared tag references.</exception>
    public void Freeze()
    {
        if (!Registry.Frozen)
            Registry.Freeze();
    }

    internal void RegisterDefinitionSchemas(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        Schemas.RegisterChain(definition.Schema);
    }

    private void ValidateDefinitions()
    {
        Schemas.Validate();
        ValidateDefinitionSchemaOwnership();
        ValidateDefinitionConstructorsDoNotExposeSchemas();
        ValidateReferencedAttributesAreDeclared();
        ValidateReferencedTagsAreDeclared();
        foreach (var definition in Registry.Definitions)
            definition.Validate();
    }

    private void ValidateDefinitionSchemaOwnership()
    {
        foreach (var definition in Registry.Definitions)
        {
            var definitionType = definition.GetType();
            var schema = definition.Schema;

            while (schema != null)
            {
                var ownerType = schema.OwnerDefinitionType;
                if (ownerType != null && !ownerType.IsAssignableFrom(definitionType))
                {
                    throw new InvalidOperationException(
                        $"Schema '{schema.Id}' is owned by definition type '{ownerType.Name}' and cannot be used by definition type '{definitionType.Name}'.");
                }

                schema = schema.Parent;
            }
        }
    }

    private void ValidateDefinitionConstructorsDoNotExposeSchemas()
    {
        var schemaType = typeof(ItemSchema<TKey>);

        foreach (var definition in Registry.Definitions)
        {
            var definitionType = definition.GetType();
            if (definitionType == typeof(ItemDefinition<TKey>))
                continue;

            var exposesSchema = definitionType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == schemaType);

            if (exposesSchema)
            {
                throw new InvalidOperationException(
                    $"Definition type '{definitionType.Name}' exposes a public ItemSchema constructor parameter. Schemas should be owned by definition classes and passed only through protected constructors.");
            }
        }
    }

    private void ValidateReferencedAttributesAreDeclared()
    {
        foreach (var schema in Schemas.Schemas)
        {
            foreach (var attribute in schema.DirectAttributes)
            {
                if (!Attributes.Contains(attribute.Key))
                    throw new InvalidOperationException($"Attribute '{attribute.Key}' is used by schema '{schema.Id}' but is not declared in the item catalog attribute catalog.");
            }
        }
    }

    private void ValidateReferencedTagsAreDeclared()
    {
        foreach (var schema in Schemas.Schemas)
        {
            foreach (var tag in schema.DirectTagKeys)
            {
                if (!Tags.Contains(tag))
                    throw new InvalidOperationException($"Tag '{tag}' is used by schema '{schema.Id}' but is not declared in the item catalog tag catalog.");
            }
        }

        foreach (var definition in Registry.Definitions)
        {
            foreach (var tag in definition.Tags.AllKeys())
            {
                if (!Tags.Contains(tag))
                    throw new InvalidOperationException($"Tag '{tag}' is used by definition '{definition.Id}' but is not declared in the item catalog tag catalog.");
            }
        }
    }

    private void FreezeCatalogState()
    {
        ValidateDefinitions();
        Schemas.Freeze();
    }

    /// <summary>
    /// Resolves all tags satisfied by a definition, including schema tags and generated parent tags.
    /// </summary>
    /// <param name="definition">The definition whose tags should be resolved.</param>
    /// <returns>The resolved tags with source information.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public IReadOnlyCollection<ResolvedTag> ResolveTags(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        var resolved = new Dictionary<TagKey, ResolvedTag>();

        foreach (var tag in definition.Schema.GetResolvedDirectSchemaTags())
            AddWithHierarchy(resolved, tag, TagSource.Schema);

        foreach (var tag in definition.Tags.AllKeys())
            AddWithHierarchy(resolved, tag, TagSource.Definition);

        return new List<ResolvedTag>(resolved.Values);
    }

    /// <summary>
    /// Determines whether a definition satisfies a tag directly, through its schema, or through a generated parent tag.
    /// </summary>
    /// <param name="definition">The definition to evaluate.</param>
    /// <param name="tagId">The tag id to search for.</param>
    /// <returns><see langword="true"/> when the definition satisfies the tag; otherwise, <see langword="false"/>.</returns>
    public bool Satisfies(ItemDefinition<TKey> definition, string tagId)
    {
        return Tags.TryGetKey(tagId, out var tag) && tag != null && Satisfies(definition, tag);
    }

    internal bool Satisfies(ItemDefinition<TKey> definition, TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        foreach (var resolved in ResolveTags(definition))
        {
            if (string.Equals(resolved.Id, tag.Id, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void AddWithHierarchy(Dictionary<TagKey, ResolvedTag> resolved, TagKey directTag, TagSource source)
    {
        AddResolved(resolved, directTag, new ResolvedTag(directTag, source, directTag));

        foreach (var parent in Tags.GetHierarchy(directTag))
            AddResolved(resolved, parent, new ResolvedTag(parent, TagSource.GeneratedParent, directTag));
    }

    private static void AddResolved(Dictionary<TagKey, ResolvedTag> resolved, TagKey key, ResolvedTag tag)
    {
        if (!resolved.ContainsKey(key))
            resolved.Add(key, tag);
    }
}
