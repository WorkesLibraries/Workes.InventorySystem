using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Provides section-slot placement instructions for an operation or transaction.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class SectionedLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the target section id for a single placement context.
    /// </summary>
    public string SectionId { get; }

    /// <summary>
    /// Gets the target slot index within <see cref="SectionId"/> for a single placement context.
    /// </summary>
    public int SlotIndex { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to section slots.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets added-entry index to section-slot mappings.
    /// </summary>
    public IReadOnlyDictionary<int, (string sectionId, int slotIndex)> AddedEntrySlots { get; }

    /// <summary>
    /// Creates a single section-slot placement context.
    /// </summary>
    /// <param name="sectionId">The target section id.</param>
    /// <param name="slotIndex">The target slot index within the section.</param>
    /// <exception cref="ArgumentException"><paramref name="sectionId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex"/> is negative.</exception>
    public SectionedLayoutContext(string sectionId, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
            throw new ArgumentException("Section id cannot be null or empty.", nameof(sectionId));
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Section slot index cannot be negative.");

        SectionId = sectionId;
        SlotIndex = slotIndex;
        IsMapped = false;
        AddedEntrySlots = new Dictionary<int, (string sectionId, int slotIndex)>();
    }

    private SectionedLayoutContext(IReadOnlyDictionary<int, (string sectionId, int slotIndex)> addedEntrySlots)
    {
        SectionId = string.Empty;
        SlotIndex = -1;
        IsMapped = true;
        AddedEntrySlots = addedEntrySlots;
    }

    /// <summary>
    /// Creates a single section-slot placement context.
    /// </summary>
    /// <param name="sectionId">The target section id.</param>
    /// <param name="slotIndex">The target slot index within the section.</param>
    /// <returns>A single placement context.</returns>
    public static SectionedLayoutContext<TKey> Single(string sectionId, int slotIndex) => new(sectionId, slotIndex);

    /// <summary>
    /// Creates a builder for transaction added-entry section-slot mappings.
    /// </summary>
    /// <returns>A mapping builder.</returns>
    public static SectionedLayoutContextBuilder<TKey> Map() => new();

    internal static SectionedLayoutContext<TKey> FromMap(IReadOnlyDictionary<int, (string sectionId, int slotIndex)> map)
    {
        return new SectionedLayoutContext<TKey>(map);
    }
}

/// <summary>
/// Builds sectioned-layout mappings from transaction added-entry indices to section slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class SectionedLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, (string sectionId, int slotIndex)> _map = new();

    /// <summary>
    /// Maps an added-entry index to a section slot.
    /// </summary>
    /// <param name="addedEntryIndex">The transaction added-entry index.</param>
    /// <param name="sectionId">The target section id.</param>
    /// <param name="slotIndex">The target slot index within the section.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> or <paramref name="slotIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="sectionId"/> is invalid or <paramref name="addedEntryIndex"/> is already mapped.</exception>
    public SectionedLayoutContextBuilder<TKey> Add(int addedEntryIndex, string sectionId, int slotIndex)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index cannot be negative.");
        if (string.IsNullOrWhiteSpace(sectionId))
            throw new ArgumentException("Section id cannot be null or empty.", nameof(sectionId));
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Section slot index cannot be negative.");
        if (_map.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _map.Add(addedEntryIndex, (sectionId, slotIndex));
        return this;
    }

    /// <summary>
    /// Builds the mapped sectioned layout context.
    /// </summary>
    /// <returns>A mapped sectioned layout context.</returns>
    public SectionedLayoutContext<TKey> Build()
    {
        return SectionedLayoutContext<TKey>.FromMap(new Dictionary<int, (string sectionId, int slotIndex)>(_map));
    }
}
