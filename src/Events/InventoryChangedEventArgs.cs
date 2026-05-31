using System;
using Workes.InventorySystem.Events.Dto;
using System.Collections.Generic;
namespace Workes.InventorySystem.Events;

public class InventoryChangedEventArgs<TKey> : EventArgs
{
    public List<ItemAdded<TKey>> Added { get; set; }
    public List<ItemRemoved<TKey>> Removed { get; set; }
    public List<ItemModified<TKey>> Modified { get; set; }
    public List<ItemMoved<TKey>> Moved { get; set; }
    public List<ItemSwapped<TKey>> Swapped { get; set; }

    public bool Cleared { get; set; }

    public InventoryChangedEventArgs()
    {
        Added = new List<ItemAdded<TKey>>();
        Removed = new List<ItemRemoved<TKey>>();
        Modified = new List<ItemModified<TKey>>();
        Moved = new List<ItemMoved<TKey>>();
        Swapped = new List<ItemSwapped<TKey>>();
        Cleared = false;
    }

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
