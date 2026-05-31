using System.Collections.Generic;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

public interface IInventoryLayout<TKey>
{
    int GetSlotCount(Inventory<TKey> inventory);

    ItemInstance<TKey>? GetAt(Inventory<TKey> inventory, ILayoutContext<TKey> context);

    int? GetSlotOfItem(ILayoutContext<TKey> context);

    IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context);

    bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, ILayoutContext<TKey>? context, out string? error);

    bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error);

    bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error);

    bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error);

    void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context);

    void OnItemRemoved(Inventory<TKey> inventory, int index);

    void OnInventoryCleared(Inventory<TKey> inventory);

    ILayoutPersistentData GetPersistentData();

    void RestorePersistentData(ILayoutPersistentData? persistentData);

    /// <summary>Returns a new layout instance with the same state. Used for simulation cloning.</summary>
    IInventoryLayout<TKey> Clone();
}
