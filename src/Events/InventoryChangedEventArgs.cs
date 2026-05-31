using System;
using Workes.InventorySystem.Events.Dto;
using System.Collections.Generic;
namespace Workes.InventorySystem.Events;

/// <summary>
/// Provides grouped details for one inventory change notification.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>Each collection represents one category of changes produced by a single operation or committed transaction.</remarks>
public class InventoryChangedEventArgs<TKey> : EventArgs
{
    /// <summary>
    /// Gets or sets the item instances added by the operation.
    /// </summary>
    public List<ItemAdded<TKey>> Added { get; set; }

    /// <summary>
    /// Gets or sets the item instances removed by the operation.
    /// </summary>
    public List<ItemRemoved<TKey>> Removed { get; set; }

    /// <summary>
    /// Gets or sets item instances whose amounts changed.
    /// </summary>
    public List<ItemModified<TKey>> Modified { get; set; }

    /// <summary>
    /// Gets or sets item instances moved between layout contexts.
    /// </summary>
    public List<ItemMoved<TKey>> Moved { get; set; }

    /// <summary>
    /// Gets or sets item instances swapped between layout contexts.
    /// </summary>
    public List<ItemSwapped<TKey>> Swapped { get; set; }

    /// <summary>
    /// Gets or sets whether the inventory was fully cleared.
    /// </summary>
    public bool Cleared { get; set; }

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
        List<ItemAdded<TKey>>? added = null,
        List<ItemRemoved<TKey>>? removed = null,
        List<ItemModified<TKey>>? modified = null,
        List<ItemMoved<TKey>>? moved = null,
        List<ItemSwapped<TKey>>? swapped = null,
        bool cleared = false)
    {
        Added = added ?? new List<ItemAdded<TKey>>();
        Removed = removed ?? new List<ItemRemoved<TKey>>();
        Modified = modified ?? new List<ItemModified<TKey>>();
        Moved = moved ?? new List<ItemMoved<TKey>>();
        Swapped = swapped ?? new List<ItemSwapped<TKey>>();
        Cleared = cleared;
    }
}
