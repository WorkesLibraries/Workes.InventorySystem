using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines a named fixed-size section for <see cref="SectionedLayout{TKey}"/>.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
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

    internal IReadOnlyList<TagKey> RequiredTagKeys { get; }

    /// <summary>
    /// Creates a section definition.
    /// </summary>
    /// <param name="id">The stable section identifier.</param>
    /// <param name="slotCount">The number of slots in the section.</param>
    /// <param name="requiredTags">The tag ids an item definition must satisfy to fit this section.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is less than or equal to zero.</exception>
    public SectionDefinition(string id, int slotCount, params string[] requiredTags)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Section id cannot be null or empty.", nameof(id));
        if (slotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Section slot count must be greater than zero.");

        var keys = requiredTags != null
            ? requiredTags.Where(t => t != null).Select(TagKey.Parse).ToList()
            : new List<TagKey>();

        Id = id;
        SlotCount = slotCount;
        RequiredTagKeys = keys;
        RequiredTags = keys.Select(tag => tag.Id).ToList();
    }
}
