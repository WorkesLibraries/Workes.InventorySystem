using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds an outgoing-only cross-inventory transfer from a source inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
/// <remarks>
/// Successful remove operations update only the builder simulation. Commit the staged transfer through the source
/// inventory with <see cref="Inventory{TKey}.TryCommitTransfer(InventoryTransferBuilder{TKey}, Inventory{TKey}, out string?)"/>.
/// </remarks>
public sealed class InventoryTransferBuilder<TKey>
{
    private readonly InventoryTransactionBuilder<TKey> _builder;

    internal InventoryTransferBuilder(Inventory<TKey> source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _builder = InventoryTransaction<TKey>.From(source);
    }

    /// <summary>
    /// Gets the source inventory whose items are planned to leave.
    /// </summary>
    public Inventory<TKey> Source { get; }

    /// <summary>
    /// Gets whether the builder contains no outgoing items.
    /// </summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Gets a snapshot of the outgoing entries currently planned by this builder.
    /// </summary>
    public IReadOnlyList<InventoryTransferEntry<TKey>> Entries => BuildEntries(BuildSourceTransaction());

    /// <summary>
    /// Plans to remove an amount from a source item instance for transfer.
    /// </summary>
    /// <param name="item">The source item instance to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(ItemInstance<TKey> item, int amount, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemove(item, out error, amount);
    }

    /// <summary>
    /// Plans to remove an amount from the source item at a storage index for transfer.
    /// </summary>
    /// <param name="index">The source storage index to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtStorageIndex(int index, int amount, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemoveAtStorageIndex(index, out error, amount);
    }

    /// <summary>
    /// Plans to remove an amount by item definition for transfer.
    /// </summary>
    /// <param name="definition">The definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemoveByDefinition(definition, amount, ignoreMetadata, out error);
    }

    internal InventoryTransaction<TKey> BuildSourceTransaction()
    {
        return _builder.Build();
    }

    internal static IReadOnlyList<InventoryTransferEntry<TKey>> BuildEntries(InventoryTransaction<TKey> transaction)
    {
        var entries = new List<InventoryTransferEntry<TKey>>();

        foreach (var (index, delta) in transaction.AmountDeltas)
        {
            if (delta >= 0)
                continue;

            var sourceInstance = transaction.Inventory.Items[index];
            entries.Add(new InventoryTransferEntry<TKey>(
                sourceInstance.Definition,
                -delta,
                CloneMetadataOrNull(sourceInstance.Metadata),
                sourceInstance));
        }

        foreach (var (_, instance) in transaction.Removed)
        {
            entries.Add(new InventoryTransferEntry<TKey>(
                instance.Definition,
                instance.Amount,
                CloneMetadataOrNull(instance.Metadata),
                instance));
        }

        return entries;
    }

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata metadata)
    {
        if (metadata == null || metadata.IsEmpty)
            return null;

        var clone = new InstanceMetadata();
        clone.RestoreMetadata(new Dictionary<string, object>(metadata.ToDictionary()));
        return clone;
    }
}
