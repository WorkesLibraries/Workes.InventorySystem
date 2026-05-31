using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a structural change to an inventory (deltas, removals, additions).
/// Transactions are formulated by the inventory and committed via <see cref="Inventory{TKey}.CommitTransaction"/>.
/// </summary>
public class InventoryTransaction<TKey>
{
    public Inventory<TKey> Inventory { get; }
    public IReadOnlyList<(int index, int delta)> AmountDeltas { get; }
    public IReadOnlyList<(int index, ItemInstance<TKey> instance)> Removed { get; }
    public IReadOnlyList<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> Added { get; }
    public bool IsApplied { get; private set; }

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
    /// Creates a new transaction with the same structural data but targeting a different inventory.
    /// Used when committing a transaction built against a simulation to the real inventory.
    /// </summary>
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
