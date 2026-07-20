using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Persistence;

internal static class InventorySnapshotCapture
{
    public static bool TryCapture<TKey>(
        Inventory<TKey> inventory,
        out InventorySnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        var result = new InventorySnapshot();
        var entryIds = new Dictionary<ItemInstance<TKey>, string>();

        foreach (var pair in inventory.Metadata.EnumerateStored().OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryNamedValue(pair.Key, pair.Value, "inventory metadata", out var named, out error) ||
                named == null)
                return false;
            result.Metadata.Add(named);
        }

        for (int index = 0; index < inventory.Items.Count; index++)
        {
            var instance = inventory.Items[index];
            string entryId = "e" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            entryIds.Add(instance, entryId);

            if (instance.Definition.Id is null)
            {
                error = $"Definition id for entry '{entryId}' cannot be null.";
                return false;
            }
            if (!InventorySnapshotCodecs.TryEncodeKey(
                    instance.Definition.Id,
                    out var definitionId,
                    out error) ||
                definitionId == null)
            {
                error = $"Definition id for entry '{entryId}' could not be captured: {error}";
                return false;
            }

            var entry = new InventorySnapshotEntry
            {
                EntryId = entryId,
                DefinitionId = definitionId,
                Amount = instance.Amount
            };
            foreach (var pair in instance.Metadata.AsReadOnly().OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!TryNamedValue(pair.Key, pair.Value, "metadata", out var named, out error) || named == null)
                    return false;
                entry.Metadata.Add(named);
            }
            result.Entries.Add(entry);
        }

        if (!TryCaptureLayout(inventory, entryIds, result.Entries, out var layout, out error) || layout == null)
            return false;
        result.Layout = layout;

        if (!InventorySnapshotValidator.TryValidate(result, out error))
            return false;

        snapshot = result;
        return true;
    }

    private static bool TryNamedValue(
        string name,
        object? value,
        string role,
        out SnapshotNamedValue? named,
        out string? error)
    {
        named = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = $"Snapshot {role} name cannot be null or empty.";
            return false;
        }
        if (!InventorySnapshotCodecs.TryEncodeObject(value, out var encoded, out error) || encoded == null)
        {
            error = $"Snapshot {role} '{name}' could not be captured: {error}";
            return false;
        }
        named = new SnapshotNamedValue { Name = name, Value = encoded };
        return true;
    }

    private static bool TryCaptureLayout<TKey>(
        Inventory<TKey> inventory,
        IReadOnlyDictionary<ItemInstance<TKey>, string> entryIds,
        IReadOnlyList<InventorySnapshotEntry> snapshotEntries,
        out InventoryLayoutSnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        var codec = inventory.Layout.SnapshotCodec;
        if (codec == null ||
            string.IsNullOrWhiteSpace(codec.LayoutKind) ||
            codec.CurrentVersion <= 0)
        {
            error = "The layout snapshot codec requires a stable kind and positive version.";
            return false;
        }
        if (!InventoryLayoutSnapshotCodecIdentity.TryAssociate(
                codec.GetType(),
                codec.LayoutKind,
                out error))
            return false;
        try
        {
            if (!codec.TryCapture(
                    new InventoryLayoutSnapshotCaptureContext<TKey>(inventory, entryIds),
                    out var data,
                    out error) ||
                data == null)
            {
                error ??= $"Layout snapshot codec '{codec.LayoutKind}' rejected capture.";
                return false;
            }
            if (!SnapshotValueValidator.TryClone(data, out var detached, out error) || detached == null)
            {
                error = $"Layout snapshot codec '{codec.LayoutKind}' produced invalid data: {error}";
                return false;
            }
            snapshot = new InventoryLayoutSnapshot
            {
                Kind = codec.LayoutKind,
                DataVersion = codec.CurrentVersion,
                Data = detached
            };
            var entries = snapshotEntries.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
            if (!codec.TryDecode(
                    new InventoryLayoutSnapshotDecodeContext<TKey>(snapshot, entries),
                    out var candidate,
                    out error) ||
                candidate == null ||
                !string.Equals(candidate.LayoutKind, codec.LayoutKind, StringComparison.Ordinal) ||
                candidate.DataVersion != codec.CurrentVersion)
            {
                snapshot = null;
                error =
                    $"Layout snapshot codec '{codec.LayoutKind}' could not decode its captured data: " +
                    (error ?? "The codec returned an incompatible candidate.");
                return false;
            }
            var storageIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            var instances = new Dictionary<string, ItemInstance<TKey>>(StringComparer.Ordinal);
            for (int index = 0; index < inventory.Items.Count; index++)
            {
                var current = inventory.Items[index];
                string entryId = entryIds[current];
                storageIndices.Add(entryId, index);
                instances.Add(
                    entryId,
                    new ItemInstance<TKey>(
                        current.Definition,
                        current.Amount,
                        current.Metadata.IsEmpty ? null : current.Metadata.Clone()));
            }
            if (!codec.TryCreateExactLayout(
                    new InventoryLayoutSnapshotRestoreContext<TKey>(
                        inventory.Layout,
                        candidate,
                        storageIndices,
                        instances),
                    out var exactLayout,
                    out error) ||
                exactLayout == null ||
                ReferenceEquals(exactLayout, inventory.Layout))
            {
                snapshot = null;
                error =
                    $"Layout snapshot codec '{codec.LayoutKind}' could not exactly restore its captured data: " +
                    (error ?? "The codec did not return an isolated layout.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            error = $"Layout snapshot codec '{codec.LayoutKind}' failed: {ex.Message}";
            return false;
        }

    }
}
