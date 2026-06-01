using System;
using Workes.InventorySystem.Events.Dto;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Layout;
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
    /// Gets layout contexts affected by this change notification.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> AffectedLayoutContexts { get; }

    /// <summary>
    /// Gets whether consumers should refresh the whole inventory view.
    /// </summary>
    public bool RequiresFullRefresh { get; }

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
        AffectedLayoutContexts = new List<ILayoutContext<TKey>>();
        RequiresFullRefresh = false;
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
    /// <param name="affectedLayoutContexts">Optional explicit affected layout contexts.</param>
    /// <param name="requiresFullRefresh">Whether consumers should refresh the whole inventory view.</param>
    public InventoryChangedEventArgs(
        IEnumerable<ItemAdded<TKey>>? added = null,
        IEnumerable<ItemRemoved<TKey>>? removed = null,
        IEnumerable<ItemModified<TKey>>? modified = null,
        IEnumerable<ItemMoved<TKey>>? moved = null,
        IEnumerable<ItemSwapped<TKey>>? swapped = null,
        bool cleared = false,
        IEnumerable<ILayoutContext<TKey>>? affectedLayoutContexts = null,
        bool requiresFullRefresh = false)
    {
        Added = added != null ? added.ToList() : new List<ItemAdded<TKey>>();
        Removed = removed != null ? removed.ToList() : new List<ItemRemoved<TKey>>();
        Modified = modified != null ? modified.ToList() : new List<ItemModified<TKey>>();
        Moved = moved != null ? moved.ToList() : new List<ItemMoved<TKey>>();
        Swapped = swapped != null ? swapped.ToList() : new List<ItemSwapped<TKey>>();
        Cleared = cleared;
        AffectedLayoutContexts = BuildAffectedContexts(affectedLayoutContexts);
        RequiresFullRefresh = requiresFullRefresh || cleared;
    }

    private IReadOnlyList<ILayoutContext<TKey>> BuildAffectedContexts(IEnumerable<ILayoutContext<TKey>>? explicitContexts)
    {
        var contexts = new List<ILayoutContext<TKey>>();
        AddRange(contexts, explicitContexts);
        foreach (var added in Added)
            AddRange(contexts, added.LayoutContexts);
        foreach (var removed in Removed)
            AddRange(contexts, removed.LayoutContexts);
        foreach (var modified in Modified)
        {
            AddRange(contexts, modified.BeforeLayoutContexts);
            AddRange(contexts, modified.AfterLayoutContexts);
        }
        foreach (var moved in Moved)
        {
            AddRange(contexts, moved.FromLayoutContexts);
            AddRange(contexts, moved.ToLayoutContexts);
        }
        foreach (var swapped in Swapped)
            AddRange(contexts, swapped.AffectedLayoutContexts);
        return contexts;
    }

    private static void AddRange(List<ILayoutContext<TKey>> target, IEnumerable<ILayoutContext<TKey>>? contexts)
    {
        if (contexts == null)
            return;

        foreach (var context in contexts)
        {
            if (context != null && !target.Contains(context))
                target.Add(context);
        }
    }
}
