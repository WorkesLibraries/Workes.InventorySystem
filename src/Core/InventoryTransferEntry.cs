using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes one item amount planned for cross-inventory transfer.
/// </summary>
/// <remarks>
/// Transfer entries are inspection results produced by transfer builders and transfer internals. Callers should stage
/// transfers through <see cref="InventoryTransfer.From{TKey}(Inventory{TKey})"/> rather than constructing entries directly.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
[Obsolete("InventoryTransferEntry is retained for backwards compatibility with deprecated transfer builders. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging.")]
public sealed class InventoryTransferEntry<TKey>
{
    /// <summary>
    /// Gets the item definition to transfer.
    /// </summary>
    public ItemDefinition<TKey> Definition { get; }

    /// <summary>
    /// Gets the amount to transfer.
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// Gets a shallow snapshot of the per-instance metadata to preserve on the target, if any.
    /// </summary>
    public InstanceMetadata? Metadata { get; }

    /// <summary>
    /// Gets the source item instance this transfer entry comes from.
    /// </summary>
    public ItemInstance<TKey> SourceInstance { get; }

    /// <summary>
    /// Creates a transfer entry for transfer-builder inspection.
    /// </summary>
    /// <param name="definition">The item definition to transfer.</param>
    /// <param name="amount">The amount to transfer.</param>
    /// <param name="metadata">Optional metadata to snapshot for the target.</param>
    /// <param name="sourceInstance">The source item instance this entry comes from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="sourceInstance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than or equal to zero.</exception>
    internal InventoryTransferEntry(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        ItemInstance<TKey> sourceInstance)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (sourceInstance == null)
            throw new ArgumentNullException(nameof(sourceInstance));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");

        Definition = definition;
        Amount = amount;
        Metadata = CloneMetadataOrNull(metadata);
        SourceInstance = sourceInstance;
    }

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? source)
    {
        if (source == null || source.IsEmpty)
            return null;

        return source.Clone();
    }
}
