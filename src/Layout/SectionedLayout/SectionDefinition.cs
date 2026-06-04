using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines a named fixed-size section for <see cref="SectionedLayout{TKey}"/>.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Tag restrictions use catalog-resolved tags. Definition restrictions compare item definition ids. If both are
/// configured, matching either restriction allows placement. A section with no tag or definition restrictions accepts
/// any item that otherwise satisfies inventory rules.
/// </remarks>
public sealed class SectionDefinition<TKey>
{
    /// <summary>
    /// Gets the stable section identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the number of slots in this section.
    /// </summary>
    public int SlotCount { get; }

    /// <summary>
    /// Gets the catalog-resolved tags required by items placed in this section.
    /// </summary>
    public IReadOnlyList<string> RequiredTags { get; }

    /// <summary>
    /// Gets item definition ids explicitly allowed in this section.
    /// </summary>
    public IReadOnlyList<TKey> AllowedDefinitionIds { get; }

    internal IReadOnlyList<TagKey> RequiredTagKeys { get; }
    internal IReadOnlyCollection<TKey> AllowedDefinitionIdSet { get; }

    /// <summary>
    /// Creates a section definition.
    /// </summary>
    /// <param name="id">The stable section identifier.</param>
    /// <param name="slotCount">The number of slots in the section.</param>
    /// <param name="requiredTags">The tag ids an item definition must satisfy to fit this section.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is less than or equal to zero.</exception>
    public SectionDefinition(string id, int slotCount, params string[] requiredTags)
        : this(id, slotCount, new SectionDefinitionOptions<TKey> { RequiredTags = requiredTags })
    {
    }

    /// <summary>
    /// Creates a section definition with tag and definition compatibility options.
    /// </summary>
    /// <param name="id">The stable section identifier.</param>
    /// <param name="slotCount">The number of slots in the section.</param>
    /// <param name="options">The tag and definition compatibility options.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace, or an allowed definition id is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is less than or equal to zero.</exception>
    public SectionDefinition(string id, int slotCount, SectionDefinitionOptions<TKey> options)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Section id cannot be null or empty.", nameof(id));
        if (slotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Section slot count must be greater than zero.");
        options ??= new SectionDefinitionOptions<TKey>();

        var keys = options.RequiredTags != null
            ? options.RequiredTags.Where(t => t != null).Select(TagKey.Parse).ToList()
            : new List<TagKey>();
        var allowedIds = BuildAllowedDefinitionIds(options);

        Id = id;
        SlotCount = slotCount;
        RequiredTagKeys = keys.AsReadOnly();
        RequiredTags = keys.Select(tag => tag.Id).ToList().AsReadOnly();
        AllowedDefinitionIds = allowedIds.AsReadOnly();
        AllowedDefinitionIdSet = allowedIds.AsReadOnly();
    }

    private static List<TKey> BuildAllowedDefinitionIds(SectionDefinitionOptions<TKey> options)
    {
        var ids = new List<TKey>();
        var seen = new HashSet<TKey>();

        AddIds(options.AllowedDefinitionIds, ids, seen);

        if (options.AllowedDefinitions != null)
        {
            foreach (var definition in options.AllowedDefinitions)
            {
                if (definition == null)
                    continue;

                AddId(definition.Id, ids, seen);
            }
        }

        return ids;
    }

    private static void AddIds(IEnumerable<TKey>? source, List<TKey> ids, HashSet<TKey> seen)
    {
        if (source == null)
            return;

        foreach (var id in source)
            AddId(id, ids, seen);
    }

    private static void AddId(TKey id, List<TKey> ids, HashSet<TKey> seen)
    {
        if (id == null)
            throw new ArgumentException("Allowed definition id cannot be null.");

        if (seen.Add(id))
            ids.Add(id);
    }
}
