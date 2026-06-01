using System;
using Workes.InventorySystem.Events.Dto;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events;

/// <summary>
/// Provides grouped details for one inventory change notification.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>Each collection represents one category of changes produced by a single operation or committed transaction.</remarks>
public class InventoryChangedEventArgs<TKey> : EventArgs
{
    /// <summary>
    /// Gets the item instances added by the operation.
    /// </summary>
    public IReadOnlyList<ItemAdded<TKey>> Added { get; }

    /// <summary>
    /// Gets the item instances removed by the operation.
    /// </summary>
    public IReadOnlyList<ItemRemoved<TKey>> Removed { get; }

    /// <summary>
    /// Gets item instances whose amounts changed.
    /// </summary>
    public IReadOnlyList<ItemModified<TKey>> Modified { get; }

    /// <summary>
    /// Gets item instances moved between layout contexts.
    /// </summary>
    public IReadOnlyList<ItemMoved<TKey>> Moved { get; }

    /// <summary>
    /// Gets item instances swapped between layout contexts.
    /// </summary>
    public IReadOnlyList<ItemSwapped<TKey>> Swapped { get; }

    /// <summary>
    /// Gets whether the inventory was fully cleared.
    /// </summary>
    public bool Cleared { get; }

    /// <summary>
    /// Creates an empty inventory change payload.
    /// </summary>
    public InventoryChangedEventArgs()
    {
        Added = new List<ItemAdded<TKey>>();
        Removed = new List<ItemRemoved<TKey>>();
        Modified = new List<ItemModified<TKey>>();
        Moved = new List<ItemMoved<TKey>>();
        Swapped = new List<ItemSwapped<TKey>>();
        Cleared = false;
    }

    /// <summary>
    /// Creates an inventory change payload from grouped change collections.
    /// </summary>
    /// <param name="added">The item instances added by the operation.</param>
    /// <param name="removed">The item instances removed by the operation.</param>
    /// <param name="modified">The item instances whose amounts changed.</param>
    /// <param name="moved">The item instances moved between layout contexts.</param>
    /// <param name="swapped">The item instances swapped between layout contexts.</param>
    /// <param name="cleared">Whether the inventory was fully cleared.</param>
    public InventoryChangedEventArgs(
        IEnumerable<ItemAdded<TKey>>? added = null,
        IEnumerable<ItemRemoved<TKey>>? removed = null,
        IEnumerable<ItemModified<TKey>>? modified = null,
        IEnumerable<ItemMoved<TKey>>? moved = null,
        IEnumerable<ItemSwapped<TKey>>? swapped = null,
        bool cleared = false)
    {
        Added = added != null ? added.ToList() : new List<ItemAdded<TKey>>();
        Removed = removed != null ? removed.ToList() : new List<ItemRemoved<TKey>>();
        Modified = modified != null ? modified.ToList() : new List<ItemModified<TKey>>();
        Moved = moved != null ? moved.ToList() : new List<ItemMoved<TKey>>();
        Swapped = swapped != null ? swapped.ToList() : new List<ItemSwapped<TKey>>();
        Cleared = cleared;
    }
}
