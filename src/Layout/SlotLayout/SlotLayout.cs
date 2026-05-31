using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Fixed-size layout that places inventory items into numbered slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>Slot contexts must be <see cref="SlotLayoutContext{TKey}"/> instances. Invalid or empty slots return <see langword="null"/> from lookups.</remarks>
public class SlotLayout<TKey> : IInventoryLayout<TKey>
{
    private readonly List<int?> _slotMap;

    /// <summary>
    /// Creates a slot layout with a fixed number of slots.
    /// </summary>
    /// <param name="slotCount">The number of slots to create.</param>
    public SlotLayout(int slotCount)
    {
        _slotMap = new List<int?>(slotCount);
        for (int i = 0; i < slotCount; i++)
            _slotMap.Add(null);
    }

    /// <summary>
    /// Creates a slot layout from persistent slot-map context.
    /// </summary>
    /// <param name="persistentContext">A <see cref="List{T}"/> of nullable storage indices.</param>
    public SlotLayout(object? persistentContext)
    {
        _slotMap = (List<int?>)persistentContext!;
    }

    /// <inheritdoc />
    public int GetSlotCount(Inventory<TKey> inventory) => _slotMap.Count;

    /// <inheritdoc />
    public ItemInstance<TKey>? GetAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (context is not SlotLayoutContext<TKey> slotContext)
            return null;

        if (slotContext.SlotIndex < 0 || slotContext.SlotIndex >= _slotMap.Count)
            return null;

        var itemIndex = _slotMap[slotContext.SlotIndex];
        if (!itemIndex.HasValue)
            return null;

        return inventory.Items[itemIndex.Value];
    }

    /// <inheritdoc />
    public int? GetSlotOfItem(ILayoutContext<TKey> context)
    {
        if (context is not SlotLayoutContext<TKey> slotContext)
            return null;

        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i] == slotContext.SlotIndex)
                return i;
        }

        return null;
    }

    /// <inheritdoc />
    public IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context)
    {
        if (context is SlotLayoutContext<TKey> slotContext)
        {
            int slot = slotContext.SlotIndex;
            if (slot < 0 || slot >= _slotMap.Count)
                yield break;
            var itemIndex = _slotMap[slot];
            if (itemIndex.HasValue)
                yield return itemIndex.Value;
            yield break;
        }

        for (int slot = 0; slot < _slotMap.Count; slot++)
        {
            var itemIndex = _slotMap[slot];
            if (itemIndex.HasValue)
                yield return itemIndex.Value;
        }
    }

    /// <inheritdoc />
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;
        int newInstanceCount = transaction.Added.Count;
        int mergeDeltaCount = transaction.AmountDeltas.Count;

        if (context is SlotLayoutContext<TKey> slotContext)
        {
            int slot = slotContext.SlotIndex;
            if (slot < 0 || slot >= _slotMap.Count)
            {
                error = "Slot index out of range.";
                return false;
            }

            if (newInstanceCount > 0 && mergeDeltaCount > 0)
            {
                error = "With slot context cannot have both merge (delta) and new instance; only one action on the slot is allowed.";
                return false;
            }

            if (newInstanceCount > 1 || mergeDeltaCount > 1)
            {
                error = "With slot context only one new instance or merge delta can be placed.";
                return false;
            }

            if (newInstanceCount == 1)
            {
                if (_slotMap[slot].HasValue)
                {
                    error = "Slot already occupied.";
                    return false;
                }
                return true;
            }

            if (mergeDeltaCount == 1)
            {
                if (!_slotMap[slot].HasValue)
                {
                    error = "Merge delta targets a slot that has no item.";
                    return false;
                }
                int itemIndexInSlot = _slotMap[slot]!.Value;
                if (transaction.AmountDeltas[0].index != itemIndexInSlot)
                {
                    error = "Merge delta index does not match the item in the slot specified by context.";
                    return false;
                }
            }

            return true;
        }

        if (newInstanceCount <= 0)
            return true;

        int emptySlots = 0;
        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (!_slotMap[i].HasValue)
                emptySlots++;
        }

        if (newInstanceCount > emptySlots)
        {
            error = "Not enough empty slots for new instances.";
            return false;
        }

        foreach (var (_, itemContext) in transaction.Added)
        {

            if (itemContext is SlotLayoutContext<TKey> itemSlotContext)
            {
                int slot = itemSlotContext.SlotIndex;
                if (slot < 0 || slot >= _slotMap.Count)
                {
                    error = "Slot index out of range.";
                    return false;
                }
                if (_slotMap[slot].HasValue)
                {
                    error = "Slot already occupied.";
                    return false;
                }
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;

        int slot;
        if (context is SlotLayoutContext<TKey> slotContext)
        {
            slot = slotContext.SlotIndex;
            if (slot < 0 || slot >= _slotMap.Count)
            {
                error = "Slot index out of range.";
                return false;
            }
        }
        else
        {
            slot = FindFirstAvailableSlot();
            if (slot < 0)
            {
                error = "No available slot.";
                return false;
            }
        }

        if (_slotMap[slot].HasValue)
        {
            error = "Slot already occupied.";
            return false;
        }

        return true;
    }

    private int FindFirstAvailableSlot()
    {
        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (!_slotMap[i].HasValue)
                return i;
        }
        return -1;
    }

    /// <inheritdoc />
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        if (contextFrom is not SlotLayoutContext<TKey> slotContextFrom || contextTo is not SlotLayoutContext<TKey> slotContextTo)
        {
            error = "Invalid context type.";
            return false;
        }

        int fromSlot = slotContextFrom.SlotIndex;
        int toSlot = slotContextTo.SlotIndex;

        if (toSlot < 0 || toSlot >= _slotMap.Count || fromSlot < 0 || fromSlot >= _slotMap.Count)
        {
            error = "Slot index out of range.";
            return false;
        }

        if (toSlot == fromSlot)
        {
            error = "Cannot move item to the same slot.";
            return false;
        }

        if (!_slotMap[fromSlot].HasValue)
        {
            error = "Source slot has no item.";
            return false;
        }

        if (_slotMap[toSlot].HasValue)
        {
            error = "Target slot already occupied.";
            return false;
        }

        _slotMap[toSlot] = _slotMap[fromSlot];
        _slotMap[fromSlot] = null;

        return true;
    }

    /// <inheritdoc />
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        if (contextFrom is not SlotLayoutContext<TKey> slotContextFrom || contextTo is not SlotLayoutContext<TKey> slotContextTo)
        {
            error = "Invalid context type.";
            return false;
        }

        int fromSlot = slotContextFrom.SlotIndex;
        int toSlot = slotContextTo.SlotIndex;

        if (toSlot < 0 || toSlot >= _slotMap.Count || fromSlot < 0 || fromSlot >= _slotMap.Count)
        {
            error = "Slot index out of range.";
            return false;
        }

        if (toSlot == fromSlot)
        {
            error = "Cannot swap item with itself.";
            return false;
        }

        if (!_slotMap[fromSlot].HasValue || !_slotMap[toSlot].HasValue)
        {
            error = "One or both of the slots has no item.";
            return false;
        }

        var temp = _slotMap[fromSlot];
        _slotMap[fromSlot] = _slotMap[toSlot];
        _slotMap[toSlot] = temp;

        return true;
    }

    /// <inheritdoc />
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        int slot = context is SlotLayoutContext<TKey> slotContext
            ? slotContext.SlotIndex
            : FindFirstAvailableSlot();
        _slotMap[slot] = index;
    }

    /// <inheritdoc />
    public void OnItemRemoved(Inventory<TKey> inventory, int removedIndex)
    {
        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i] == removedIndex)
                _slotMap[i] = null;
            else if (_slotMap[i] > removedIndex)
                _slotMap[i]--;
        }
    }

    /// <inheritdoc />
    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        for (int i = 0; i < _slotMap.Count; i++)
            _slotMap[i] = null;
    }

    /// <inheritdoc />
    public ILayoutPersistentData GetPersistentData()
    {
        return new SlotLayoutPersistentData
        {
            SlotMap = new List<int?>(_slotMap)
        };
    }

    /// <inheritdoc />
    public void RestorePersistentData(ILayoutPersistentData? data)
    {
        if (data is not SlotLayoutPersistentData slotData)
            throw new InvalidOperationException("Invalid layout data");

        _slotMap.Clear();
        _slotMap.AddRange(slotData.SlotMap);
    }

    /// <inheritdoc />
    public IInventoryLayout<TKey> Clone()
    {
        var data = (SlotLayoutPersistentData)GetPersistentData();
        return new SlotLayout<TKey>(new List<int?>(data.SlotMap));
    }
}
