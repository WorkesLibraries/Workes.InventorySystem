using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Persistence;

internal static class BuiltInLayoutSnapshot
{
    internal const int Version = 1;

    internal static SnapshotEncodedValue Encode<T>(T value) => InventorySnapshotCodecs.Encode(value);

    internal static SnapshotNamedValue Property(string name, SnapshotEncodedValue value) =>
        new() { Name = name, Value = value };

    internal static bool TryReferences<TKey>(
        InventoryLayoutSnapshotCaptureContext<TKey> context,
        IEnumerable<int?> storageIndices,
        out SnapshotEncodedValue? encoded,
        out InventoryFailure? failure)
    {
        var references = new List<object?>();
        foreach (var storageIndex in storageIndices)
        {
            if (!storageIndex.HasValue)
            {
                references.Add(null);
                continue;
            }
            if (storageIndex.Value < 0 || storageIndex.Value >= context.Inventory.Items.Count ||
                !context.TryGetEntryId(context.Inventory.Items[storageIndex.Value], out var entryId))
            {
                encoded = null;
                failure = InventoryFailures.Layout("Layout state references an item that is not part of the captured inventory.");
                return false;
            }
            references.Add(entryId);
        }
        encoded = Encode(references);
        failure = null;
        return true;
    }

    internal static bool TryDecode<TKey>(
        InventoryLayoutSnapshotDecodeContext<TKey> context,
        string expectedKind,
        string referenceProperty,
        bool allowRepeatedEntries,
        Func<int, ILayoutContext<TKey>> contextFactory,
        Func<IReadOnlyDictionary<string, SnapshotEncodedValue>, int, bool> validateShape,
        out InventoryLayoutSnapshotCandidate<TKey>? candidate,
        out InventoryFailure? failure)
    {
        candidate = null;
        var snapshot = context.Snapshot;
        if (!string.Equals(snapshot.Kind, expectedKind, StringComparison.Ordinal))
        {
            failure = InventoryFailures.Layout($"Layout codec '{expectedKind}' cannot decode layout kind '{snapshot.Kind}'.");
            return false;
        }
        if (snapshot.DataVersion != Version || snapshot.Data.Kind != SnapshotValueKind.Object)
        {
            failure = InventoryFailures.Layout($"Layout '{expectedKind}' requires object data version {Version}.");
            return false;
        }
        if (!SnapshotValueValidator.TryClone(snapshot.Data, out var detached, out failure) || detached == null)
            return false;

        var properties = detached.Properties.ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        if (!properties.TryGetValue(referenceProperty, out var encodedReferences) ||
            !InventorySnapshotCodecs.TryDecode(encodedReferences, out List<object?> references, out failure))
        {
            failure = InventoryFailures.Layout($"Layout '{expectedKind}' has invalid '{referenceProperty}' references: {failure}");
            return false;
        }
        if (!validateShape(properties, references.Count))
        {
            failure = InventoryFailures.Layout($"Layout '{expectedKind}' has malformed shape data.");
            return false;
        }

        var contexts = new Dictionary<string, List<ILayoutContext<TKey>>>(StringComparer.Ordinal);
        for (int index = 0; index < references.Count; index++)
        {
            if (references[index] == null)
                continue;
            if (references[index] is not string entryId || !context.TryGetEntry(entryId, out _))
            {
                failure = InventoryFailures.Layout($"Layout '{expectedKind}' references unknown entry id at position {index}.");
                return false;
            }
            if (!contexts.TryGetValue(entryId, out var itemContexts))
                contexts.Add(entryId, itemContexts = new List<ILayoutContext<TKey>>());
            else if (!allowRepeatedEntries)
            {
                failure = InventoryFailures.Layout($"Layout '{expectedKind}' maps entry '{entryId}' more than once.");
                return false;
            }
            itemContexts.Add(contextFactory(index));
        }
        if (contexts.Count != context.EntryCount)
        {
            failure = InventoryFailures.Layout($"Layout '{expectedKind}' must place every snapshot entry.");
            return false;
        }

        var readonlyContexts = contexts.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ILayoutContext<TKey>>)pair.Value,
            StringComparer.Ordinal);
        candidate = new InventoryLayoutSnapshotCandidate<TKey>(
            expectedKind,
            snapshot.DataVersion,
            detached,
            readonlyContexts);
        failure = null;
        return true;
    }

    internal static bool TryPositiveInt(
        IReadOnlyDictionary<string, SnapshotEncodedValue> properties,
        string name,
        out int value)
    {
        value = 0;
        return properties.TryGetValue(name, out var encoded) &&
               InventorySnapshotCodecs.TryDecode(encoded, out value, out _) &&
               value > 0;
    }

    internal static bool TryProperty<T>(
        InventoryLayoutSnapshotCandidate<T> candidate,
        string name,
        out SnapshotEncodedValue? value)
    {
        value = candidate.Data.Properties
            .FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.Ordinal))
            ?.Value;
        return value != null;
    }

    internal static bool TryStorageIndices<TKey>(
        InventoryLayoutSnapshotRestoreContext<TKey> context,
        string propertyName,
        out List<int?> indices,
        out InventoryFailure? failure)
    {
        indices = new List<int?>();
        failure = null;
        if (!TryProperty(context.Candidate, propertyName, out var encoded) ||
            !InventorySnapshotCodecs.TryDecode(encoded!, out List<object?> references, out failure))
        {
            failure = InventoryFailures.Layout($"Layout snapshot property '{propertyName}' is invalid: {failure}");
            return false;
        }
        foreach (var reference in references)
        {
            if (reference == null)
            {
                indices.Add(null);
                continue;
            }
            if (reference is not string entryId ||
                !context.StorageIndices.TryGetValue(entryId, out int storageIndex))
            {
                failure = InventoryFailures.Layout($"Layout snapshot property '{propertyName}' references an unknown entry.");
                return false;
            }
            indices.Add(storageIndex);
        }
        failure = null;
        return true;
    }

    internal static bool TryDecodedProperty<TKey, TValue>(
        InventoryLayoutSnapshotRestoreContext<TKey> context,
        string name,
        out TValue value,
        out InventoryFailure? failure)
    {
        value = default!;
        failure = null;
        if (!TryProperty(context.Candidate, name, out var encoded) ||
            !InventorySnapshotCodecs.TryDecode(encoded!, out value, out failure))
        {
            failure = InventoryFailures.Layout($"Layout snapshot property '{name}' is invalid: {failure}");
            return false;
        }
        return true;
    }
}

internal sealed class EntryLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static EntryLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.entry";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not EntryLayout<TKey> layout ||
            layout.GetPersistentData() is not EntryLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.Order.Select(value => (int?)value), out var order, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Entry layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[] { BuiltInLayoutSnapshot.Property("order", order!) });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure) =>
        BuiltInLayoutSnapshot.TryDecode(
            context, LayoutKind, "order", false,
            index => new EntryLayoutContext<TKey>(index),
            (_, count) => count == context.EntryCount,
            out candidate, out failure);

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not EntryLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "order", out var indices, out failure) ||
            indices.Any(index => !index.HasValue))
        {
            failure ??= InventoryFailures.Layout("Exact entry-layout restoration requires compatible entry layout data.");
            return false;
        }
        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new EntryLayoutPersistentData
            {
                Order = indices.Select(index => index!.Value).ToList()
            });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact entry-layout restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}

internal sealed class SlotLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static SlotLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.slot";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not SlotLayout<TKey> layout ||
            layout.GetPersistentData() is not SlotLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.SlotMap, out var slots, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Slot layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[] { BuiltInLayoutSnapshot.Property("slots", slots!) });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure) =>
        BuiltInLayoutSnapshot.TryDecode(context, LayoutKind, "slots", false,
            index => new SlotLayoutContext<TKey>(index), (_, _) => true, out candidate, out failure);

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not SlotLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "slots", out var slots, out failure))
        {
            failure ??= InventoryFailures.Layout("Exact slot-layout restoration requires compatible slot data.");
            return false;
        }
        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new SlotLayoutPersistentData { SlotMap = slots });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact slot-layout restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}

internal sealed class GridLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static GridLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.grid";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not GridLayout<TKey> layout ||
            layout.GetPersistentData() is not GridLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.CellMap, out var cells, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Grid layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[]
        {
            BuiltInLayoutSnapshot.Property("width", BuiltInLayoutSnapshot.Encode(state.Width)),
            BuiltInLayoutSnapshot.Property("height", BuiltInLayoutSnapshot.Encode(state.Height)),
            BuiltInLayoutSnapshot.Property("placementOrder", BuiltInLayoutSnapshot.Encode(state.PlacementOrder.ToString())),
            BuiltInLayoutSnapshot.Property("cells", cells!)
        });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure)
    {
        int width = 0;
        return BuiltInLayoutSnapshot.TryDecode(context, LayoutKind, "cells", false,
            index => new GridLayoutContext<TKey>(index % width, index / width),
            (properties, count) =>
            {
                if (!BuiltInLayoutSnapshot.TryPositiveInt(properties, "width", out width) ||
                    !BuiltInLayoutSnapshot.TryPositiveInt(properties, "height", out int height) ||
                    count != (long)width * height ||
                    !properties.TryGetValue("placementOrder", out var order) ||
                    !InventorySnapshotCodecs.TryDecode(order, out string orderName, out _))
                    return false;
                return Enum.TryParse<GridPlacementOrder>(orderName, false, out _);
            }, out candidate, out failure);
    }

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not GridLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "cells", out var cells, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "width", out int width, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "height", out int height, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "placementOrder", out string orderName, out failure) ||
            !Enum.TryParse(orderName, false, out GridPlacementOrder order))
        {
            failure ??= InventoryFailures.Layout("Exact grid-layout restoration requires compatible grid data.");
            return false;
        }
        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new GridLayoutPersistentData
            {
                Width = width,
                Height = height,
                PlacementOrder = order,
                CellMap = cells
            });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact grid-layout restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}

internal sealed class MultiCellGridLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static MultiCellGridLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.multi-cell-grid";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not MultiCellGridLayout<TKey> layout ||
            layout.GetPersistentData() is not MultiCellGridLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.CellMap, out var cells, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Multi-cell grid layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[]
        {
            BuiltInLayoutSnapshot.Property("width", BuiltInLayoutSnapshot.Encode(state.Width)),
            BuiltInLayoutSnapshot.Property("height", BuiltInLayoutSnapshot.Encode(state.Height)),
            BuiltInLayoutSnapshot.Property("placementOrder", BuiltInLayoutSnapshot.Encode(state.PlacementOrder.ToString())),
            BuiltInLayoutSnapshot.Property("defaultAnchor", BuiltInLayoutSnapshot.Encode(state.DefaultAnchor.ToString())),
            BuiltInLayoutSnapshot.Property("cells", cells!)
        });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure)
    {
        int width = 0;
        return BuiltInLayoutSnapshot.TryDecode(context, LayoutKind, "cells", true,
            index => new MultiCellGridLayoutContext<TKey>(index % width, index / width),
            (properties, count) =>
            {
                if (!BuiltInLayoutSnapshot.TryPositiveInt(properties, "width", out width) ||
                    !BuiltInLayoutSnapshot.TryPositiveInt(properties, "height", out int height) ||
                    count != (long)width * height ||
                    !properties.TryGetValue("placementOrder", out var order) ||
                    !properties.TryGetValue("defaultAnchor", out var anchor) ||
                    !InventorySnapshotCodecs.TryDecode(order, out string orderName, out _) ||
                    !InventorySnapshotCodecs.TryDecode(anchor, out string anchorName, out _))
                    return false;
                return Enum.TryParse<GridPlacementOrder>(orderName, false, out _) &&
                       Enum.TryParse<GridAnchor>(anchorName, false, out _);
            }, out candidate, out failure);
    }

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not MultiCellGridLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "cells", out var cells, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "width", out int width, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "height", out int height, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "placementOrder", out string orderName, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "defaultAnchor", out string anchorName, out failure) ||
            !Enum.TryParse(orderName, false, out GridPlacementOrder order) ||
            !Enum.TryParse(anchorName, false, out GridAnchor anchor))
        {
            failure ??= InventoryFailures.Layout("Exact multi-cell grid restoration requires compatible grid data.");
            return false;
        }

        foreach (var pair in context.StorageIndices)
        {
            var occupied = new List<int>();
            for (int cell = 0; cell < cells.Count; cell++)
            {
                if (cells[cell] == pair.Value)
                    occupied.Add(cell);
            }
            var footprint = target.FootprintProvider.GetFootprint(context.Instances[pair.Key].Definition);
            if (occupied.Count != footprint.Width * footprint.Height ||
                occupied.Count == 0)
            {
                failure = InventoryFailures.Layout($"Saved cells for entry '{pair.Key}' do not match its current footprint.");
                return false;
            }
            int minX = occupied.Min(cell => cell % width);
            int maxX = occupied.Max(cell => cell % width);
            int minY = occupied.Min(cell => cell / width);
            int maxY = occupied.Max(cell => cell / width);
            if (maxX - minX + 1 != footprint.Width ||
                maxY - minY + 1 != footprint.Height)
            {
                failure = InventoryFailures.Layout($"Saved cells for entry '{pair.Key}' are not a valid current footprint.");
                return false;
            }
        }

        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new MultiCellGridLayoutPersistentData
            {
                Width = width,
                Height = height,
                PlacementOrder = order,
                DefaultAnchor = anchor,
                CellMap = cells
            });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact multi-cell grid restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}

internal sealed class EquipmentLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static EquipmentLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.equipment";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not EquipmentLayout<TKey> layout ||
            layout.GetPersistentData() is not EquipmentLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.SlotMap, out var slots, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Equipment layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[]
        {
            BuiltInLayoutSnapshot.Property("slotIds", BuiltInLayoutSnapshot.Encode(state.SlotIds)),
            BuiltInLayoutSnapshot.Property("slots", slots!)
        });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure)
    {
        List<string>? slotIds = null;
        return BuiltInLayoutSnapshot.TryDecode(context, LayoutKind, "slots", false,
            index => new EquipmentLayoutContext<TKey>(slotIds![index]),
            (properties, count) =>
            {
                if (!properties.TryGetValue("slotIds", out var encoded) ||
                    !InventorySnapshotCodecs.TryDecode(encoded, out List<string> decodedSlotIds, out _))
                    return false;
                slotIds = decodedSlotIds;
                return slotIds.Count == count &&
                       slotIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
                       slotIds.Distinct(StringComparer.Ordinal).Count() == slotIds.Count;
            }, out candidate, out failure);
    }

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not EquipmentLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "slots", out var slots, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "slotIds", out List<string> slotIds, out failure))
        {
            failure ??= InventoryFailures.Layout("Exact equipment-layout restoration requires compatible slot data.");
            return false;
        }
        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new EquipmentLayoutPersistentData
            {
                SlotIds = slotIds,
                SlotMap = slots
            });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact equipment-layout restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}

internal sealed class SectionedLayoutSnapshotCodec<TKey> : IInventoryLayoutSnapshotCodec<TKey>
{
    internal static SectionedLayoutSnapshotCodec<TKey> Instance { get; } = new();
    public string LayoutKind => "workes.inventory.layout.sectioned";
    public int CurrentVersion => BuiltInLayoutSnapshot.Version;

    public bool TryCapture(InventoryLayoutSnapshotCaptureContext<TKey> context, out SnapshotValue? data, out InventoryFailure? failure)
    {
        failure = null;
        if (context.Layout is not SectionedLayout<TKey> layout ||
            layout.GetPersistentData() is not SectionedLayoutPersistentData state ||
            !BuiltInLayoutSnapshot.TryReferences(context, state.SlotMap, out var slots, out failure))
        {
            data = null;
            failure ??= InventoryFailures.Layout("Sectioned layout codec received an incompatible layout.");
            return false;
        }
        data = SnapshotValue.Object(new[]
        {
            BuiltInLayoutSnapshot.Property("sectionIds", BuiltInLayoutSnapshot.Encode(state.SectionIds)),
            BuiltInLayoutSnapshot.Property("sectionSlotCounts", BuiltInLayoutSnapshot.Encode(state.SectionSlotCounts)),
            BuiltInLayoutSnapshot.Property("slots", slots!)
        });
        return true;
    }

    public bool TryDecode(InventoryLayoutSnapshotDecodeContext<TKey> context, out InventoryLayoutSnapshotCandidate<TKey>? candidate, out InventoryFailure? failure)
    {
        List<string>? sectionIds = null;
        List<int>? counts = null;
        int[]? starts = null;
        return BuiltInLayoutSnapshot.TryDecode(context, LayoutKind, "slots", false,
            index =>
            {
                int section = Array.FindLastIndex(starts!, start => start <= index);
                return new SectionedLayoutContext<TKey>(sectionIds![section], index - starts![section]);
            },
            (properties, count) =>
            {
                if (!properties.TryGetValue("sectionIds", out var ids) ||
                    !properties.TryGetValue("sectionSlotCounts", out var sizes) ||
                    !InventorySnapshotCodecs.TryDecode(ids, out List<string> decodedSectionIds, out _) ||
                    !InventorySnapshotCodecs.TryDecode(sizes, out List<int> decodedCounts, out _) ||
                    decodedSectionIds.Count != decodedCounts.Count ||
                    decodedSectionIds.Count == 0 ||
                    decodedSectionIds.Any(string.IsNullOrWhiteSpace) ||
                    decodedSectionIds.Distinct(StringComparer.Ordinal).Count() != decodedSectionIds.Count ||
                    decodedCounts.Any(value => value <= 0))
                    return false;
                sectionIds = decodedSectionIds;
                counts = decodedCounts;

                long total = 0;
                starts = new int[counts.Count];
                for (int i = 0; i < counts.Count; i++)
                {
                    if (total > int.MaxValue)
                        return false;
                    starts[i] = (int)total;
                    total += counts[i];
                    if (total > int.MaxValue)
                        return false;
                }
                return total == count;
            }, out candidate, out failure);
    }

    public bool TryCreateExactLayout(InventoryLayoutSnapshotRestoreContext<TKey> context, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        layout = null;
        failure = null;
        if (context.TargetLayout is not SectionedLayout<TKey> target ||
            !BuiltInLayoutSnapshot.TryStorageIndices(context, "slots", out var slots, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "sectionIds", out List<string> sectionIds, out failure) ||
            !BuiltInLayoutSnapshot.TryDecodedProperty(context, "sectionSlotCounts", out List<int> counts, out failure))
        {
            failure ??= InventoryFailures.Layout("Exact sectioned-layout restoration requires compatible section data.");
            return false;
        }
        var restored = target.Clone();
        try
        {
            restored.RestorePersistentData(new SectionedLayoutPersistentData
            {
                SectionIds = sectionIds,
                SectionSlotCounts = counts,
                SlotMap = slots
            });
        }
        catch (Exception ex)
        {
            failure = InventoryFailures.Layout($"Exact sectioned-layout restoration failed: {ex.Message}");
            return false;
        }
        layout = restored;
        failure = null;
        return true;
    }
}
