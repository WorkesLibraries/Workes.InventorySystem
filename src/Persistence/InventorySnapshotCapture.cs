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
        out InventoryFailure? failure)
    {
        snapshot = null;
        var result = new InventorySnapshot();
        var entryIds = new Dictionary<ItemInstance<TKey>, string>();

        foreach (var pair in inventory.Metadata.EnumerateStored().OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryNamedValue(pair.Key, pair.Value, "inventory metadata", out var named, out failure) ||
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
                failure = InventoryFailures.Definition($"Definition id for entry '{entryId}' cannot be null.");
                return false;
            }
            if (!InventorySnapshotCodecs.TryEncodeKey(
                    instance.Definition.Id,
                    out var definitionId,
                    out failure) ||
                definitionId == null)
            {
                failure = InventoryFailures.Definition($"Definition id for entry '{entryId}' could not be captured: {failure}");
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
                if (!TryNamedValue(pair.Key, pair.Value, "metadata", out var named, out failure) || named == null)
                    return false;
                entry.Metadata.Add(named);
            }
            result.Entries.Add(entry);
        }

        if (!TryCaptureLayout(inventory, entryIds, result.Entries, out var layout, out failure) || layout == null)
            return false;
        result.Layout = layout;

        if (!InventorySnapshotValidator.TryValidate(result, out failure))
            return false;

        snapshot = result;
        return true;
    }

    private static bool TryNamedValue(
        string name,
        object? value,
        string role,
        out SnapshotNamedValue? named,
        out InventoryFailure? failure)
    {
        named = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            failure = InventoryFailures.Snapshot($"Snapshot {role} name cannot be null or empty.");
            return false;
        }
        if (!InventorySnapshotCodecs.TryEncodeObject(value, out var encoded, out failure) || encoded == null)
        {
            failure = InventoryFailures.Snapshot($"Snapshot {role} '{name}' could not be captured: {failure}");
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
        out InventoryFailure? failure)
    {
        snapshot = null;
        var codec = inventory.Layout.SnapshotCodec;
        if (codec == null ||
            string.IsNullOrWhiteSpace(codec.LayoutKind) ||
            codec.CurrentVersion <= 0)
        {
            failure = InventoryFailures.Layout("The layout snapshot codec requires a stable kind and positive version.");
            return false;
        }
        if (!InventoryLayoutSnapshotCodecIdentity.TryAssociate(
                codec.GetType(),
                codec.LayoutKind,
                out failure))
            return false;
        try
        {
            if (!codec.TryCapture(
                    new InventoryLayoutSnapshotCaptureContext<TKey>(inventory, entryIds),
                    out var data,
                    out failure) ||
                data == null)
            {
                failure ??= InventoryFailures.Layout($"Layout snapshot codec '{codec.LayoutKind}' rejected capture.");
                return false;
            }
            if (!SnapshotValueValidator.TryClone(data, out var detached, out failure) || detached == null)
            {
                failure = InventoryFailures.Layout($"Layout snapshot codec '{codec.LayoutKind}' produced invalid data: {failure}");
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
                    out failure) ||
                candidate == null ||
                !string.Equals(candidate.LayoutKind, codec.LayoutKind, StringComparison.Ordinal) ||
                candidate.DataVersion != codec.CurrentVersion)
            {
                snapshot = null;
                failure =
                    InventoryFailures.SnapshotCodecRejected(
                        $"Layout snapshot codec '{codec.LayoutKind}' could not decode its captured data: " +
                        (failure?.Message ?? "The codec returned an incompatible candidate."));
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
                    out failure) ||
                exactLayout == null ||
                ReferenceEquals(exactLayout, inventory.Layout))
            {
                snapshot = null;
                failure =
                    InventoryFailures.SnapshotCodecRejected(
                        $"Layout snapshot codec '{codec.LayoutKind}' could not exactly restore its captured data: " +
                        (failure?.Message ?? "The codec did not return an isolated layout."));
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            failure = InventoryFailures.Layout($"Layout snapshot codec '{codec.LayoutKind}' failed: {ex.Message}");
            return false;
        }

    }
}
