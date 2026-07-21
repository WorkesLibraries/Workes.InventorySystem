using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Validates the serializer-facing structure of an inventory snapshot.
/// </summary>
public static class InventorySnapshotValidator
{
    /// <summary>
    /// Validates format version, entry identity, concrete value shapes, and layout envelope data.
    /// </summary>
    public static bool TryValidate(InventorySnapshot? snapshot, out InventoryFailure? failure)
    {
        if (snapshot == null)
        {
            failure = InventoryFailures.Snapshot("Inventory snapshot cannot be null.");
            return false;
        }
        if (snapshot.FormatVersion != InventorySnapshot.CurrentFormatVersion)
        {
            failure = InventoryFailures.Snapshot($"Inventory snapshot format version {snapshot.FormatVersion} is unsupported.");
            return false;
        }
        if (snapshot.Entries == null || snapshot.Metadata == null || snapshot.Layout == null)
        {
            failure = InventoryFailures.Layout("Inventory snapshot collections and layout cannot be null.");
            return false;
        }
        if (!TryValidateNamedValues(snapshot.Metadata, "inventory metadata", out failure))
            return false;

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EntryId) || !entryIds.Add(entry.EntryId))
            {
                failure = InventoryFailures.Snapshot("Inventory snapshot entry ids must be non-empty and unique.");
                return false;
            }
            if (entry.Amount <= 0)
            {
                failure = InventoryFailures.Snapshot($"Inventory snapshot entry '{entry.EntryId}' has a non-positive amount.");
                return false;
            }
            if (!SnapshotValueValidator.TryCloneEncoded(entry.DefinitionId, out _, out failure))
            {
                failure = InventoryFailures.Definition($"Inventory snapshot entry '{entry.EntryId}' has an invalid definition id: {failure}");
                return false;
            }
            if (!TryValidateNamedValues(
                    entry.Metadata,
                    $"entry '{entry.EntryId}' metadata",
                    out failure))
                return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Layout.Kind) || snapshot.Layout.DataVersion <= 0)
        {
            failure = InventoryFailures.Layout("Inventory snapshot layout requires a kind and positive data version.");
            return false;
        }
        if (!SnapshotValueValidator.TryClone(snapshot.Layout.Data, out _, out failure))
        {
            failure = InventoryFailures.Layout($"Inventory snapshot layout data is invalid: {failure}");
            return false;
        }
        failure = null;
        return true;
    }

    private static bool TryValidateNamedValues(
        List<SnapshotNamedValue>? values,
        string role,
        out InventoryFailure? failure)
    {
        if (values == null)
        {
            failure = InventoryFailures.Snapshot($"Inventory snapshot {role} cannot be null.");
            return false;
        }
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            string identity = value == null
                ? string.Empty
                : value.Name;
            if (value == null || string.IsNullOrWhiteSpace(value.Name) || !identities.Add(identity))
            {
                failure = InventoryFailures.Snapshot($"Inventory snapshot {role} names must be non-empty and unique.");
                return false;
            }
            if (!SnapshotValueValidator.TryCloneEncoded(value.Value, out _, out failure))
            {
                failure = InventoryFailures.Snapshot($"Inventory snapshot {role} value '{value.Name}' is invalid: {failure}");
                return false;
            }
            var encodedValue = value.Value!;
            if (!InventorySnapshotCodecs.IsCodecResolvable(encodedValue.CodecId))
            {
                failure =
                    InventoryFailures.SnapshotMissingCodec(
                        $"Inventory snapshot {role} value '{value.Name}' uses unsupported portable codec " +
                        $"'{encodedValue.CodecId}'.");
                return false;
            }
        }
        failure = null;
        return true;
    }
}
