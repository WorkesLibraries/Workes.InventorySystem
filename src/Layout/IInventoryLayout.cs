using System.Collections.Generic;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Maps inventory storage indices to a placement model such as entries or fixed slots.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Layout implementations must keep their placement state synchronized when the inventory calls the item notification methods.
/// </remarks>
public interface IInventoryLayout<TKey>
{
    /// <summary>
    /// Gets the number of addressable positions exposed by the layout.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <returns>The number of positions known to the layout.</returns>
    int GetPositionCount(Inventory<TKey> inventory);

    /// <summary>
    /// Gets every layout context that can be refreshed or addressed by a UI.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <returns>All layout contexts addressable by this layout.</returns>
    /// <remarks>
    /// Fixed layouts return every possible position. Entry-style layouts return
    /// the current entry positions because the list has no empty addressable
    /// gaps.
    /// </remarks>
    IReadOnlyList<ILayoutContext<TKey>> GetAddressableContexts(Inventory<TKey> inventory);

    /// <summary>
    /// Gets the item at the specified layout context.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="context">The layout-specific context that identifies a position.</param>
    /// <returns>The item at the context, or <see langword="null"/> when the context is invalid or empty.</returns>
    ItemInstance<TKey>? GetItemAt(Inventory<TKey> inventory, ILayoutContext<TKey> context);

    /// <summary>
    /// Gets every layout context currently occupied by a storage index.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="storageIndex">The inventory storage index to resolve.</param>
    /// <returns>The layout contexts occupied by the storage index.</returns>
    /// <remarks>
    /// Single-position layouts return zero or one context. Multi-position
    /// layouts return every occupied position for the item.
    /// </remarks>
    IReadOnlyList<ILayoutContext<TKey>> GetContextsForStorageIndex(Inventory<TKey> inventory, int storageIndex);

    /// <summary>
    /// Gets the first layout context currently associated with a storage index.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="storageIndex">The inventory storage index to resolve.</param>
    /// <param name="context">The layout-specific context when the storage index is placed; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the storage index has a layout context; otherwise, <see langword="false"/>.</returns>
    bool TryGetContextForStorageIndex(Inventory<TKey> inventory, int storageIndex, out ILayoutContext<TKey>? context);

    /// <summary>
    /// Returns storage indices that should be considered for merging an added item into existing stacks.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="prototype">A representative item instance for the item being added.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns>Inventory storage indices that may be merge targets.</returns>
    IEnumerable<int> GetMergeCandidates(Inventory<TKey> inventory, ItemInstance<TKey> prototype, ILayoutContext<TKey>? context);

    /// <summary>
    /// Validates whether the layout can place the structural effects of a transaction.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="transaction">The structural transaction being validated.</param>
    /// <param name="error">A consumer-facing reason when placement is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the layout can satisfy placement; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Implementations must validate against the final transaction state: removals
    /// are applied before additions, amount deltas do not create new layout
    /// positions, and added-entry contexts are authoritative for new placements.
    /// </remarks>
    bool CanSatisfyPlacement(Inventory<TKey> inventory, InventoryTransaction<TKey> transaction, out string? error);

    /// <summary>
    /// Applies a transaction-level placement context to the transaction.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="transaction">The structural transaction to map.</param>
    /// <param name="context">Optional transaction-level placement context.</param>
    /// <param name="mappedTransaction">The mapped transaction when context application succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A consumer-facing reason when context application is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the context is valid for the transaction; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Mapping is layout-owned. Transaction-level mapping should target
    /// <see cref="InventoryTransaction{TKey}.Added"/> entry indices and must
    /// preserve amount deltas and removals exactly.
    /// </remarks>
    bool TryApplyPlacementContext(
        Inventory<TKey> inventory,
        InventoryTransaction<TKey> transaction,
        ILayoutContext<TKey>? context,
        out InventoryTransaction<TKey>? mappedTransaction,
        out string? error);

    /// <summary>
    /// Validates whether a new item instance can be placed.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="instance">The item instance being placed.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <param name="error">A consumer-facing reason when placement is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the instance can be placed; otherwise, <see langword="false"/>.</returns>
    bool CanAcceptNewItem(Inventory<TKey> inventory, ItemInstance<TKey> instance, ILayoutContext<TKey>? context, out string? error);

    /// <summary>
    /// Moves an item between two layout contexts.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="contextFrom">The source layout context.</param>
    /// <param name="contextTo">The destination layout context.</param>
    /// <param name="error">A consumer-facing reason when the move is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the move succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryMove(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error);

    /// <summary>
    /// Swaps two items between layout contexts.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="contextFrom">The first layout context.</param>
    /// <param name="contextTo">The second layout context.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the swap succeeds; otherwise, <see langword="false"/>.</returns>
    bool TrySwap(Inventory<TKey> inventory, ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error);

    /// <summary>
    /// Sorts the layout's placement state without mutating inventory storage order.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="comparer">The item comparer used for sorting placed items.</param>
    /// <param name="error">A consumer-facing reason when sorting is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when sorting succeeds; otherwise, <see langword="false"/>.</returns>
    bool TrySort(Inventory<TKey> inventory, IComparer<ItemInstance<TKey>> comparer, out string? error);

    /// <summary>
    /// Notifies the layout that the inventory added an item at the specified storage index.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="index">The storage index of the added item.</param>
    /// <param name="context">Optional layout-specific placement context for the added item.</param>
    void OnItemAdded(Inventory<TKey> inventory, int index, ILayoutContext<TKey>? context);

    /// <summary>
    /// Notifies the layout that the inventory removed an item at the specified storage index.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    /// <param name="index">The removed storage index.</param>
    void OnItemRemoved(Inventory<TKey> inventory, int index);

    /// <summary>
    /// Notifies the layout that the inventory contents were cleared.
    /// </summary>
    /// <param name="inventory">The inventory using this layout.</param>
    void OnInventoryCleared(Inventory<TKey> inventory);

    /// <summary>
    /// Captures layout-specific state that can be restored later.
    /// </summary>
    /// <returns>The persistent data for this layout.</returns>
    ILayoutPersistentData GetPersistentData();

    /// <summary>
    /// Restores layout-specific state previously produced by <see cref="GetPersistentData"/>.
    /// </summary>
    /// <param name="persistentData">The layout-specific data to restore.</param>
    void RestorePersistentData(ILayoutPersistentData? persistentData);

    /// <summary>
    /// Returns a new layout instance with the same state.
    /// </summary>
    /// <returns>A layout clone that does not share mutable placement state with the original.</returns>
    /// <remarks>Inventory transaction builders use this for simulation cloning.</remarks>
    IInventoryLayout<TKey> Clone();
}
