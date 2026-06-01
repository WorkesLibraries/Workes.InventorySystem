using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for fixed-slot placement or transaction added-entry mapping.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class SlotLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the slot index addressed by this context.
    /// </summary>
    public int SlotIndex { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to slot indices.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets mapped transaction added-entry indices and their target slot indices.
    /// </summary>
    public IReadOnlyDictionary<int, int> AddedEntrySlots { get; }

    /// <summary>
    /// Creates a slot layout context.
    /// </summary>
    /// <param name="slotIndex">The target slot index.</param>
    public SlotLayoutContext(int slotIndex)
    {
        SlotIndex = slotIndex;
        AddedEntrySlots = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
    }

    private SlotLayoutContext(Dictionary<int, int> addedEntrySlots)
    {
        SlotIndex = -1;
        IsMapped = true;
        AddedEntrySlots = new ReadOnlyDictionary<int, int>(addedEntrySlots);
    }

    /// <summary>
    /// Creates a single-slot placement context.
    /// </summary>
    /// <param name="slotIndex">The target slot index.</param>
    /// <returns>A context that addresses one slot.</returns>
    public static SlotLayoutContext<TKey> Single(int slotIndex)
    {
        return new SlotLayoutContext<TKey>(slotIndex);
    }

    /// <summary>
    /// Starts a builder for mapping transaction added-entry indices to slot indices.
    /// </summary>
    /// <returns>A slot layout context builder.</returns>
    public static SlotLayoutContextBuilder<TKey> Map()
    {
        return new SlotLayoutContextBuilder<TKey>();
    }

    internal static SlotLayoutContext<TKey> FromMap(Dictionary<int, int> addedEntrySlots)
    {
        return new SlotLayoutContext<TKey>(addedEntrySlots);
    }
}

/// <summary>
/// Builds a slot layout context that maps transaction added-entry indices to slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class SlotLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, int> _addedEntrySlots = new();

    /// <summary>
    /// Maps one transaction added-entry index to a target slot.
    /// </summary>
    /// <param name="addedEntryIndex">The index in <see cref="InventoryTransaction{TKey}.Added"/>.</param>
    /// <param name="slotIndex">The target slot index.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="addedEntryIndex"/> is already mapped.</exception>
    public SlotLayoutContextBuilder<TKey> Add(int addedEntryIndex, int slotIndex)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index must be non-negative.");
        if (_addedEntrySlots.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _addedEntrySlots.Add(addedEntryIndex, slotIndex);
        return this;
    }

    /// <summary>
    /// Creates the mapped slot layout context.
    /// </summary>
    /// <returns>A mapped slot layout context.</returns>
    public SlotLayoutContext<TKey> Build()
    {
        return SlotLayoutContext<TKey>.FromMap(new Dictionary<int, int>(_addedEntrySlots));
    }
}
