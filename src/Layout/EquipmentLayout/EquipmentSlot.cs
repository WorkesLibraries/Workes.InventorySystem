using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Defines one named equipment position and the tags or item definitions accepted there.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Tag restrictions use catalog-resolved tags. Definition restrictions compare item definition ids. If both are
/// configured, matching either restriction allows placement. A slot with no tag or definition restrictions accepts
/// any item that otherwise satisfies inventory rules.
/// </remarks>
public sealed class EquipmentSlot<TKey>
{
    /// <summary>
    /// Gets the stable equipment slot identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the catalog-resolved tags an item definition must satisfy to fit this slot.
    /// </summary>
    public IReadOnlyList<string> RequiredTags { get; }

    /// <summary>
    /// Gets item definition ids explicitly allowed in this slot.
    /// </summary>
    public IReadOnlyList<TKey> AllowedDefinitionIds { get; }

    internal IReadOnlyList<string> RequiredTagIds { get; }
    internal IReadOnlyCollection<TKey> AllowedDefinitionIdSet { get; }

    /// <summary>
    /// Creates an equipment slot definition.
    /// </summary>
    /// <param name="id">The stable equipment slot identifier.</param>
    /// <param name="requiredTags">The tag ids an item definition must satisfy to fit this slot.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    public EquipmentSlot(string id, params string[] requiredTags)
        : this(id, new EquipmentSlotOptions<TKey> { RequiredTags = requiredTags })
    {
    }

    /// <summary>
    /// Creates an equipment slot definition with tag and definition compatibility options.
    /// </summary>
    /// <param name="id">The stable equipment slot identifier.</param>
    /// <param name="options">The tag and definition compatibility options.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace, or an allowed definition id is null.</exception>
    public EquipmentSlot(string id, EquipmentSlotOptions<TKey> options)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Equipment slot id cannot be null or empty.", nameof(id));
        options ??= new EquipmentSlotOptions<TKey>();

        var tags = options.RequiredTags != null
            ? options.RequiredTags.Where(t => t != null).ToList()
            : new List<string>();
        var allowedIds = BuildAllowedDefinitionIds(options);

        Id = id;
        RequiredTagIds = tags.AsReadOnly();
        RequiredTags = tags.AsReadOnly();
        AllowedDefinitionIds = allowedIds.AsReadOnly();
        AllowedDefinitionIdSet = allowedIds.AsReadOnly();
    }

    private static List<TKey> BuildAllowedDefinitionIds(EquipmentSlotOptions<TKey> options)
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
