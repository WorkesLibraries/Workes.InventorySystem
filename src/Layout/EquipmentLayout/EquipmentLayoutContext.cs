using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Provides equipment-slot placement instructions for an operation or transaction.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class EquipmentLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the target equipment slot id for a single placement context.
    /// </summary>
    public string SlotId { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to equipment slots.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets added-entry index to equipment slot mappings.
    /// </summary>
    public IReadOnlyDictionary<int, string> AddedEntrySlots { get; }

    /// <summary>
    /// Creates a single equipment-slot placement context.
    /// </summary>
    /// <param name="slotId">The target equipment slot id.</param>
    public EquipmentLayoutContext(string slotId)
    {
        SlotId = slotId;
        IsMapped = false;
        AddedEntrySlots = new Dictionary<int, string>();
    }

    private EquipmentLayoutContext(IReadOnlyDictionary<int, string> addedEntrySlots)
    {
        SlotId = string.Empty;
        IsMapped = true;
        AddedEntrySlots = addedEntrySlots;
    }

    /// <summary>
    /// Creates a single equipment-slot placement context.
    /// </summary>
    /// <param name="slotId">The target equipment slot id.</param>
    /// <returns>A single placement context.</returns>
    public static EquipmentLayoutContext<TKey> Single(string slotId) => new(slotId);

    /// <summary>
    /// Creates a builder for transaction added-entry slot mappings.
    /// </summary>
    /// <returns>A mapping builder.</returns>
    public static EquipmentLayoutContextBuilder<TKey> Map() => new();

    internal static EquipmentLayoutContext<TKey> FromMap(IReadOnlyDictionary<int, string> map)
    {
        return new EquipmentLayoutContext<TKey>(map);
    }
}

/// <summary>
/// Builds equipment-layout mappings from transaction added-entry indices to equipment slot ids.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class EquipmentLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, string> _map = new();

    /// <summary>
    /// Maps an added-entry index to an equipment slot id.
    /// </summary>
    /// <param name="addedEntryIndex">The transaction added-entry index.</param>
    /// <param name="slotId">The target equipment slot id.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="slotId"/> is null, empty, or whitespace, or the added-entry index is already mapped.</exception>
    public EquipmentLayoutContextBuilder<TKey> Add(int addedEntryIndex, string slotId)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index cannot be negative.");
        if (string.IsNullOrWhiteSpace(slotId))
            throw new ArgumentException("Equipment slot id cannot be null or empty.", nameof(slotId));
        if (_map.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _map.Add(addedEntryIndex, slotId);
        return this;
    }

    /// <summary>
    /// Builds the mapped equipment layout context.
    /// </summary>
    /// <returns>A mapped equipment layout context.</returns>
    public EquipmentLayoutContext<TKey> Build()
    {
        return EquipmentLayoutContext<TKey>.FromMap(new Dictionary<int, string>(_map));
    }
}
