using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout context for entry-based placement or transaction added-entry insertion mapping.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class EntryLayoutContext<TKey> : ILayoutContext<TKey>
{
    /// <summary>
    /// Gets the target entry position.
    /// </summary>
    public int TargetIndex { get; }

    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to target entry positions.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets mapped transaction added-entry indices and their target entry positions.
    /// </summary>
    public IReadOnlyDictionary<int, int> AddedEntryTargetIndices { get; }

    /// <summary>
    /// Creates an entry layout context.
    /// </summary>
    /// <param name="targetIndex">The target entry position.</param>
    public EntryLayoutContext(int targetIndex)
    {
        TargetIndex = targetIndex;
        AddedEntryTargetIndices = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
    }

    private EntryLayoutContext(Dictionary<int, int> addedEntryTargetIndices)
    {
        TargetIndex = -1;
        IsMapped = true;
        AddedEntryTargetIndices = new ReadOnlyDictionary<int, int>(addedEntryTargetIndices);
    }

    /// <summary>
    /// Creates a single-entry placement context.
    /// </summary>
    /// <param name="targetIndex">The target entry position.</param>
    /// <returns>A context that addresses one entry position.</returns>
    public static EntryLayoutContext<TKey> Single(int targetIndex)
    {
        return new EntryLayoutContext<TKey>(targetIndex);
    }

    /// <summary>
    /// Starts a builder for mapping transaction added-entry indices to entry positions.
    /// </summary>
    /// <returns>An entry layout context builder.</returns>
    public static EntryLayoutContextBuilder<TKey> Map()
    {
        return new EntryLayoutContextBuilder<TKey>();
    }

    internal static EntryLayoutContext<TKey> FromMap(Dictionary<int, int> addedEntryTargetIndices)
    {
        return new EntryLayoutContext<TKey>(addedEntryTargetIndices);
    }
}

/// <summary>
/// Builds an entry layout context that maps transaction added-entry indices to insertion positions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class EntryLayoutContextBuilder<TKey>
{
    private readonly Dictionary<int, int> _addedEntryTargetIndices = new();

    /// <summary>
    /// Maps one transaction added-entry index to a target insertion position.
    /// </summary>
    /// <param name="addedEntryIndex">The index in <see cref="InventoryTransaction{TKey}.Added"/>.</param>
    /// <param name="targetIndex">The target insertion position.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="addedEntryIndex"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="addedEntryIndex"/> is already mapped.</exception>
    public EntryLayoutContextBuilder<TKey> Insert(int addedEntryIndex, int targetIndex)
    {
        if (addedEntryIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(addedEntryIndex), "Added entry index must be non-negative.");
        if (_addedEntryTargetIndices.ContainsKey(addedEntryIndex))
            throw new ArgumentException("Added entry index is already mapped.", nameof(addedEntryIndex));

        _addedEntryTargetIndices.Add(addedEntryIndex, targetIndex);
        return this;
    }

    /// <summary>
    /// Creates the mapped entry layout context.
    /// </summary>
    /// <returns>A mapped entry layout context.</returns>
    public EntryLayoutContext<TKey> Build()
    {
        return EntryLayoutContext<TKey>.FromMap(new Dictionary<int, int>(_addedEntryTargetIndices));
    }
}
