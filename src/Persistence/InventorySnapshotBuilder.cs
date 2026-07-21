using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Detached editor for portable inventory snapshots used by save migrations and application-controlled recovery.
/// </summary>
/// <remarks>
/// The builder validates serializer-facing snapshot structure only. Catalog resolution, rules, capacity, stacking, and
/// target-layout compatibility remain the responsibility of <c>AssessSnapshot</c>, exact restoration, reconciliation,
/// and salvage.
/// </remarks>
public sealed class InventorySnapshotBuilder
{
    private static readonly HashSet<string> KnownLayoutKinds = new(StringComparer.Ordinal)
    {
        "workes.inventory.layout.entry",
        "workes.inventory.layout.slot",
        "workes.inventory.layout.grid",
        "workes.inventory.layout.multi-cell-grid",
        "workes.inventory.layout.equipment",
        "workes.inventory.layout.sectioned"
    };

    private readonly InventorySnapshot _snapshot;

    /// <summary>Creates a detached builder from an existing snapshot.</summary>
    public InventorySnapshotBuilder(InventorySnapshot snapshot)
    {
        if (!TryCloneSnapshot(snapshot, out _snapshot!, out var failure) || _snapshot == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.SnapshotMalformed("Inventory snapshot is malformed."));
    }

    /// <summary>Gets the current package snapshot format version carried by the builder.</summary>
    public int FormatVersion
    {
        get => _snapshot.FormatVersion;
        set => _snapshot.FormatVersion = value;
    }

    /// <summary>Gets detached copies of entries in current builder storage order.</summary>
    public IReadOnlyList<InventorySnapshotEntry> Entries =>
        _snapshot.Entries.Select(CloneEntryOrThrow).ToList();

    /// <summary>Gets detached copies of root inventory metadata values.</summary>
    public IReadOnlyList<SnapshotNamedValue> Metadata =>
        _snapshot.Metadata.Select(CloneNamedValueOrThrow).ToList();

    /// <summary>Gets a detached copy of the current layout snapshot.</summary>
    public InventoryLayoutSnapshot Layout => CloneLayoutOrThrow(_snapshot.Layout);

    /// <summary>Returns whether the builder currently contains an entry with the supplied snapshot-local id.</summary>
    public bool ContainsEntry(string entryId) => FindEntry(entryId) != null;

    /// <summary>Sets or adds an encoded inventory metadata value.</summary>
    public InventorySnapshotBuilder SetMetadata(string name, SnapshotEncodedValue value)
    {
        SetNamedValue(_snapshot.Metadata, name, value);
        return this;
    }

    /// <summary>Removes an inventory metadata value if present.</summary>
    public InventorySnapshotBuilder RemoveMetadata(string name)
    {
        RemoveNamedValue(_snapshot.Metadata, name);
        return this;
    }

    /// <summary>Clears all inventory metadata values.</summary>
    public InventorySnapshotBuilder ClearMetadata()
    {
        _snapshot.Metadata.Clear();
        return this;
    }

    /// <summary>Replaces all inventory metadata values with detached copies sorted by key.</summary>
    public InventorySnapshotBuilder ReplaceMetadata(IEnumerable<SnapshotNamedValue> metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        var values = metadata.Select(CloneNamedValueOrThrow)
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .ToList();
        _snapshot.Metadata = values;
        return this;
    }

    /// <summary>Changes an entry's encoded definition id.</summary>
    public InventorySnapshotBuilder SetEntryDefinitionId(string entryId, SnapshotEncodedValue definitionId)
    {
        FindRequiredEntry(entryId).DefinitionId = CloneEncodedOrThrow(definitionId);
        return this;
    }

    /// <summary>Changes an entry amount. The amount must be positive.</summary>
    public InventorySnapshotBuilder SetEntryAmount(string entryId, int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Snapshot entry amount must be positive.");
        FindRequiredEntry(entryId).Amount = amount;
        return this;
    }

    /// <summary>Sets or adds an encoded metadata value on one entry.</summary>
    public InventorySnapshotBuilder SetEntryMetadata(string entryId, string name, SnapshotEncodedValue value)
    {
        SetNamedValue(FindRequiredEntry(entryId).Metadata, name, value);
        return this;
    }

    /// <summary>Removes one metadata value from one entry if present.</summary>
    public InventorySnapshotBuilder RemoveEntryMetadata(string entryId, string name)
    {
        RemoveNamedValue(FindRequiredEntry(entryId).Metadata, name);
        return this;
    }

    /// <summary>Clears all metadata values from one entry.</summary>
    public InventorySnapshotBuilder ClearEntryMetadata(string entryId)
    {
        FindRequiredEntry(entryId).Metadata.Clear();
        return this;
    }

    /// <summary>
    /// Removes an entry and updates package-owned layout references. Custom layouts must be reset or replaced first.
    /// </summary>
    public InventorySnapshotBuilder RemoveEntry(string entryId)
    {
        if (!TryRemoveEntry(entryId, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Snapshot("Snapshot entry could not be removed."));
        return this;
    }

    /// <summary>
    /// Attempts to remove an entry and update package-owned layout references.
    /// </summary>
    public bool TryRemoveEntry(string entryId, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            failure = InventoryFailures.SnapshotMalformed("Snapshot entry id cannot be null or empty.");
            return false;
        }

        var entry = FindEntry(entryId);
        if (entry == null)
        {
            failure = InventoryFailures.SnapshotMalformed($"Snapshot entry '{entryId}' was not found.");
            return false;
        }

        if (!TryRemoveLayoutReferences(entryId, out failure))
            return false;

        _snapshot.Entries.Remove(entry);
        failure = null;
        return true;
    }

    /// <summary>Reorders entries by snapshot-local ids without changing their identities.</summary>
    public InventorySnapshotBuilder ReorderEntries(IEnumerable<string> entryIds)
    {
        if (entryIds == null)
            throw new ArgumentNullException(nameof(entryIds));
        var requested = entryIds.ToList();
        var byId = _snapshot.Entries.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
        if (requested.Count != byId.Count ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Count ||
            requested.Any(id => !byId.ContainsKey(id)))
        {
            throw new ArgumentException("Entry order must contain each current snapshot entry id exactly once.", nameof(entryIds));
        }

        _snapshot.Entries = requested.Select(id => byId[id]).ToList();
        return this;
    }

    /// <summary>Replaces the layout snapshot with a detached copy.</summary>
    public InventorySnapshotBuilder ReplaceLayout(InventoryLayoutSnapshot layout)
    {
        _snapshot.Layout = CloneLayoutOrThrow(layout);
        return this;
    }

    /// <summary>
    /// Discards layout-specific saved placement by replacing it with an entry-layout snapshot over current entry order.
    /// </summary>
    public InventorySnapshotBuilder ResetLayoutToEntryOrder()
    {
        _snapshot.Layout = new InventoryLayoutSnapshot
        {
            Kind = "workes.inventory.layout.entry",
            DataVersion = BuiltInLayoutSnapshot.Version,
            Data = SnapshotValue.Object(new[]
            {
                BuiltInLayoutSnapshot.Property(
                    "order",
                    InventorySnapshotCodecs.Encode(_snapshot.Entries.Select(entry => (object?)entry.EntryId).ToList()))
            })
        };
        return this;
    }

    /// <summary>Builds a detached, validated snapshot or throws with a structured failure.</summary>
    public InventorySnapshot Build()
    {
        if (!TryBuild(out var snapshot, out var failure) || snapshot == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.SnapshotMalformed("Snapshot builder produced an invalid snapshot."));
        return snapshot;
    }

    /// <summary>Attempts to build a detached snapshot and validate serializer-facing snapshot invariants.</summary>
    public bool TryBuild(out InventorySnapshot? snapshot, out InventoryFailure? failure)
    {
        snapshot = null;
        if (!TryCloneSnapshot(_snapshot, out var clone, out failure) || clone == null)
            return false;
        if (!InventorySnapshotValidator.TryValidate(clone, out failure))
            return false;
        snapshot = clone;
        return true;
    }

    private InventorySnapshotEntry FindRequiredEntry(string entryId) =>
        FindEntry(entryId) ??
        throw new InventoryOperationException(
            InventoryFailures.SnapshotMalformed($"Snapshot entry '{entryId}' was not found."));

    private InventorySnapshotEntry? FindEntry(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return null;
        return _snapshot.Entries.FirstOrDefault(entry => string.Equals(entry.EntryId, entryId, StringComparison.Ordinal));
    }

    private static void SetNamedValue(List<SnapshotNamedValue> values, string name, SnapshotEncodedValue value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Snapshot metadata names cannot be null or empty.", nameof(name));
        var clone = CloneEncodedOrThrow(value);
        var existing = values.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        if (existing == null)
            values.Add(new SnapshotNamedValue { Name = name, Value = clone });
        else
            existing.Value = clone;
        values.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
    }

    private static void RemoveNamedValue(List<SnapshotNamedValue> values, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Snapshot metadata names cannot be null or empty.", nameof(name));
        values.RemoveAll(item => string.Equals(item.Name, name, StringComparison.Ordinal));
    }

    private bool TryRemoveLayoutReferences(string removedEntryId, out InventoryFailure? failure)
    {
        if (_snapshot.Layout == null || string.IsNullOrWhiteSpace(_snapshot.Layout.Kind))
        {
            failure = InventoryFailures.Layout("Snapshot layout must be reset or replaced before removing entries.");
            return false;
        }
        if (!KnownLayoutKinds.Contains(_snapshot.Layout.Kind))
        {
            failure =
                InventoryFailures.Layout(
                    $"Snapshot layout kind '{_snapshot.Layout.Kind}' is custom. Reset or replace the layout before removing entries.");
            return false;
        }

        return _snapshot.Layout.Kind switch
        {
            "workes.inventory.layout.entry" => TryFilterReferenceList("order", removedEntryId, removePositions: true, out failure),
            "workes.inventory.layout.slot" => TryFilterReferenceList("slots", removedEntryId, removePositions: false, out failure),
            "workes.inventory.layout.grid" => TryFilterReferenceList("cells", removedEntryId, removePositions: false, out failure),
            "workes.inventory.layout.multi-cell-grid" => TryFilterReferenceList("cells", removedEntryId, removePositions: false, out failure),
            "workes.inventory.layout.equipment" => TryFilterReferenceList("slots", removedEntryId, removePositions: false, out failure),
            "workes.inventory.layout.sectioned" => TryFilterReferenceList("slots", removedEntryId, removePositions: false, out failure),
            _ => throw new InvalidOperationException("Known layout set and switch are out of sync.")
        };
    }

    private bool TryFilterReferenceList(
        string propertyName,
        string removedEntryId,
        bool removePositions,
        out InventoryFailure? failure)
    {
        failure = null;
        if (_snapshot.Layout.Data.Kind != SnapshotValueKind.Object ||
            _snapshot.Layout.Data.Properties == null)
        {
            failure = InventoryFailures.Layout("Package-owned layout data must be an object before entry references can be updated.");
            return false;
        }

        var property = _snapshot.Layout.Data.Properties
            .FirstOrDefault(item => string.Equals(item.Name, propertyName, StringComparison.Ordinal));
        if (property == null ||
            !InventorySnapshotCodecs.TryDecode(property.Value, out List<object?> references, out failure))
        {
            failure = InventoryFailures.Layout($"Package-owned layout property '{propertyName}' is invalid: {failure?.Message}");
            return false;
        }

        var rewritten = new List<object?>(references.Count);
        foreach (var reference in references)
        {
            if (reference is string entryId && string.Equals(entryId, removedEntryId, StringComparison.Ordinal))
            {
                if (!removePositions)
                    rewritten.Add(null);
                continue;
            }
            rewritten.Add(reference);
        }
        property.Value = InventorySnapshotCodecs.Encode(rewritten);
        return true;
    }

    private static bool TryCloneSnapshot(
        InventorySnapshot? source,
        out InventorySnapshot? clone,
        out InventoryFailure? failure)
    {
        clone = null;
        if (source == null)
        {
            failure = InventoryFailures.SnapshotMalformed("Inventory snapshot cannot be null.");
            return false;
        }
        if (source.Entries == null || source.Metadata == null || source.Layout == null)
        {
            failure = InventoryFailures.SnapshotMalformed("Inventory snapshot collections and layout cannot be null.");
            return false;
        }

        var result = new InventorySnapshot
        {
            FormatVersion = source.FormatVersion,
            Layout = CloneLayoutOrThrow(source.Layout)
        };
        foreach (var value in source.Metadata)
            result.Metadata.Add(CloneNamedValueOrThrow(value));
        foreach (var entry in source.Entries)
            result.Entries.Add(CloneEntryOrThrow(entry));
        clone = result;
        failure = null;
        return true;
    }

    private static InventorySnapshotEntry CloneEntryOrThrow(InventorySnapshotEntry entry)
    {
        if (entry == null)
            throw new InventoryOperationException(InventoryFailures.SnapshotMalformed("Snapshot entries cannot contain null items."));
        return new InventorySnapshotEntry
        {
            EntryId = entry.EntryId,
            DefinitionId = CloneEncodedOrThrow(entry.DefinitionId),
            Amount = entry.Amount,
            Metadata = entry.Metadata?.Select(CloneNamedValueOrThrow).ToList()
                ?? throw new InventoryOperationException(InventoryFailures.SnapshotMalformed("Snapshot entry metadata cannot be null."))
        };
    }

    private static InventoryLayoutSnapshot CloneLayoutOrThrow(InventoryLayoutSnapshot layout)
    {
        if (layout == null)
            throw new InventoryOperationException(InventoryFailures.Layout("Snapshot layout cannot be null."));
        if (!SnapshotValueValidator.TryClone(layout.Data, out var data, out var failure) || data == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Layout("Snapshot layout data is invalid."));
        return new InventoryLayoutSnapshot
        {
            Kind = layout.Kind,
            DataVersion = layout.DataVersion,
            Data = data
        };
    }

    private static SnapshotNamedValue CloneNamedValueOrThrow(SnapshotNamedValue value)
    {
        if (value == null)
            throw new InventoryOperationException(InventoryFailures.SnapshotMalformed("Snapshot named values cannot contain null items."));
        return new SnapshotNamedValue
        {
            Name = value.Name,
            Value = CloneEncodedOrThrow(value.Value)
        };
    }

    private static SnapshotEncodedValue CloneEncodedOrThrow(SnapshotEncodedValue value)
    {
        if (!SnapshotValueValidator.TryCloneEncoded(value, out var clone, out var failure) || clone == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.SnapshotMalformed("Encoded snapshot value is invalid."));
        return clone;
    }
}
