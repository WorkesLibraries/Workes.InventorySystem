using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines one named equipment position and the tags required by items placed there.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class EquipmentSlot<TKey>
{
    /// <summary>
    /// Gets the stable equipment slot identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the catalog-resolved tags an item definition must satisfy to fit this slot.
    /// </summary>
    public IReadOnlyList<TagKey> RequiredTags { get; }

    /// <summary>
    /// Creates an equipment slot definition.
    /// </summary>
    /// <param name="id">The stable equipment slot identifier.</param>
    /// <param name="requiredTags">The tags an item definition must satisfy to fit this slot.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public EquipmentSlot(string id, params TagKey[] requiredTags)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Equipment slot id cannot be null or empty.", nameof(id));

        Id = id;
        RequiredTags = requiredTags != null ? requiredTags.Where(t => t != null).ToList() : new List<TagKey>();
    }
}
