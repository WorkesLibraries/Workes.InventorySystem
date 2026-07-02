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
    public static bool TryValidate(InventorySnapshot? snapshot, out string? error)
    {
        if (snapshot == null)
        {
            error = "Inventory snapshot cannot be null.";
            return false;
        }
        if (snapshot.FormatVersion != InventorySnapshot.CurrentFormatVersion)
        {
            error = $"Inventory snapshot format version {snapshot.FormatVersion} is unsupported.";
            return false;
        }
        if (snapshot.Entries == null || snapshot.Attributes == null || snapshot.Layout == null)
        {
            error = "Inventory snapshot collections and layout cannot be null.";
            return false;
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EntryId) || !entryIds.Add(entry.EntryId))
            {
                error = "Inventory snapshot entry ids must be non-empty and unique.";
                return false;
            }
            if (entry.Amount <= 0)
            {
                error = $"Inventory snapshot entry '{entry.EntryId}' has a non-positive amount.";
                return false;
            }
            if (!SnapshotValueValidator.TryCloneEncoded(entry.DefinitionId, out _, out error))
            {
                error = $"Inventory snapshot entry '{entry.EntryId}' has an invalid definition id: {error}";
                return false;
            }
            if (!TryValidateNamedValues(
                    entry.Metadata,
                    $"entry '{entry.EntryId}' metadata",
                    allowSameNameWithDifferentCodec: false,
                    out error))
                return false;
        }

        if (!TryValidateNamedValues(
                snapshot.Attributes,
                "inventory attributes",
                allowSameNameWithDifferentCodec: true,
                out error))
            return false;
        if (string.IsNullOrWhiteSpace(snapshot.Layout.Kind) || snapshot.Layout.DataVersion <= 0)
        {
            error = "Inventory snapshot layout requires a kind and positive data version.";
            return false;
        }
        if (!SnapshotValueValidator.TryClone(snapshot.Layout.Data, out _, out error))
        {
            error = $"Inventory snapshot layout data is invalid: {error}";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidateNamedValues(
        List<SnapshotNamedValue>? values,
        string role,
        bool allowSameNameWithDifferentCodec,
        out string? error)
    {
        if (values == null)
        {
            error = $"Inventory snapshot {role} cannot be null.";
            return false;
        }
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            string identity = value == null
                ? string.Empty
                : allowSameNameWithDifferentCodec
                    ? value.Name + "\0" + value.Value?.CodecId
                    : value.Name;
            if (value == null || string.IsNullOrWhiteSpace(value.Name) || !identities.Add(identity))
            {
                error = $"Inventory snapshot {role} names must be non-empty and unique.";
                return false;
            }
            if (!SnapshotValueValidator.TryCloneEncoded(value.Value, out _, out error))
            {
                error = $"Inventory snapshot {role} value '{value.Name}' is invalid: {error}";
                return false;
            }
            var encodedValue = value.Value!;
            if (!InventorySnapshotCodecs.IsCodecResolvable(encodedValue.CodecId))
            {
                error =
                    $"Inventory snapshot {role} value '{value.Name}' uses unsupported portable codec " +
                    $"'{encodedValue.CodecId}'.";
                return false;
            }
        }
        error = null;
        return true;
    }
}
