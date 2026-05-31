using System;
using System.Collections.Generic;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

public sealed class ItemCatalog<TKey>
{
    public ItemRegistry<TKey> Registry { get; }
    public ItemSchemaRegistry<TKey> Schemas { get; } = new();
    public TagCatalog Tags { get; } = new();

    public bool Frozen => Registry.Frozen;

    public ItemCatalog()
    {
        Registry = new ItemRegistry<TKey>(RegisterDefinitionSchemas, FreezeCatalogState);
    }

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
        ValidateReferencedTagsAreDeclared();
        foreach (var definition in Registry.Definitions)
            definition.Validate();
    }

    private void ValidateReferencedTagsAreDeclared()
    {
        foreach (var schema in Schemas.Schemas)
        {
            foreach (var tag in schema.DirectTags)
            {
                if (!Tags.Contains(tag))
                    throw new InvalidOperationException($"Tag '{tag}' is used by schema '{schema.Id}' but is not declared in the item catalog tag catalog.");
            }
        }

        foreach (var definition in Registry.Definitions)
        {
            foreach (var tag in definition.Tags.All())
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

    public IReadOnlyCollection<ResolvedTag> ResolveTags(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        var resolved = new Dictionary<TagKey, ResolvedTag>();

        foreach (var tag in definition.Schema.GetResolvedDirectSchemaTags())
            AddWithHierarchy(resolved, tag, TagSource.Schema);

        foreach (var tag in definition.Tags.All())
            AddWithHierarchy(resolved, tag, TagSource.Definition);

        return new List<ResolvedTag>(resolved.Values);
    }

    public bool Satisfies(ItemDefinition<TKey> definition, TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        foreach (var resolved in ResolveTags(definition))
        {
            if (resolved.Tag.Equals(tag))
                return true;
        }

        return false;
    }

    private void AddWithHierarchy(Dictionary<TagKey, ResolvedTag> resolved, TagKey directTag, TagSource source)
    {
        AddResolved(resolved, new ResolvedTag(directTag, source, directTag));

        foreach (var parent in Tags.GetHierarchy(directTag))
            AddResolved(resolved, new ResolvedTag(parent, TagSource.GeneratedParent, directTag));
    }

    private static void AddResolved(Dictionary<TagKey, ResolvedTag> resolved, ResolvedTag tag)
    {
        if (!resolved.ContainsKey(tag.Tag))
            resolved.Add(tag.Tag, tag);
    }
}
