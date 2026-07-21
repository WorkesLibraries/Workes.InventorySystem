using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Persistence;
using System.ComponentModel;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Fixed-size layout that places inventory items into numbered slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>Slot contexts must be <see cref="SlotLayoutContext{TKey}"/> instances. Invalid or empty slots return <see langword="null"/> from lookups.</remarks>
public class SlotLayout<TKey> : IParameterizedRepackableInventoryLayout<TKey>
{
    /// <inheritdoc />
    public virtual IInventoryLayoutSnapshotCodec<TKey> SnapshotCodec => SlotLayoutSnapshotCodec<TKey>.Instance;
    private readonly List<int?> _slotMap;
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("slotCount", typeof(int), "Number of addressable slots in the layout.")
        };

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
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayout(
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? error)
    {
        layout = new SlotLayout<TKey>(_slotMap.Count);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateEmptyRepackLayoutWithParameter(
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? error)
    {
        layout = null;
        if (!TryResolveSlotCount(parameterId, value, out int slotCount, out error))
            return false;

        layout = new SlotLayout<TKey>(slotCount);
        error = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? error)
    {
        layout = null;
        if (!TryResolveSlotCount(parameterId, value, out int slotCount, out error))
            return false;

        for (int slot = slotCount; slot < _slotMap.Count; slot++)
        {
            if (_slotMap[slot].HasValue)
            {
                error = "Cannot shrink slot layout because a removed slot is occupied.";
                return false;
            }
        }

        var newMap = new List<int?>(slotCount);
        for (int slot = 0; slot < slotCount; slot++)
            newMap.Add(slot < _slotMap.Count ? _slotMap[slot] : null);

        layout = new SlotLayout<TKey>(newMap);
        error = null;
        return true;
    }

    private static bool TryResolveSlotCount(
        string parameterId,
        object? value,
        out int slotCount,
        out InventoryFailure? error)
    {
        slotCount = 0;
        if (parameterId != "slotCount")
        {
            error = $"Parameter '{parameterId}' is not supported by SlotLayout.";
            return false;
        }

        if (value is not int resolvedSlotCount)
        {
            error = "Parameter 'slotCount' expects value type 'Int32'.";
            return false;
        }

        if (resolvedSlotCount <= 0)
        {
            error = "Slot count must be greater than zero.";
            return false;
        }

        slotCount = resolvedSlotCount;
        error = null;
        return true;
    }

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory) => _slotMap.Count;

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_slotMap.Count);
        for (int i = 0; i < _slotMap.Count; i++)
            contexts.Add(SlotLayoutContext<TKey>.Single(i));
        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
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
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i] == storageIndex)
                return new List<ILayoutContext<TKey>> { SlotLayoutContext<TKey>.Single(i) };
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out InventoryFailure? error)
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
            var (_, itemContext) = transaction.Added[addedIndex];
            int slot;

            if (itemContext is SlotLayoutContext<TKey> itemSlotContext)
            {
                if (itemSlotContext.IsMapped)
                {
                    error = "Invalid context type.";
                    return false;
                }

                slot = itemSlotContext.SlotIndex;
                if (slot < 0 || slot >= simulated.Count)
                {
                    error = "Slot index out of range.";
                    return false;
                }
                if (!explicitSlots.Add(slot))
                {
                    error = "Duplicate mapped target slot.";
                    return false;
                }
            }
            else if (itemContext == null)
            {
                slot = FindFirstAvailableSlot(simulated);
                if (slot < 0)
                {
                    error = "Not enough empty slots for new instances.";
                    return false;
                }
            }
            else
            {
                error = "Invalid context type.";
                return false;
            }

            if (simulated[slot].HasValue)
            {
                error = "Slot already occupied.";
                return false;
            }

            simulated[slot] = futureStorageIndex + addedIndex;
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
        out InventoryFailure? error)
    {
        mappedTransaction = null;
        error = null;

        if (context == null)
        {
            mappedTransaction = transaction;
            return true;
        }

        if (context is not SlotLayoutContext<TKey> slotContext)
        {
            error = "Invalid context type.";
            return false;
        }

        if (!slotContext.IsMapped)
        {
            if (transaction.Added.Count == 1)
            {
                if (!TryCreateAddedCopy(transaction, 0, slotContext, out mappedTransaction, out error))
                    return false;
                return true;
            }

            if (transaction.Added.Count == 0 && transaction.AmountDeltas.Count == 1 && transaction.AmountDeltas[0].delta > 0)
            {
                int slot = slotContext.SlotIndex;
                if (slot < 0 || slot >= _slotMap.Count)
                {
                    error = "Slot index out of range.";
                    return false;
                }
                if (!_slotMap[slot].HasValue || _slotMap[slot]!.Value != transaction.AmountDeltas[0].index)
                {
                    error = "Merge delta index does not match the item in the slot specified by context.";
                    return false;
                }

                mappedTransaction = transaction;
                return true;
            }

            error = "Transaction placement context can only target one added entry unless it is a mapped context.";
            return false;
        }

        var targetSlots = new HashSet<int>();
        foreach (var pair in slotContext.AddedEntrySlots)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                error = "Mapped added entry index out of range.";
                return false;
            }
            if (!targetSlots.Add(pair.Value))
            {
                error = "Duplicate mapped target slot.";
                return false;
            }
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (slotContext.AddedEntrySlots.TryGetValue(i, out int mappedSlot))
            {
                var mappedContext = SlotLayoutContext<TKey>.Single(mappedSlot);
                if (existingContext is SlotLayoutContext<TKey> existingSlotContext &&
                    !existingSlotContext.IsMapped &&
                    existingSlotContext.SlotIndex != mappedSlot)
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not SlotLayoutContext<TKey>)
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

        mappedTransaction = transaction.WithAddedEntryContexts(
            added.ConvertAll(entry => entry.context));
        return true;
    }

    private static bool TryCreateAddedCopy(
        InventoryTransaction<TKey> transaction,
        int addedIndex,
        ILayoutContext<TKey> context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out InventoryFailure? error)
    {
        mappedTransaction = null;
        error = null;
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int i = 0; i < transaction.Added.Count; i++)
        {
            var (instance, existingContext) = transaction.Added[i];
            if (i == addedIndex)
            {
                if (existingContext is SlotLayoutContext<TKey> existingSlotContext &&
                    context is SlotLayoutContext<TKey> newSlotContext &&
                    !existingSlotContext.IsMapped &&
                    existingSlotContext.SlotIndex != newSlotContext.SlotIndex)
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not SlotLayoutContext<TKey>)
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

        mappedTransaction = transaction.WithAddedEntryContexts(
            added.ConvertAll(entry => entry.context));
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out InventoryFailure? error)
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
        return FindFirstAvailableSlot(_slotMap);
    }

    private static int FindFirstAvailableSlot(List<int?> slotMap)
    {
        for (int i = 0; i < slotMap.Count; i++)
        {
            if (!slotMap[i].HasValue)
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

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? error)
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? error)
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TrySort(Inventory<TKey> inventory, IInventorySortContext<TKey> sortContext, out InventoryFailure? error)
    {
        if (sortContext is not ItemSortContext<TKey> itemSortContext)
        {
            error = "Invalid sort context type.";
            return false;
        }

        var occupied = new List<(int storageIndex, int slotIndex)>();
        for (int i = 0; i < _slotMap.Count; i++)
        {
            if (_slotMap[i].HasValue)
                occupied.Add((_slotMap[i]!.Value, i));
        }

        occupied.Sort((a, b) =>
        {
            int comparison = itemSortContext.Comparer.Compare(inventory.Items[a.storageIndex], inventory.Items[b.storageIndex]);
            return comparison != 0 ? comparison : a.slotIndex.CompareTo(b.slotIndex);
        });

        for (int i = 0; i < _slotMap.Count; i++)
            _slotMap[i] = i < occupied.Count ? occupied[i].storageIndex : null;

        error = null;
        return true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        int slot = context is SlotLayoutContext<TKey> slotContext && !slotContext.IsMapped
            ? slotContext.SlotIndex
            : FindFirstAvailableSlot();
        _slotMap[slot] = index;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnItemRemoved(Inventory<TKey> inventory, int removedIndex)
    {
        ApplyRemovalToSlotMap(_slotMap, removedIndex);
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
        return new SlotLayoutPersistentData
        {
            SlotMap = new List<int?>(_slotMap)
        };
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RestorePersistentData(ILayoutPersistentData? data)
    {
        if (data is not SlotLayoutPersistentData slotData)
            throw new InvalidOperationException("Invalid layout data");

        _slotMap.Clear();
        _slotMap.AddRange(slotData.SlotMap);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IInventoryLayout<TKey> Clone()
    {
        var data = (SlotLayoutPersistentData)GetPersistentData();
        return new SlotLayout<TKey>(new List<int?>(data.SlotMap));
    }
}
