using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;
using System.ComponentModel;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Layout that places item instances into named equipment slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class EquipmentLayout<TKey> : IInventoryLayout<TKey>
{
    private readonly List<EquipmentSlot<TKey>> _slots;
    private readonly List<int?> _slotMap;
    private readonly Dictionary<string, int> _slotIndices;

    /// <summary>
    /// Gets the equipment slots in layout order.
    /// </summary>
    public IReadOnlyList<EquipmentSlot<TKey>> Slots => _slots;

    /// <summary>
    /// Creates an equipment layout with named equipment slots.
    /// </summary>
    /// <param name="slots">The equipment slots in layout order.</param>
    public EquipmentLayout(params EquipmentSlot<TKey>[] slots)
        : this((IEnumerable<EquipmentSlot<TKey>>)slots)
    {
    }

    /// <summary>
    /// Creates an equipment layout with named equipment slots.
    /// </summary>
    /// <param name="slots">The equipment slots in layout order.</param>
    /// <exception cref="ArgumentException">A slot id is duplicated or no slots are provided.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="slots"/> is <see langword="null"/>.</exception>
    public EquipmentLayout(IEnumerable<EquipmentSlot<TKey>> slots)
    {
        if (slots == null)
            throw new ArgumentNullException(nameof(slots));

        _slots = slots.ToList();
        if (_slots.Count == 0)
            throw new ArgumentException("Equipment layout must contain at least one slot.", nameof(slots));

        _slotIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slotIndices.TryAdd(_slots[i].Id, i))
                throw new ArgumentException("Equipment slot ids must be unique.", nameof(slots));
        }

        _slotMap = new List<int?>(_slots.Count);
        for (int i = 0; i < _slots.Count; i++)
            _slotMap.Add(null);
    }

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory) => _slots.Count;

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_slots.Count);
        foreach (var slot in _slots)
            contexts.Add(EquipmentLayoutContext<TKey>.Single(slot.Id));
        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (!TryGetSingleContext(context, out var equipmentContext))
            return null;
        if (!TryGetSlotIndex(equipmentContext.SlotId, out int slotIndex))
            return null;

        var storageIndex = _slotMap[slotIndex];
        if (!storageIndex.HasValue || storageIndex.Value < 0 || storageIndex.Value >= inventory.Items.Count)
            return null;

        return inventory.Items[storageIndex.Value];
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i] == storageIndex)
                return new List<ILayoutContext<TKey>> { EquipmentLayoutContext<TKey>.Single(_slots[i].Id) };
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
        if (context is EquipmentLayoutContext<TKey> equipmentContext && !equipmentContext.IsMapped)
        {
            if (!TryGetSlotIndex(equipmentContext.SlotId, out int slotIndex))
                yield break;

            var storageIndex = _slotMap[slotIndex];
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
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out string? error)
    {
        error = null;
        foreach (var (index, _) in transaction.AmountDeltas)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                error = "Index out of range.";
                return false;
            }
        }

        var removedIndices = new HashSet<int>();
        foreach (var (index, _) in transaction.Removed)
        {
            if (index < 0 || index >= inventory.Items.Count)
            {
                error = "Index out of range.";
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
            int slotIndex;
            if (itemContext is EquipmentLayoutContext<TKey> equipmentContext)
            {
                if (equipmentContext.IsMapped)
                {
                    error = "Invalid context type.";
                    return false;
                }
                if (!TryGetSlotIndex(equipmentContext.SlotId, out slotIndex))
                {
                    error = "Equipment slot not found.";
                    return false;
                }
                if (!explicitSlots.Add(slotIndex))
                {
                    error = "Duplicate mapped target equipment slot.";
                    return false;
                }
                if (!CanSlotAccept(inventory, _slots[slotIndex], instance.Definition))
                {
                    error = "No compatible equipment slot available.";
                    return false;
                }
            }
            else if (itemContext == null)
            {
                slotIndex = FindFirstCompatibleEmptySlot(inventory, simulated, instance.Definition);
                if (slotIndex < 0)
                {
                    error = "No compatible equipment slot available.";
                    return false;
                }
            }
            else
            {
                error = "Invalid context type.";
                return false;
            }

            if (simulated[slotIndex].HasValue)
            {
                error = "Equipment slot already occupied.";
                return false;
            }

            simulated[slotIndex] = futureStorageIndex + addedIndex;
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
        out string? error)
    {
        mappedTransaction = null;
        error = null;
        if (context == null)
        {
            mappedTransaction = transaction;
            return true;
        }

        if (context is not EquipmentLayoutContext<TKey> equipmentContext)
        {
            error = "Invalid context type.";
            return false;
        }

        if (!equipmentContext.IsMapped)
        {
            if (transaction.Added.Count == 1)
                return TryCreateAddedCopy(transaction, 0, equipmentContext, out mappedTransaction, out error);

            if (transaction.Added.Count == 0 && transaction.AmountDeltas.Count == 1 && transaction.AmountDeltas[0].delta > 0)
            {
                if (!TryGetSlotIndex(equipmentContext.SlotId, out int slotIndex))
                {
                    error = "Equipment slot not found.";
                    return false;
                }
                if (!_slotMap[slotIndex].HasValue || _slotMap[slotIndex]!.Value != transaction.AmountDeltas[0].index)
                {
                    error = "Merge delta does not match the item at the specified equipment slot.";
                    return false;
                }

                mappedTransaction = transaction;
                return true;
            }

            error = "Transaction placement context can only target one added entry unless it is a mapped context.";
            return false;
        }

        var targetSlots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in equipmentContext.AddedEntrySlots)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                error = "Mapped added entry index out of range.";
                return false;
            }
            if (!targetSlots.Add(pair.Value))
            {
                error = "Duplicate mapped target equipment slot.";
                return false;
            }
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (equipmentContext.AddedEntrySlots.TryGetValue(i, out string? mappedSlot))
            {
                var mappedContext = EquipmentLayoutContext<TKey>.Single(mappedSlot);
                if (existingContext is EquipmentLayoutContext<TKey> existingEquipmentContext &&
                    !existingEquipmentContext.IsMapped &&
                    !string.Equals(existingEquipmentContext.SlotId, mappedSlot, StringComparison.Ordinal))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not EquipmentLayoutContext<TKey>)
                {
                    error = "Invalid context type.";
                    return false;
                }
                added.Add((instance, mappedContext));
            }
            else
            {
                added.Add((instance, existingContext));
            }
        }

        mappedTransaction = new InventoryTransaction<TKey>(
            transaction.Inventory,
            new List<(int index, int delta)>(transaction.AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed),
            added);
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error)
    {
        if (context is EquipmentLayoutContext<TKey> equipmentContext && !equipmentContext.IsMapped)
        {
            if (!TryGetSlotIndex(equipmentContext.SlotId, out int slotIndex))
            {
                error = "Equipment slot not found.";
                return false;
            }
            if (_slotMap[slotIndex].HasValue)
            {
                error = "Equipment slot already occupied.";
                return false;
            }
            if (!CanSlotAccept(inventory, _slots[slotIndex], instance.Definition))
            {
                error = "No compatible equipment slot available.";
                return false;
            }

            error = null;
            return true;
        }

        if (context != null)
        {
            error = "Invalid context type.";
            return false;
        }

        if (FindFirstCompatibleEmptySlot(inventory, _slotMap, instance.Definition) < 0)
        {
            error = "No compatible equipment slot available.";
            return false;
        }

        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            error = "Invalid context type.";
            return false;
        }
        if (!TryGetSlotIndex(fromContext.SlotId, out int fromSlot) || !TryGetSlotIndex(toContext.SlotId, out int toSlot))
        {
            error = "Equipment slot not found.";
            return false;
        }
        if (fromSlot == toSlot)
        {
            error = "Cannot move item to itself.";
            return false;
        }
        if (!_slotMap[fromSlot].HasValue)
        {
            error = "Source equipment slot has no item.";
            return false;
        }
        if (_slotMap[toSlot].HasValue)
        {
            error = "Equipment slot already occupied.";
            return false;
        }

        var item = inventory.Items[_slotMap[fromSlot]!.Value];
        if (!CanSlotAccept(inventory, _slots[toSlot], item.Definition))
        {
            error = "No compatible equipment slot available.";
            return false;
        }

        _slotMap[toSlot] = _slotMap[fromSlot];
        _slotMap[fromSlot] = null;
        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        if (!TryGetSingleContext(contextFrom, out var fromContext) || !TryGetSingleContext(contextTo, out var toContext))
        {
            error = "Invalid context type.";
            return false;
        }
        if (!TryGetSlotIndex(fromContext.SlotId, out int fromSlot) || !TryGetSlotIndex(toContext.SlotId, out int toSlot))
        {
            error = "Equipment slot not found.";
            return false;
        }
        if (fromSlot == toSlot)
        {
            error = "Cannot swap item with itself.";
            return false;
        }
        if (!_slotMap[fromSlot].HasValue || !_slotMap[toSlot].HasValue)
        {
            error = "One or both of the equipment slots has no item.";
            return false;
        }

        var fromItem = inventory.Items[_slotMap[fromSlot]!.Value];
        var toItem = inventory.Items[_slotMap[toSlot]!.Value];
        if (!CanSlotAccept(inventory, _slots[fromSlot], toItem.Definition) ||
            !CanSlotAccept(inventory, _slots[toSlot], fromItem.Definition))
        {
            error = "No compatible equipment slot available.";
            return false;
        }

        var temp = _slotMap[fromSlot];
        _slotMap[fromSlot] = _slotMap[toSlot];
        _slotMap[toSlot] = temp;
        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySort(Inventory<TKey> inventory, IInventorySortContext<TKey> sortContext, out string? error)
    {
        error = "Layout does not support sorting.";
        return false;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        int slotIndex;
        var instance = inventory.Items[index];
        if (context is EquipmentLayoutContext<TKey> equipmentContext && !equipmentContext.IsMapped)
        {
            if (!TryGetSlotIndex(equipmentContext.SlotId, out slotIndex))
                throw new InvalidOperationException("Equipment slot not found.");
        }
        else if (context == null)
        {
            slotIndex = FindFirstCompatibleEmptySlot(inventory, _slotMap, instance.Definition);
        }
        else
        {
            throw new InvalidOperationException("Invalid context type.");
        }

        if (slotIndex < 0)
            throw new InvalidOperationException("No compatible equipment slot available.");
        _slotMap[slotIndex] = index;
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
        return new EquipmentLayoutPersistentData
        {
            SlotIds = _slots.Select(s => s.Id).ToList(),
            SlotMap = new List<int?>(_slotMap)
        };
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RestorePersistentData(ILayoutPersistentData? persistentData)
    {
        if (persistentData is not EquipmentLayoutPersistentData equipmentData ||
            equipmentData.SlotIds == null ||
            equipmentData.SlotMap == null ||
            equipmentData.SlotIds.Count != _slots.Count ||
            equipmentData.SlotMap.Count != _slotMap.Count)
        {
            throw new InvalidOperationException("Invalid layout data");
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            if (!string.Equals(equipmentData.SlotIds[i], _slots[i].Id, StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid layout data");
        }

        _slotMap.Clear();
        _slotMap.AddRange(equipmentData.SlotMap);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IInventoryLayout<TKey> Clone()
    {
        var clone = new EquipmentLayout<TKey>(_slots);
        clone.RestorePersistentData(new EquipmentLayoutPersistentData
        {
            SlotIds = _slots.Select(s => s.Id).ToList(),
            SlotMap = new List<int?>(_slotMap)
        });
        return clone;
    }

    private bool TryGetSlotIndex(string slotId, out int slotIndex)
    {
        if (slotId == null)
        {
            slotIndex = -1;
            return false;
        }

        return _slotIndices.TryGetValue(slotId, out slotIndex);
    }

    private bool CanSlotAccept(Inventory<TKey> inventory, EquipmentSlot<TKey> slot, ItemDefinition<TKey> definition)
    {
        foreach (var tag in slot.RequiredTagKeys)
        {
            if (!inventory.Catalog.Satisfies(definition, tag))
                return false;
        }

        return true;
    }

    private int FindFirstCompatibleEmptySlot(Inventory<TKey> inventory, List<int?> map, ItemDefinition<TKey> definition)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!map[i].HasValue && CanSlotAccept(inventory, _slots[i], definition))
                return i;
        }

        return -1;
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

    private static bool TryGetSingleContext(ILayoutContext<TKey> context, out EquipmentLayoutContext<TKey> equipmentContext)
    {
        if (context is EquipmentLayoutContext<TKey> candidate && !candidate.IsMapped)
        {
            equipmentContext = candidate;
            return true;
        }

        equipmentContext = null!;
        return false;
    }

    private static bool TryCreateAddedCopy(
        InventoryTransaction<TKey> transaction,
        int addedIndex,
        ILayoutContext<TKey> context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out string? error)
    {
        mappedTransaction = null;
        error = null;
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (i == addedIndex)
            {
                if (existingContext is EquipmentLayoutContext<TKey> existingEquipmentContext &&
                    context is EquipmentLayoutContext<TKey> newEquipmentContext &&
                    !existingEquipmentContext.IsMapped &&
                    !string.Equals(existingEquipmentContext.SlotId, newEquipmentContext.SlotId, StringComparison.Ordinal))
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not EquipmentLayoutContext<TKey>)
                {
                    error = "Invalid context type.";
                    return false;
                }
                added.Add((instance, context));
            }
            else
            {
                added.Add((instance, existingContext));
            }
        }

        mappedTransaction = new InventoryTransaction<TKey>(
            transaction.Inventory,
            new List<(int index, int delta)>(transaction.AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed),
            added);
        return true;
    }
}
