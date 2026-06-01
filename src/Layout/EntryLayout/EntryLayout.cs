using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Sorting;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Entry-based layout that treats the underlying inventory items as an unordered bag.
/// Structure (ordering) is tracked via an internal index mapping and never by mutating
/// the inventory's <c>_items</c> list.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class EntryLayout<TKey> : IInventoryLayout<TKey>
{
    private readonly List<int> _order = new();

    /// <inheritdoc />
    public int GetPositionCount(Inventory<TKey> inventory)
    {
        return _order.Count;
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory)
    {
        var contexts = new List<ILayoutContext<TKey>>(_order.Count);
        for (int i = 0; i < _order.Count; i++)
            contexts.Add(EntryLayoutContext<TKey>.Single(i));
        return contexts;
    }

    /// <inheritdoc />
    public ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
    {
        if (context is not EntryLayoutContext<TKey> entryContext)
            return null;

        if (entryContext.TargetIndex < 0 || entryContext.TargetIndex >= _order.Count)
            return null;

        int itemIndex = _order[entryContext.TargetIndex];
        if (itemIndex < 0 || itemIndex >= inventory.Items.Count)
            return null;

        return inventory.Items[itemIndex];
    }

    /// <inheritdoc />
    public IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= inventory.Items.Count)
            return Array.Empty<ILayoutContext<TKey>>();

        for (int i = 0; i < _order.Count; i++)
        {
            if (_order[i] == storageIndex)
                return new List<ILayoutContext<TKey>> { EntryLayoutContext<TKey>.Single(i) };
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
    public IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context)
    {
        // If we have an entry layout context, the only valid merge candidate is the
        // item currently stored at that structural position.
        if (context is EntryLayoutContext<TKey> entryContext)
        {
            int targetPos = entryContext.TargetIndex;
            if (targetPos < 0 || targetPos >= _order.Count)
                yield break;

            int itemIndex = _order[targetPos];
            if (itemIndex < 0 || itemIndex >= inventory.Items.Count)
                yield break;

            yield return itemIndex;
            yield break;
        }

        // No specific context: all items in the current structural order are candidates.
        for (int i = 0; i < _order.Count; i++)
            yield return _order[i];
    }

    /// <inheritdoc />
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

        var simulated = new List<int>(_order);
        var removed = new List<int>(removedIndices);
        removed.Sort((a, b) => b.CompareTo(a));
        foreach (int removedIndex in removed)
            ApplyRemovalToOrder(simulated, removedIndex);

        int futureStorageIndex = inventory.Items.Count - removed.Count;
        for (int addedIndex = 0; addedIndex < transaction.Added.Count; addedIndex++)
        {
            var (_, itemContext) = transaction.Added[addedIndex];
            int targetIndex;
            if (itemContext is EntryLayoutContext<TKey> entryContext)
            {
                if (entryContext.IsMapped)
                {
                    error = "Invalid context type.";
                    return false;
                }

                targetIndex = entryContext.TargetIndex;
                if (targetIndex < 0 || targetIndex > simulated.Count)
                {
                    error = "Target index out of range.";
                    return false;
                }
            }
            else if (itemContext == null)
            {
                targetIndex = simulated.Count;
            }
            else
            {
                error = "Invalid context type.";
                return false;
            }

            simulated.Insert(targetIndex, futureStorageIndex + addedIndex);
        }

        return true;
    }

    /// <inheritdoc />
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

        if (context is not EntryLayoutContext<TKey> entryContext)
        {
            error = "Invalid context type.";
            return false;
        }

        if (!entryContext.IsMapped)
        {
            if (transaction.Added.Count == 1)
            {
                if (!TryCreateAddedCopy(transaction, 0, entryContext, out mappedTransaction, out error))
                    return false;
                return true;
            }

            if (transaction.Added.Count == 0 && transaction.AmountDeltas.Count == 1 && transaction.AmountDeltas[0].delta > 0)
            {
                int targetPos = entryContext.TargetIndex;
                if (targetPos < 0 || targetPos >= _order.Count)
                {
                    error = "Target index out of range.";
                    return false;
                }

                int itemIndex = _order[targetPos];
                if (transaction.AmountDeltas[0].index != itemIndex)
                {
                    error = "Merge delta does not match the item at the specified entry index.";
                    return false;
                }

                mappedTransaction = transaction;
                return true;
            }

            error = "Transaction placement context can only target one added entry unless it is a mapped context.";
            return false;
        }

        foreach (var pair in entryContext.AddedEntryTargetIndices)
        {
            if (pair.Key < 0 || pair.Key >= transaction.Added.Count)
            {
                error = "Mapped added entry index out of range.";
                return false;
            }
        }

        var mappedEntries = new List<(int addedIndex, int targetIndex)>();
        foreach (var pair in entryContext.AddedEntryTargetIndices)
            mappedEntries.Add((pair.Key, pair.Value));
        mappedEntries.Sort((a, b) =>
        {
            int targetComparison = a.targetIndex.CompareTo(b.targetIndex);
            return targetComparison != 0 ? targetComparison : a.addedIndex.CompareTo(b.addedIndex);
        });

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        var mappedIndices = new HashSet<int>();
        for (int i = 0; i < mappedEntries.Count; i++)
        {
            var (addedIndex, targetIndex) = mappedEntries[i];
            var (instance, existingContext) = transaction.Added[addedIndex];
            int adjustedTargetIndex = targetIndex + CountPriorMappedInsertions(mappedEntries, i, targetIndex);
            var mappedContext = EntryLayoutContext<TKey>.Single(adjustedTargetIndex);
            if (existingContext is EntryLayoutContext<TKey> existingEntryContext &&
                !existingEntryContext.IsMapped &&
                existingEntryContext.TargetIndex != adjustedTargetIndex)
            {
                error = "Transaction placement context conflicts with an added entry context.";
                return false;
            }
            if (existingContext != null && existingContext is not EntryLayoutContext<TKey>)
            {
                error = "Invalid context type.";
                return false;
            }
            added.Add((instance, mappedContext));
            mappedIndices.Add(addedIndex);
        }

        for (int i = 0; i < transaction.Added.Count; i++)
        {
            if (mappedIndices.Contains(i))
                continue;
            added.Add(transaction.Added[i]);
        }

        mappedTransaction = new InventoryTransaction<TKey>(
            transaction.Inventory,
            new List<(int index, int delta)>(transaction.AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed),
            added);
        return true;
    }

    private static int CountPriorMappedInsertions(List<(int addedIndex, int targetIndex)> mappedEntries, int currentPosition, int targetIndex)
    {
        int count = 0;
        for (int i = 0; i < currentPosition; i++)
        {
            if (mappedEntries[i].targetIndex <= targetIndex)
                count++;
        }
        return count;
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
                if (existingContext is EntryLayoutContext<TKey> existingEntryContext &&
                    context is EntryLayoutContext<TKey> newEntryContext &&
                    !existingEntryContext.IsMapped &&
                    existingEntryContext.TargetIndex != newEntryContext.TargetIndex)
                {
                    error = "Transaction placement context conflicts with an added entry context.";
                    return false;
                }
                if (existingContext != null && existingContext is not EntryLayoutContext<TKey>)
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

    /// <inheritdoc />
    public bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;
        // Entry layout has no capacity limit, but if a context is provided we validate
        // that the requested target index is sensible for this layout.
        if (context is EntryLayoutContext<TKey> entryContext)
        {
            int targetPos = entryContext.TargetIndex;
            if (targetPos < 0 || targetPos > _order.Count)
            {
                error = "Target index out of range.";
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        if (contextTo is not EntryLayoutContext<TKey> entryContextTo || contextFrom is not EntryLayoutContext<TKey> entryContextFrom)
        {
            error = "Invalid context type.";
            return false;
        }

        int fromPos = entryContextFrom.TargetIndex;
        int targetPos = entryContextTo.TargetIndex;

        if (fromPos < 0 || fromPos >= _order.Count || targetPos < 0 || targetPos >= _order.Count)
        {
            error = "Invalid position.";
            return false;
        }

        if (targetPos == fromPos)
        {
            error = "Cannot move item to the same position.";
            return false;
        }

        int storageIndex = _order[fromPos];
        _order.RemoveAt(fromPos);
        if (targetPos > fromPos)
            targetPos--;
        _order.Insert(targetPos, storageIndex);

        return true;
    }

    /// <inheritdoc />
    public bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        if (contextTo is not EntryLayoutContext<TKey> entryContextTo || contextFrom is not EntryLayoutContext<TKey> entryContextFrom)
        {
            error = "Invalid context type.";
            return false;
        }

        int fromPos = entryContextFrom.TargetIndex;
        int targetPos = entryContextTo.TargetIndex;

        if (fromPos < 0 || fromPos >= _order.Count || targetPos < 0 || targetPos >= _order.Count)
        {
            error = "Invalid position.";
            return false;
        }

        if (fromPos == targetPos)
        {
            error = "Cannot swap item with itself.";
            return false;
        }

        var temp = _order[fromPos];
        _order[fromPos] = _order[targetPos];
        _order[targetPos] = temp;

        return true;
    }

    /// <inheritdoc />
    public bool TrySort(Inventory<TKey> inventory, IInventorySortContext<TKey> sortContext, out string? error)
    {
        if (sortContext is not ItemSortContext<TKey> itemSortContext)
        {
            error = "Invalid sort context type.";
            return false;
        }

        var indexed = new List<(int storageIndex, int orderIndex)>();
        for (int i = 0; i < _order.Count; i++)
            indexed.Add((_order[i], i));

        indexed.Sort((a, b) =>
        {
            int comparison = itemSortContext.Comparer.Compare(inventory.Items[a.storageIndex], inventory.Items[b.storageIndex]);
            return comparison != 0 ? comparison : a.orderIndex.CompareTo(b.orderIndex);
        });

        _order.Clear();
        foreach (var entry in indexed)
            _order.Add(entry.storageIndex);

        error = null;
        return true;
    }

    /// <inheritdoc />
    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        if (context is EntryLayoutContext<TKey> entryContext && !entryContext.IsMapped)
        {
            _order.Insert(entryContext.TargetIndex, index);
            return;
        }

        _order.Add(index);
    }

    /// <inheritdoc />
    public void OnItemRemoved(Inventory<TKey> inventory, int index)
    {
        ApplyRemovalToOrder(_order, index);
    }

    private static void ApplyRemovalToOrder(List<int> order, int index)
    {
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == index)
            {
                order.RemoveAt(i);
                i--;
            }
            else if (order[i] > index)
            {
                order[i]--;
            }
        }
    }

    /// <inheritdoc />
    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        _order.Clear();
    }

    /// <inheritdoc />
    public ILayoutPersistentData GetPersistentData() => new EntryLayoutPersistentData { Order = new List<int>(_order) };

    /// <inheritdoc />
    public void RestorePersistentData(ILayoutPersistentData? data)
    {
        if (data is not EntryLayoutPersistentData entryData)
            throw new InvalidOperationException("Invalid layout data");

        _order.Clear();
        _order.AddRange(entryData.Order);
    }

    /// <inheritdoc />
    public IInventoryLayout<TKey> Clone()
    {
        var data = (EntryLayoutPersistentData)GetPersistentData();
        var clone = new EntryLayout<TKey>();
        clone.RestorePersistentData(new EntryLayoutPersistentData { Order = new List<int>(data.Order) });
        return clone;
    }
}
