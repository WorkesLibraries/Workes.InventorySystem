using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Semantic (definition+metadata grouped) view of a transaction for capacity evaluation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class NormalizedInventoryTransaction<TKey>
{
    /// <summary>
    /// Gets added amounts grouped by item definition and structurally equal metadata.
    /// </summary>
    public IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> Added { get; }

    /// <summary>
    /// Gets removed amounts grouped by item definition and structurally equal metadata.
    /// </summary>
    public IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> Removed { get; }

    /// <summary>
    /// Gets whether the transaction has no added or removed semantic amounts.
    /// </summary>
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    /// <summary>
    /// Creates a normalized transaction.
    /// </summary>
    /// <param name="added">Added amounts grouped by definition and metadata.</param>
    /// <param name="removed">Removed amounts grouped by definition and metadata.</param>
    public NormalizedInventoryTransaction(
        List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> added,
        List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> removed)
    {
        Added = added ?? new List<(ItemDefinition<TKey>, InstanceMetadata?, int)>();
        Removed = removed ?? new List<(ItemDefinition<TKey>, InstanceMetadata?, int)>();
    }
}
