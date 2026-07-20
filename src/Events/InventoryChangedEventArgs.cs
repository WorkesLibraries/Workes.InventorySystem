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
/// <remarks>
/// Each collection represents one category of changes produced by a single operation or committed transaction.
/// UI listeners can usually refresh from <see cref="AffectedLayoutContexts"/> and <see cref="RequiresFullRefresh"/>;
/// the semantic groups provide richer information for animations, gameplay, and auditing.
/// </remarks>
public class InventoryChangedEventArgs<TKey> : EventArgs
{
    /// <summary>Gets the high-level workflow that produced this change.</summary>
    public InventoryChangeOrigin Origin { get; }

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
    /// Gets item instances whose metadata changed.
    /// </summary>
    public IReadOnlyList<ItemMetadataChanged<TKey>> MetadataChanged { get; }

    /// <summary>Gets the inventory-owned metadata change, when one was committed.</summary>
    public InventoryMetadataChanged? InventoryMetadataChanged { get; }

    /// <summary>
    /// Gets whether the inventory was fully cleared.
    /// </summary>
    public bool Cleared { get; }

    /// <summary>
    /// Gets runtime inventory configuration changes produced by the operation.
    /// </summary>
    public IReadOnlyList<InventoryConfigurationChanged<TKey>> ConfigurationChanged { get; }

    /// <summary>
    /// Gets layout contexts affected by this change notification.
    /// </summary>
    /// <remarks>
    /// Contexts are gathered from semantic payloads and layout reconciliation. Equivalent positions can appear more
    /// than once when their context type uses reference equality.
    /// </remarks>
    public IReadOnlyList<ILayoutContext<TKey>> AffectedLayoutContexts { get; }

    /// <summary>
    /// Gets whether this event does not completely describe the observable change through its semantic payloads and
    /// <see cref="AffectedLayoutContexts"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, consumers can synchronize item presentation at existing addressable contexts from
    /// this event. When <see langword="true"/>, consumers should rebuild the complete inventory view because topology,
    /// layout-owned presentation state, or another intentionally unrepresented change may also have changed.
    /// </remarks>
    public bool RequiresFullRefresh { get; }

    /// <summary>
    /// Creates an empty inventory change payload.
    /// </summary>
    public InventoryChangedEventArgs()
    {
        Origin = InventoryChangeOrigin.Operation;
        Added = new List<ItemAdded<TKey>>();
        Removed = new List<ItemRemoved<TKey>>();
        Modified = new List<ItemModified<TKey>>();
        Moved = new List<ItemMoved<TKey>>();
        Swapped = new List<ItemSwapped<TKey>>();
        MetadataChanged = new List<ItemMetadataChanged<TKey>>();
        InventoryMetadataChanged = null;
        Cleared = false;
        ConfigurationChanged = new List<InventoryConfigurationChanged<TKey>>();
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
    /// <param name="metadataChanged">The item instances whose metadata changed.</param>
    /// <param name="cleared">Whether the inventory was fully cleared.</param>
    /// <param name="configurationChanged">Runtime inventory configuration changes.</param>
    /// <param name="affectedLayoutContexts">Optional explicit affected layout contexts.</param>
    /// <param name="requiresFullRefresh">
    /// Whether the supplied payloads and affected contexts do not completely describe the observable change.
    /// </param>
    /// <param name="origin">The high-level workflow that produced the change.</param>
    /// <param name="inventoryMetadataChanged">The inventory-owned metadata change.</param>
    public InventoryChangedEventArgs(
        IEnumerable<ItemAdded<TKey>>? added = null,
        IEnumerable<ItemRemoved<TKey>>? removed = null,
        IEnumerable<ItemModified<TKey>>? modified = null,
        IEnumerable<ItemMoved<TKey>>? moved = null,
        IEnumerable<ItemSwapped<TKey>>? swapped = null,
        IEnumerable<ItemMetadataChanged<TKey>>? metadataChanged = null,
        bool cleared = false,
        IEnumerable<InventoryConfigurationChanged<TKey>>? configurationChanged = null,
        IEnumerable<ILayoutContext<TKey>>? affectedLayoutContexts = null,
        bool requiresFullRefresh = false,
        InventoryChangeOrigin origin = InventoryChangeOrigin.Operation,
        InventoryMetadataChanged? inventoryMetadataChanged = null)
    {
        Origin = origin;
        Added = added != null ? added.ToList() : new List<ItemAdded<TKey>>();
        Removed = removed != null ? removed.ToList() : new List<ItemRemoved<TKey>>();
        Modified = modified != null ? modified.ToList() : new List<ItemModified<TKey>>();
        Moved = moved != null ? moved.ToList() : new List<ItemMoved<TKey>>();
        Swapped = swapped != null ? swapped.ToList() : new List<ItemSwapped<TKey>>();
        MetadataChanged = metadataChanged != null ? metadataChanged.ToList() : new List<ItemMetadataChanged<TKey>>();
        InventoryMetadataChanged = inventoryMetadataChanged;
        Cleared = cleared;
        ConfigurationChanged = configurationChanged != null ? configurationChanged.ToList() : new List<InventoryConfigurationChanged<TKey>>();
        AffectedLayoutContexts = BuildAffectedContexts(affectedLayoutContexts);
        RequiresFullRefresh = requiresFullRefresh || cleared || ConfigurationChanged.Any(change => change.RequiresFullRefresh);
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
        foreach (var metadataChanged in MetadataChanged)
            AddRange(contexts, metadataChanged.LayoutContexts);
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
