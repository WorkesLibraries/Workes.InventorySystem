using System.Collections.Generic;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Provides context to an application-plan selector for a candidate stack that may satisfy a remove operation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryDeltaRemovalCandidate<TKey>
{
    internal InventoryDeltaRemovalCandidate(
        Inventory<TKey> inventory,
        InventoryItemDeltaOperation<TKey> operation,
        ItemInstance<TKey> instance,
        int storageIndex,
        int plannedAmount,
        IReadOnlyList<ILayoutContext<TKey>> contexts)
    {
        Inventory = inventory;
        Operation = operation;
        Instance = instance;
        StorageIndex = storageIndex;
        PlannedAmount = plannedAmount;
        Contexts = contexts;
    }

    /// <summary>Gets the inventory the delta is being applied to.</summary>
    public Inventory<TKey> Inventory { get; }

    /// <summary>Gets the semantic remove operation being satisfied.</summary>
    public InventoryItemDeltaOperation<TKey> Operation { get; }

    /// <summary>Gets the candidate item stack.</summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>Gets the current storage index of the candidate stack.</summary>
    public int StorageIndex { get; }

    /// <summary>Gets the amount that would be removed from this candidate if it is accepted at this point.</summary>
    public int PlannedAmount { get; }

    /// <summary>Gets the current layout contexts for the candidate stack.</summary>
    public IReadOnlyList<ILayoutContext<TKey>> Contexts { get; }
}
