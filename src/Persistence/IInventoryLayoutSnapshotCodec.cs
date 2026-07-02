using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Converts all persistent state owned by one layout contract to and from portable snapshot data.
/// Implementations must be stateless and safe for concurrent use.
/// </summary>
/// <remarks>
/// Successful capture promises that the produced data can be decoded and exactly reconstructed against equivalent
/// runtime configuration. Capture validates this round trip before returning a snapshot.
/// </remarks>
public interface IInventoryLayoutSnapshotCodec<TKey>
{
    /// <summary>Gets the globally unique, stable persisted layout kind.</summary>
    string LayoutKind { get; }

    /// <summary>Gets the data version written by this codec.</summary>
    int CurrentVersion { get; }

    /// <summary>Captures the layout without exposing inventory storage indices.</summary>
    bool TryCapture(
        InventoryLayoutSnapshotCaptureContext<TKey> context,
        out SnapshotValue? data,
        out string? error);

    /// <summary>
    /// Decodes and structurally validates layout data into an inert candidate without mutating a live layout.
    /// </summary>
    bool TryDecode(
        InventoryLayoutSnapshotDecodeContext<TKey> context,
        out InventoryLayoutSnapshotCandidate<TKey>? candidate,
        out string? error);

    /// <summary>
    /// Creates an isolated layout containing the exact decoded placement, or rejects incompatible current
    /// configuration without mutating the target inventory.
    /// </summary>
    bool TryCreateExactLayout(
        InventoryLayoutSnapshotRestoreContext<TKey> context,
        out IInventoryLayout<TKey>? layout,
        out string? error);
}

/// <summary>Provides stable snapshot identities while a layout codec captures state.</summary>
public sealed class InventoryLayoutSnapshotCaptureContext<TKey>
{
    private readonly IReadOnlyDictionary<ItemInstance<TKey>, string> _entryIds;

    internal InventoryLayoutSnapshotCaptureContext(
        Inventory<TKey> inventory,
        IReadOnlyDictionary<ItemInstance<TKey>, string> entryIds)
    {
        Inventory = inventory;
        _entryIds = entryIds;
    }

    /// <summary>Gets the inventory whose current layout state is being captured.</summary>
    public Inventory<TKey> Inventory { get; }

    /// <summary>Gets the layout being captured.</summary>
    public IInventoryLayout<TKey> Layout => Inventory.Layout;

    /// <summary>Resolves an item instance to its stable snapshot-local identity.</summary>
    public bool TryGetEntryId(ItemInstance<TKey> instance, out string? entryId)
    {
        if (instance != null && _entryIds.TryGetValue(instance, out var found))
        {
            entryId = found;
            return true;
        }
        entryId = null;
        return false;
    }
}

/// <summary>Provides a detached layout envelope and the entries it may reference during decoding.</summary>
public sealed class InventoryLayoutSnapshotDecodeContext<TKey>
{
    private readonly IReadOnlyDictionary<string, InventorySnapshotEntry> _entries;

    /// <summary>Creates a decode context for a detached layout snapshot and its entries.</summary>
    public InventoryLayoutSnapshotDecodeContext(
        InventoryLayoutSnapshot snapshot,
        IReadOnlyDictionary<string, InventorySnapshotEntry> entries)
    {
        Snapshot = snapshot;
        _entries = entries;
    }

    /// <summary>Gets the detached layout snapshot being decoded.</summary>
    public InventoryLayoutSnapshot Snapshot { get; }

    /// <summary>Gets the number of entries available to the layout snapshot.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>Resolves a snapshot-local entry identity.</summary>
    public bool TryGetEntry(string entryId, out InventorySnapshotEntry? entry)
    {
        if (entryId != null && _entries.TryGetValue(entryId, out var found))
        {
            entry = found;
            return true;
        }
        entry = null;
        return false;
    }
}

/// <summary>Provides decoded placement identities and resolved item instances for exact layout reconstruction.</summary>
public sealed class InventoryLayoutSnapshotRestoreContext<TKey>
{
    internal InventoryLayoutSnapshotRestoreContext(
        IInventoryLayout<TKey> targetLayout,
        InventoryLayoutSnapshotCandidate<TKey> candidate,
        IReadOnlyDictionary<string, int> storageIndices,
        IReadOnlyDictionary<string, ItemInstance<TKey>> instances)
    {
        TargetLayout = targetLayout;
        Candidate = candidate;
        StorageIndices = storageIndices;
        Instances = instances;
    }

    /// <summary>Gets the current target layout whose runtime configuration must remain authoritative.</summary>
    public IInventoryLayout<TKey> TargetLayout { get; }

    /// <summary>Gets the structurally validated snapshot candidate.</summary>
    public InventoryLayoutSnapshotCandidate<TKey> Candidate { get; }

    /// <summary>Gets snapshot entry identities mapped to their reconstructed storage indices.</summary>
    public IReadOnlyDictionary<string, int> StorageIndices { get; }

    /// <summary>Gets resolved, detached item instances by snapshot entry identity.</summary>
    public IReadOnlyDictionary<string, ItemInstance<TKey>> Instances { get; }
}

/// <summary>
/// Inert, structurally validated layout state. It is deliberately separate from live layout mutation.
/// </summary>
public sealed class InventoryLayoutSnapshotCandidate<TKey>
{
    /// <summary>Creates an inert decoded layout candidate.</summary>
    public InventoryLayoutSnapshotCandidate(
        string layoutKind,
        int dataVersion,
        SnapshotValue data,
        IReadOnlyDictionary<string, IReadOnlyList<ILayoutContext<TKey>>> entryContexts)
    {
        LayoutKind = layoutKind;
        DataVersion = dataVersion;
        Data = data;
        EntryContexts = entryContexts;
    }

    /// <summary>Gets the stable layout kind.</summary>
    public string LayoutKind { get; }

    /// <summary>Gets the decoded layout data version.</summary>
    public int DataVersion { get; }

    /// <summary>Gets a detached copy of the validated codec data.</summary>
    public SnapshotValue Data { get; }

    /// <summary>Gets decoded layout contexts grouped by snapshot-local entry identity.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ILayoutContext<TKey>>> EntryContexts { get; }
}
