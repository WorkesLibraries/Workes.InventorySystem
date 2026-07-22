namespace Workes.InventorySystem.Core;

/// <summary>
/// Provides context to an application-plan selector for an added delta operation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryDeltaAdditionRequest<TKey>
{
    internal InventoryDeltaAdditionRequest(
        Inventory<TKey> inventory,
        InventoryItemDeltaOperation<TKey> operation)
    {
        Inventory = inventory;
        Operation = operation;
        Amount = operation.Amount;
    }

    /// <summary>Gets the inventory the delta is being applied to.</summary>
    public Inventory<TKey> Inventory { get; }

    /// <summary>Gets the semantic add operation being placed.</summary>
    public InventoryItemDeltaOperation<TKey> Operation { get; }

    /// <summary>Gets the amount requested by this add operation.</summary>
    public int Amount { get; }
}
