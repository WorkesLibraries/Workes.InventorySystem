using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a structural change to an inventory (deltas, removals, additions).
/// Transactions are formulated by the inventory and committed through inventory commit methods.
/// </summary>
/// <remarks>
/// Transactions do not represent layout move or swap operations. Use <see cref="Inventory{TKey}.Move"/> /
/// <see cref="Inventory{TKey}.TryMove"/> and <see cref="Inventory{TKey}.Swap"/> /
/// <see cref="Inventory{TKey}.TrySwap"/> for deliberate item movement.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class InventoryTransaction<TKey>
{
    /// <summary>
    /// Creates a transaction builder seeded with the current inventory state.
    /// </summary>
    /// <param name="inventory">The inventory the transaction will target.</param>
    /// <returns>A transaction builder for <paramref name="inventory"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is <see langword="null"/>.</exception>
    public static InventoryTransactionBuilder<TKey> From(Inventory<TKey> inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        var simulation = Inventory<TKey>.CreateSimulationClone(inventory);
        return new InventoryTransactionBuilder<TKey>(inventory, simulation);
    }

    /// <summary>
    /// Gets the inventory this transaction targets.
    /// </summary>
    public Inventory<TKey> Inventory { get; }

    /// <summary>
    /// Gets storage-index amount deltas to apply to existing item instances.
    /// </summary>
    public IReadOnlyList<(int index, int delta)> AmountDeltas { get; }

    /// <summary>
    /// Gets item instances to remove by storage index.
    /// </summary>
    public IReadOnlyList<(int index, ItemInstance<TKey> instance)> Removed { get; }

    /// <summary>
    /// Gets item instances to add with optional layout contexts.
    /// </summary>
    public IReadOnlyList<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> Added { get; }

    /// <summary>
    /// Gets whether this transaction has already been committed.
    /// </summary>
    public bool IsApplied { get; private set; }

    /// <summary>
    /// Gets whether this transaction contains no structural changes.
    /// </summary>
    public bool IsEmpty => AmountDeltas.Count == 0 && Removed.Count == 0 && Added.Count == 0;

    internal InventoryTransaction(
        Inventory<TKey> inventory,
        List<(int index, int delta)> amountDeltas,
        List<(int index, ItemInstance<TKey> instance)> removed,
        List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> added)
    {
        Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        AmountDeltas = amountDeltas ?? new List<(int, int)>();
        Removed = removed ?? new List<(int, ItemInstance<TKey>)>();
        Added = added ?? new List<(ItemInstance<TKey>, ILayoutContext<TKey>?)>();
    }

    internal void MarkApplied() => IsApplied = true;

    /// <summary>
    /// Creates an unapplied copy with replacement contexts for the existing added entries.
    /// </summary>
    /// <param name="contexts">
    /// Replacement contexts in the same order as <see cref="Added"/>. The count must match <see cref="Added"/> exactly.
    /// </param>
    /// <returns>
    /// A transaction that preserves the target inventory, amount deltas, removals, and added item instances while
    /// replacing only the added-entry contexts.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contexts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The number of supplied contexts does not match the number of added entries.
    /// </exception>
    /// <exception cref="InvalidOperationException">This transaction has already been applied.</exception>
    /// <remarks>
    /// Custom layouts can use this method from
    /// <see cref="IInventoryLayout{TKey}.TryApplyPlacementContext(Inventory{TKey}, InventoryTransaction{TKey}, ILayoutContext{TKey}?, out InventoryTransaction{TKey}?, out InventoryFailure?)"/>
    /// after validating a transaction-level placement context. It intentionally does not allow layouts to replace the
    /// transaction's structural item changes.
    /// </remarks>
    public InventoryTransaction<TKey> WithAddedEntryContexts(
        IReadOnlyList<ILayoutContext<TKey>?> contexts)
    {
        if (contexts == null)
            throw new ArgumentNullException(nameof(contexts));
        if (IsApplied)
            throw new InvalidOperationException("Applied transactions cannot be copied with replacement added-entry contexts.");
        if (contexts.Count != Added.Count)
        {
            throw new ArgumentException(
                $"Expected {Added.Count} added-entry contexts, but received {contexts.Count}.",
                nameof(contexts));
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>(Added.Count);
        for (int i = 0; i < Added.Count; i++)
            added.Add((Added[i].instance, contexts[i]));

        return new InventoryTransaction<TKey>(
            Inventory,
            new List<(int index, int delta)>(AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(Removed),
            added);
    }

    /// <summary>
    /// Creates a new transaction with the same structural data but targeting a different inventory.
    /// Used when committing a transaction built against a simulation to the real inventory.
    /// </summary>
    /// <param name="target">The inventory the copied transaction should target.</param>
    /// <returns>A transaction with copied structural data targeting <paramref name="target"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public InventoryTransaction<TKey> ForInventory(Inventory<TKey> target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        return new InventoryTransaction<TKey>(
            target,
            new List<(int, int)>(AmountDeltas),
            new List<(int, ItemInstance<TKey>)>(Removed),
            new List<(ItemInstance<TKey>, ILayoutContext<TKey>?)>(Added));
    }
}
