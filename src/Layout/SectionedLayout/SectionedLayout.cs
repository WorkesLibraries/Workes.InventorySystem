using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Persistence;
using System.ComponentModel;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Fixed-position layout split into named sections with optional tag compatibility.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Section compatibility can use catalog-resolved tags, explicit item definition ids, or both. When both are
/// configured, an item can be placed if it satisfies the required tags or its definition id is explicitly allowed.
/// Sections without tag or definition restrictions accept any item that otherwise satisfies inventory rules. Sorting
/// compacts placed items into compatible section slots without changing inventory storage order.
/// </remarks>
public sealed class SectionedLayout<TKey> : IParameterizedRepackableInventoryLayout<TKey>
{
    /// <inheritdoc />
    public IInventoryLayoutSnapshotCodec<TKey> SnapshotCodec => SectionedLayoutSnapshotCodec<TKey>.Instance;
    private readonly List<SectionDefinition<TKey>> _sections;
    private readonly List<int?> _slotMap;
    private readonly Dictionary<string, int> _sectionIndices;
    private readonly Dictionary<string, int> _sectionStartOffsets;

    /// <summary>
    /// Gets the sections in layout order.
    /// </summary>
    public IReadOnlyList<SectionDefinition<TKey>> Sections => _sections;

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters =>
        _sections
            .Select(section => new InventoryParameterDefinition(
                $"section:{section.Id}.slotCount",
                typeof(int),
                $"Number of slots in section '{section.Id}'."))
            .ToList();

    /// <summary>
    /// Creates a sectioned layout.
    /// </summary>
    /// <param name="sections">The sections in layout order.</param>
    public SectionedLayout(params SectionDefinition<TKey>[] sections)
        : this((IEnumerable<SectionDefinition<TKey>>)sections)
    {
    }

    /// <summary>
    /// Creates a sectioned layout.
    /// </summary>
    /// <param name="sections">The sections in layout order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No sections are provided or a section id is duplicated.</exception>
    public SectionedLayout(IEnumerable<SectionDefinition<TKey>> sections)
    {
        if (sections == null)
            throw new ArgumentNullException(nameof(sections));

        _sections = sections.ToList();
        if (_sections.Count == 0)
            throw new ArgumentException("Sectioned layout must contain at least one section.", nameof(sections));

        _sectionIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        _sectionStartOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        _slotMap = new List<int?>();
        int offset = 0;
        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            if (!_sectionIndices.TryAdd(section.Id, i))
                throw new ArgumentException("Section ids must be unique.", nameof(sections));

            _sectionStartOffsets.Add(section.Id, offset);
            for (int slot = 0; slot < section.SlotCount; slot++)
                _slotMap.Add(null);
            offset += section.SlotCount;
        }
    }

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayout(
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? failure)
    {
        layout = new SectionedLayout<TKey>(CloneSections());
        failure = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayoutWithParameter(
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? failure)
    {
        layout = null;
        if (!TryCreateSectionsWithParameter(parameterId, value, out var newSections, out failure) || newSections == null)
            return false;

        layout = new SectionedLayout<TKey>(newSections);
        failure = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? failure)
    {
        layout = null;
        if (!TryCreateSectionsWithParameter(parameterId, value, out var newSections, out failure) || newSections == null)
            return false;

        int newTotalSlots = newSections.Sum(section => section.SlotCount);
        var newMap = new List<int?>(newTotalSlots);
        for (int i = 0; i < newTotalSlots; i++)
            newMap.Add(null);

        int oldOffset = 0;
        int newOffset = 0;
        for (int sectionIndex = 0; sectionIndex < _sections.Count; sectionIndex++)
        {
            var oldSection = _sections[sectionIndex];
            var newSection = newSections[sectionIndex];
            for (int slot = 0; slot < oldSection.SlotCount; slot++)
            {
                var storageIndex = _slotMap[oldOffset + slot];
                if (!storageIndex.HasValue)
                    continue;

                if (slot >= newSection.SlotCount)
                {
                    failure = InventoryFailures.Layout($"Cannot shrink section '{oldSection.Id}' because a removed slot is occupied.");
                    return false;
                }

                newMap[newOffset + slot] = storageIndex;
            }

            oldOffset += oldSection.SlotCount;
            newOffset += newSection.SlotCount;
        }

        var replacement = new SectionedLayout<TKey>(newSections);
        replacement.RestorePersistentData(new SectionedLayoutPersistentData
        {
            SectionIds = newSections.Select(section => section.Id).ToList(),
            SectionSlotCounts = newSections.Select(section => section.SlotCount).ToList(),
            SlotMap = newMap
        });

        layout = replacement;
        failure = null;
        return true;
    }

    private bool TryCreateSectionsWithParameter(
        string parameterId,
        object? value,
        out List<SectionDefinition<TKey>>? sections,
        out InventoryFailure? failure)
    {
        sections = null;
        if (!TryParseSectionSlotCountParameter(parameterId, out var sectionId))
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' is not supported by SectionedLayout.");
            return false;
        }

        if (value is not int slotCount)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' expects value type 'Int32'.");
            return false;
        }

        if (slotCount <= 0)
        {
            failure = InventoryFailures.Layout("Section slot count must be greater than zero.");
            return false;
        }

        int targetSectionIndex = _sections.FindIndex(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
        if (targetSectionIndex < 0)
        {
            failure = InventoryFailures.Layout($"Section '{sectionId}' was not found.");
            return false;
        }

        var newSections = new List<SectionDefinition<TKey>>(_sections.Count);
        for (int i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            int newSlotCount = i == targetSectionIndex ? slotCount : section.SlotCount;
            newSections.Add(new SectionDefinition<TKey>(
                section.Id,
                newSlotCount,
                new SectionDefinitionOptions<TKey>
                {
                    RequiredTags = section.RequiredTags,
                    AllowedDefinitionIds = section.AllowedDefinitionIds
                }));
        }

        sections = newSections;
        failure = null;
        return true;
    }

    private List<SectionDefinition<TKey>> CloneSections()
    {
        return _sections
            .Select(section => new SectionDefinition<TKey>(
                section.Id,
                section.SlotCount,
                new SectionDefinitionOptions<TKey>
                {
                    RequiredTags = section.RequiredTags,
                    AllowedDefinitionIds = section.AllowedDefinitionIds
                }))
            .ToList();
    }

    private static bool TryParseSectionSlotCountParameter(string parameterId, out string sectionId)
    {
        sectionId = string.Empty;
        const string prefix = "section:";
        const string suffix = ".slotCount";
        if (string.IsNullOrWhiteSpace(parameterId) ||
            !parameterId.StartsWith(prefix, StringComparison.Ordinal) ||
            !parameterId.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        sectionId = parameterId.Substring(prefix.Length, parameterId.Length - prefix.Length - suffix.Length);
        return !string.IsNullOrWhiteSpace(sectionId);
    }

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory) => _slotMap.Count;

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_slotMap.Count);
        foreach (var section in _sections)
        {
            for (int slot = 0; slot < section.SlotCount; slot++)
                contexts.Add(SectionedLayoutContext<TKey>.Single(section.Id, slot));
        }

        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (!TryGetSingleContext(context, out var sectionContext))
            return null;
        if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out int flatIndex))
            return null;

        var storageIndex = _slotMap[flatIndex];
        if (!storageIndex.HasValue || storageIndex.Value < 0 || storageIndex.Value >= inventory.Items.Count)
            return null;

        return inventory.Items[storageIndex.Value];
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        for (int flatIndex = 0; flatIndex < _slotMap.Count; flatIndex++)
        {
            if (_slotMap[flatIndex] == storageIndex)
            {
                var (sectionId, slotIndex) = ToSectionSlot(flatIndex);
                return new List<ILayoutContext<TKey>> { SectionedLayoutContext<TKey>.Single(sectionId, slotIndex) };
            }
        }

        return Array.Empty<ILayoutContext<TKey>>();
    }

    /// <inheritdoc />
    public bool TryGetContextForStorageIndex(Inventory<TKey> inventory, int storageIndex, out ILayoutContext<TKey>? context)
    {
        var contexts = GetContextsForStorageIndex(inventory, storageIndex);
        context = contexts.Count > 0 ? contexts[0] : null;
        return context != null;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context)
    {
        if (context is SectionedLayoutContext<TKey> sectionContext && !sectionContext.IsMapped)
        {
            if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out int flatIndex))
                yield break;

            var storageIndex = _slotMap[flatIndex];
            if (storageIndex.HasValue)
                yield return storageIndex.Value;
            yield break;
        }

        if (context != null)
            yield break;

        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i].HasValue)
                yield return _slotMap[i]!.Value;
        }
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out InventoryFailure? failure)
    {
        failure = null;
        foreach (var (index, _) in transaction.AmountDeltas)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                failure = InventoryFailures.Layout("Index out of range.");
                return false;
            }
        }

        var removedIndices = new HashSet<int>();
        foreach (var (index, _) in transaction.Removed)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                failure = InventoryFailures.Layout("Index out of range.");
                return false;
            }
            removedIndices.Add(index);
        }

        var simulated = new List<int?>(_slotMap);
        var removed = new List<int>(removedIndices);
        removed.Sort((a, b) => b.CompareTo(a));
        foreach (int removedIndex in removed)
            ApplyRemovalToSlotMap(simulated, removedIndex);

        int futureStorageIndex = inventory.Items.Count - removed.Count;
        var explicitSlots = new HashSet<int>();
        for (int addedIndex = 0; addedIndex < transaction.Added.Count; addedIndex++)
        {
            var (instance, itemContext) = transaction.Added[addedIndex];
            int flatIndex;
            if (itemContext is SectionedLayoutContext<TKey> sectionContext)
            {
                if (sectionContext.IsMapped)
                {
                    failure = InventoryFailures.Layout("Invalid context type.");
                    return false;
                }
                if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out flatIndex))
                {
                    failure = InventoryFailures.Layout("Section slot not found.");
                    return false;
                }
                if (!explicitSlots.Add(flatIndex))
                {
                    failure = InventoryFailures.Layout("Duplicate mapped target section slot.");
                    return false;
                }
                if (!CanSectionAccept(inventory, _sections[_sectionIndices[sectionContext.SectionId]], instance.Definition))
                {
                    failure = InventoryFailures.Layout("No compatible section slot available.");
                    return false;
                }
            }
            else if (itemContext == null)
            {
                flatIndex = FindFirstCompatibleEmptySlot(inventory, simulated, instance.Definition);
                if (flatIndex < 0)
                {
                    failure = InventoryFailures.Layout("No compatible section slot available.");
                    return false;
                }
            }
            else
            {
                failure = InventoryFailures.Layout("Invalid context type.");
                return false;
            }

            if (simulated[flatIndex].HasValue)
            {
                failure = InventoryFailures.Layout("Section slot already occupied.");
                return false;
            }

            simulated[flatIndex] = futureStorageIndex + addedIndex;
        }

        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryApplyPlacementContext(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        ILayoutContext<TKey>? context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out InventoryFailure? failure)
    {
        mappedTransaction = null;
        failure = null;
        if (context == null)
        {
            mappedTransaction = transaction;
            return true;
        }
        if (context is not SectionedLayoutContext<TKey> sectionContext)
        {
            failure = InventoryFailures.Layout("Invalid context type.");
            return false;
        }

        if (!sectionContext.IsMapped)
        {
            if (transaction.Added.Count == 1)
                return TryCreateAddedCopy(transaction, 0, sectionContext, out mappedTransaction, out failure);

            if (transaction.Added.Count == 0 && transaction.AmountDeltas.Count == 1 && transaction.AmountDeltas[0].delta > 0)
            {
                if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out int flatIndex))
                {
                    failure = InventoryFailures.Layout("Section slot not found.");
                    return false;
                }
                if (!_slotMap[flatIndex].HasValue || _slotMap[flatIndex]!.Value != transaction.AmountDeltas[0].index)
                {
                    failure = InventoryFailures.Layout("Merge delta does not match the item at the specified section slot.");
                    return false;
                }

                mappedTransaction = transaction;
                return true;
            }

            failure = InventoryFailures.Layout("Transaction placement context can only target one added entry unless it is a mapped context.");
            return false;
        }

        foreach (var pair in sectionContext.AddedEntrySlots)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                failure = InventoryFailures.Layout("Mapped added entry index out of range.");
                return false;
            }
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (sectionContext.AddedEntrySlots.TryGetValue(i, out var mappedSlot))
            {
                var mappedContext = SectionedLayoutContext<TKey>.Single(mappedSlot.sectionId, mappedSlot.slotIndex);
                if (existingContext is SectionedLayoutContext<TKey> existingSectionContext &&
                    !existingSectionContext.IsMapped &&
                    (!string.Equals(existingSectionContext.SectionId, mappedSlot.sectionId, StringComparison.Ordinal) ||
                     existingSectionContext.SlotIndex != mappedSlot.slotIndex))
                {
                    failure = InventoryFailures.Layout("Transaction placement context conflicts with an added entry context.");
                    return false;
                }
                if (existingContext != null && existingContext is not SectionedLayoutContext<TKey>)
                {
                    failure = InventoryFailures.Layout("Invalid context type.");
                    return false;
                }
                added.Add((instance, mappedContext));
            }
            else
            {
                added.Add((instance, existingContext));
            }
        }

        mappedTransaction = transaction.WithAddedEntryContexts(
            added.ConvertAll(entry => entry.context));
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out InventoryFailure? failure)
    {
        if (context is SectionedLayoutContext<TKey> sectionContext && !sectionContext.IsMapped)
        {
            if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out int flatIndex))
            {
                failure = InventoryFailures.Layout("Section slot not found.");
                return false;
            }
            if (_slotMap[flatIndex].HasValue)
            {
                failure = InventoryFailures.Layout("Section slot already occupied.");
                return false;
            }
            if (!CanSectionAccept(inventory, _sections[_sectionIndices[sectionContext.SectionId]], instance.Definition))
            {
                failure = InventoryFailures.Layout("No compatible section slot available.");
                return false;
            }

            failure = null;
            return true;
        }

        if (context != null)
        {
            failure = InventoryFailures.Layout("Invalid context type.");
            return false;
        }

        if (FindFirstCompatibleEmptySlot(inventory, _slotMap, instance.Definition) < 0)
        {
            failure = InventoryFailures.Layout("No compatible section slot available.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? failure)
    {
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            failure = InventoryFailures.Layout("Invalid context type.");
            return false;
        }
        if (!TryGetFlatSlotIndex(fromContext.SectionId, fromContext.SlotIndex, out int fromSlot) ||
            !TryGetFlatSlotIndex(toContext.SectionId, toContext.SlotIndex, out int toSlot))
        {
            failure = InventoryFailures.Layout("Section slot not found.");
            return false;
        }
        if (fromSlot == toSlot)
        {
            failure = InventoryFailures.Layout("Cannot move item to itself.");
            return false;
        }
        if (!_slotMap[fromSlot].HasValue)
        {
            failure = InventoryFailures.Layout("Source section slot has no item.");
            return false;
        }
        if (_slotMap[toSlot].HasValue)
        {
            failure = InventoryFailures.Layout("Section slot already occupied.");
            return false;
        }

        var item = inventory.Items[_slotMap[fromSlot]!.Value];
        if (!CanSectionAccept(inventory, _sections[_sectionIndices[toContext.SectionId]], item.Definition))
        {
            failure = InventoryFailures.Layout("No compatible section slot available.");
            return false;
        }

        _slotMap[toSlot] = _slotMap[fromSlot];
        _slotMap[fromSlot] = null;
        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? failure)
    {
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            failure = InventoryFailures.Layout("Invalid context type.");
            return false;
        }
        if (!TryGetFlatSlotIndex(fromContext.SectionId, fromContext.SlotIndex, out int fromSlot) ||
            !TryGetFlatSlotIndex(toContext.SectionId, toContext.SlotIndex, out int toSlot))
        {
            failure = InventoryFailures.Layout("Section slot not found.");
            return false;
        }
        if (fromSlot == toSlot)
        {
            failure = InventoryFailures.Layout("Cannot swap item with itself.");
            return false;
        }
        if (!_slotMap[fromSlot].HasValue || !_slotMap[toSlot].HasValue)
        {
            failure = InventoryFailures.Layout("One or both of the section slots has no item.");
            return false;
        }

        var fromItem = inventory.Items[_slotMap[fromSlot]!.Value];
        var toItem = inventory.Items[_slotMap[toSlot]!.Value];
        if (!CanSectionAccept(inventory, _sections[_sectionIndices[fromContext.SectionId]], toItem.Definition) ||
            !CanSectionAccept(inventory, _sections[_sectionIndices[toContext.SectionId]], fromItem.Definition))
        {
            failure = InventoryFailures.Layout("No compatible section slot available.");
            return false;
        }

        var temp = _slotMap[fromSlot];
        _slotMap[fromSlot] = _slotMap[toSlot];
        _slotMap[toSlot] = temp;
        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySort(Inventory<TKey> inventory, IInventorySortContext<TKey> sortContext, out InventoryFailure? failure)
    {
        if (sortContext is not ItemSortContext<TKey> itemSortContext)
        {
            failure = InventoryFailures.Layout("Invalid sort context type.");
            return false;
        }

        var occupied = new List<(int storageIndex, int flatIndex)>();
        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i].HasValue)
                occupied.Add((_slotMap[i]!.Value, i));
        }

        occupied.Sort((a, b) =>
        {
            int comparison = itemSortContext.Comparer.Compare(inventory.Items[a.storageIndex], inventory.Items[b.storageIndex]);
            return comparison != 0 ? comparison : a.flatIndex.CompareTo(b.flatIndex);
        });

        var simulated = EmptyMap();
        foreach (var item in occupied)
        {
            int target = FindFirstCompatibleEmptySlot(inventory, simulated, inventory.Items[item.storageIndex].Definition);
            if (target < 0)
            {
                failure = InventoryFailures.Layout("No compatible section slot available.");
                return false;
            }

            simulated[target] = item.storageIndex;
        }

        _slotMap.Clear();
        _slotMap.AddRange(simulated);
        failure = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        int flatIndex;
        var instance = inventory.Items[index];
        if (context is SectionedLayoutContext<TKey> sectionContext && !sectionContext.IsMapped)
        {
            if (!TryGetFlatSlotIndex(sectionContext.SectionId, sectionContext.SlotIndex, out flatIndex))
                throw new InvalidOperationException("Section slot not found.");
        }
        else if (context == null)
        {
            flatIndex = FindFirstCompatibleEmptySlot(inventory, _slotMap, instance.Definition);
        }
        else
        {
            throw new InvalidOperationException("Invalid context type.");
        }

        if (flatIndex < 0)
            throw new InvalidOperationException("No compatible section slot available.");
        _slotMap[flatIndex] = index;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemRemoved(Inventory<TKey> inventory, int index)
    {
        ApplyRemovalToSlotMap(_slotMap, index);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        for (int i = 0; i < _slotMap.Count; i++)
            _slotMap[i] = null;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ILayoutPersistentData GetPersistentData()
    {
        return new SectionedLayoutPersistentData
        {
            SectionIds = _sections.Select(s => s.Id).ToList(),
            SectionSlotCounts = _sections.Select(s => s.SlotCount).ToList(),
            SlotMap = new List<int?>(_slotMap)
        };
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RestorePersistentData(ILayoutPersistentData? persistentData)
    {
        if (persistentData is not SectionedLayoutPersistentData data ||
            data.SectionIds == null ||
            data.SectionSlotCounts == null ||
            data.SlotMap == null ||
            data.SectionIds.Count != _sections.Count ||
            data.SectionSlotCounts.Count != _sections.Count ||
            data.SlotMap.Count != _slotMap.Count)
        {
            throw new InvalidOperationException("Invalid layout data");
        }

        for (int i = 0; i < _sections.Count; i++)
        {
            if (!string.Equals(data.SectionIds[i], _sections[i].Id, StringComparison.Ordinal) ||
                data.SectionSlotCounts[i] != _sections[i].SlotCount)
                throw new InvalidOperationException("Invalid layout data");
        }

        _slotMap.Clear();
        _slotMap.AddRange(data.SlotMap);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IInventoryLayout<TKey> Clone()
    {
        var clone = new SectionedLayout<TKey>(_sections);
        clone.RestorePersistentData(new SectionedLayoutPersistentData
        {
            SectionIds = _sections.Select(s => s.Id).ToList(),
            SectionSlotCounts = _sections.Select(s => s.SlotCount).ToList(),
            SlotMap = new List<int?>(_slotMap)
        });
        return clone;
    }

    private bool TryGetFlatSlotIndex(string sectionId, int slotIndex, out int flatIndex)
    {
        flatIndex = -1;
        if (!_sectionStartOffsets.TryGetValue(sectionId, out int offset) ||
            !_sectionIndices.TryGetValue(sectionId, out int sectionIndex))
            return false;
        if (slotIndex < 0 || slotIndex >= _sections[sectionIndex].SlotCount)
            return false;

        flatIndex = offset + slotIndex;
        return true;
    }

    private (string sectionId, int slotIndex) ToSectionSlot(int flatIndex)
    {
        for (int i = _sections.Count - 1; i >= 0; i--)
        {
            var section = _sections[i];
            int offset = _sectionStartOffsets[section.Id];
            if (flatIndex >= offset)
                return (section.Id, flatIndex - offset);
        }

        throw new ArgumentOutOfRangeException(nameof(flatIndex));
    }

    private bool CanSectionAccept(Inventory<TKey> inventory, SectionDefinition<TKey> section, ItemDefinition<TKey> definition)
    {
        var hasTags = section.RequiredTagIds.Count > 0;
        var hasDefinitions = section.AllowedDefinitionIdSet.Count > 0;

        if (!hasTags && !hasDefinitions)
            return true;

        if (hasTags && section.RequiredTagIds.All(tagId => inventory.Catalog.Satisfies(definition, inventory.Catalog.Tags.GetKey(tagId))))
            return true;

        if (hasDefinitions && section.AllowedDefinitionIdSet.Contains(definition.Id))
            return true;

        return false;
    }

    private int FindFirstCompatibleEmptySlot(Inventory<TKey> inventory, List<int?> map, ItemDefinition<TKey> definition)
    {
        for (int flatIndex = 0; flatIndex < map.Count; flatIndex++)
        {
            if (map[flatIndex].HasValue)
                continue;

            var (sectionId, _) = ToSectionSlot(flatIndex);
            if (CanSectionAccept(inventory, _sections[_sectionIndices[sectionId]], definition))
                return flatIndex;
        }

        return -1;
    }

    private List<int?> EmptyMap()
    {
        var map = new List<int?>(_slotMap.Count);
        for (int i = 0; i < _slotMap.Count; i++)
            map.Add(null);
        return map;
    }

    private static void ApplyRemovalToSlotMap(List<int?> slotMap, int removedIndex)
    {
        for (int i = 0; i < slotMap.Count; i++)
        {
            if (slotMap[i] == removedIndex)
                slotMap[i] = null;
            else if (slotMap[i] > removedIndex)
                slotMap[i]--;
        }
    }

    private static bool TryGetSingleContext(ILayoutContext<TKey> context, out SectionedLayoutContext<TKey> sectionContext)
    {
        if (context is SectionedLayoutContext<TKey> candidate && !candidate.IsMapped)
        {
            sectionContext = candidate;
            return true;
        }

        sectionContext = null!;
        return false;
    }

    private static bool TryCreateAddedCopy(
        InventoryTransaction<TKey> transaction,
        int addedIndex,
        ILayoutContext<TKey> context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out InventoryFailure? failure)
    {
        mappedTransaction = null;
        failure = null;
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (i == addedIndex)
            {
                if (existingContext is SectionedLayoutContext<TKey> existingSectionContext &&
                    context is SectionedLayoutContext<TKey> newSectionContext &&
                    !existingSectionContext.IsMapped &&
                    (!string.Equals(existingSectionContext.SectionId, newSectionContext.SectionId, StringComparison.Ordinal) ||
                     existingSectionContext.SlotIndex != newSectionContext.SlotIndex))
                {
                    failure = InventoryFailures.Layout("Transaction placement context conflicts with an added entry context.");
                    return false;
                }
                if (existingContext != null && existingContext is not SectionedLayoutContext<TKey>)
                {
                    failure = InventoryFailures.Layout("Invalid context type.");
                    return false;
                }
                added.Add((instance, context));
            }
            else
            {
                added.Add((instance, existingContext));
            }
        }

        mappedTransaction = transaction.WithAddedEntryContexts(
            added.ConvertAll(entry => entry.context));
        return true;
    }
}
