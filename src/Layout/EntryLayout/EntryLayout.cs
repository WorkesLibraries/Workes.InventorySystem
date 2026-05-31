using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Entry-based layout that treats the underlying inventory items as an unordered bag.
/// Structure (ordering) is tracked via an internal index mapping and never by mutating
/// the inventory's <c>_items</c> list.
/// </summary>
public class EntryLayout<TKey> : IInventoryLayout<TKey>
{
    private readonly List<int> _order = new();

    public int GetSlotCount(Inventory<TKey> inventory)
    {
        return _order.Count;
    }

    public ItemInstance<TKey>? GetAt(Inventory<TKey> inventory, ILayoutContext<TKey> context)
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

    public int? GetSlotOfItem(ILayoutContext<TKey> context)
    {
        if (context is not EntryLayoutContext<TKey> entryContext)
            return null;

        for (int i = 0; i < _order.Count; i++)
        {
            if (_order[i] == entryContext.TargetIndex)
                return i;
        }

        return null;
    }

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

    public bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;
        int newInstanceCount = transaction.Added.Count;
        int mergeDeltaCount = transaction.AmountDeltas.Count;

        // Entry layout has no fixed capacity; without context we always allow placement.
        if (context is not EntryLayoutContext<TKey> entryContext)
            return true;

        // With entry context we conceptually operate on a single structural index,
        // so only a single structural change (one add or one merge delta) is allowed.
        if (newInstanceCount + mergeDeltaCount > 1)
        {
            if (newInstanceCount == 0 && mergeDeltaCount == 0)
            {
                error = "The inventory transaction contains no structural changes.";
                return false;
            }
            error = "With entry layout context only one structural change is allowed.";
            return false;
        }

        // If this is a merge-only change, ensure the delta targets the item currently
        // at the specified entry position.
        if (mergeDeltaCount == 1)
        {
            int targetPos = entryContext.TargetIndex;
            if (targetPos < 0 || targetPos >= _order.Count)
            {
                error = "Target index out of range.";
                return false;
            }

            int itemIndex = _order[targetPos];
            var (index, _) = transaction.AmountDeltas[0];
            if (index != itemIndex)
            {
                error = "Merge delta does not match the item at the specified entry index.";
                return false;
            }
        }

        // For add-only with context, there is no structural capacity limit: the entry
        // list can always grow; placement details are handled in OnItemAdded.
        return true;
    }

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

    public void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context)
    {
        if (context is EntryLayoutContext<TKey> entryContext)
        {
            _order.Insert(entryContext.TargetIndex, index);
            return;
        }

        _order.Add(index);
    }

    public void OnItemRemoved(Inventory<TKey> inventory, int index)
    {
        for (int i = 0; i < _order.Count; i++)
        {
            if (_order[i] == index)
            {
                _order.RemoveAt(i);
                i--;
            }
            else if (_order[i] > index)
            {
                _order[i]--;
            }
        }
    }

    public void OnInventoryCleared(Inventory<TKey> inventory)
    {
        _order.Clear();
    }

    public ILayoutPersistentData GetPersistentData() => new EntryLayoutPersistentData { Order = new List<int>(_order) };

    public void RestorePersistentData(ILayoutPersistentData? data)
    {
        if (data is not EntryLayoutPersistentData entryData)
            throw new InvalidOperationException("Invalid layout data");

        _order.Clear();
        _order.AddRange(entryData.Order);
    }

    public IInventoryLayout<TKey> Clone()
    {
        var data = (EntryLayoutPersistentData)GetPersistentData();
        var clone = new EntryLayout<TKey>();
        clone.RestorePersistentData(new EntryLayoutPersistentData { Order = new List<int>(data.Order) });
        return clone;
    }
}
