using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds an outgoing-only cross-inventory transfer from a source inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
/// <remarks>Successful take operations update only the builder simulation until the builder is transferred.</remarks>
public sealed class InventoryTransferBuilder<TKey>
{
    private readonly InventoryTransactionBuilder<TKey> _builder;

    internal InventoryTransferBuilder(Inventory<TKey> source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _builder = source.CreateTransactionBuilder();
    }

    /// <summary>
    /// Gets the source inventory items are planned to leave.
    /// </summary>
    public Inventory<TKey> Source { get; }

    /// <summary>
    /// Gets whether the builder contains no outgoing items.
    /// </summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Gets a snapshot of the outgoing entries currently planned by this builder.
    /// </summary>
    public IReadOnlyList<InventoryTransferEntry<TKey>> Entries => BuildEntries(ToSourceTransaction());

    /// <summary>
    /// Plans to take an amount from a source item instance.
    /// </summary>
    /// <param name="item">The source item instance to take from.</param>
    /// <param name="amount">The amount to take.</param>
    /// <param name="error">A consumer-facing reason when the take is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the take is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryTake(ItemInstance<TKey> item, int amount, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemove(item, out error, amount);
    }

    /// <summary>
    /// Plans to take an amount from the source item at a storage index.
    /// </summary>
    /// <param name="index">The source storage index to take from.</param>
    /// <param name="amount">The amount to take.</param>
    /// <param name="error">A consumer-facing reason when the take is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the take is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryTakeAtStorageIndex(int index, int amount, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemoveAtStorageIndex(index, out error, amount);
    }

    /// <summary>
    /// Plans to take an amount by item definition.
    /// </summary>
    /// <param name="definition">The definition to take.</param>
    /// <param name="amount">The amount to take.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="error">A consumer-facing reason when the take is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the take is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryTake(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out string? error)
    {
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        return _builder.TryRemoveByDefinition(definition, amount, ignoreMetadata, out error);
    }

    /// <summary>
    /// Creates a source removal transaction for the planned outgoing items.
    /// </summary>
    /// <returns>A transaction targeting the source inventory.</returns>
    public InventoryTransaction<TKey> ToSourceTransaction()
    {
        return _builder.ToInventoryTransaction();
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
