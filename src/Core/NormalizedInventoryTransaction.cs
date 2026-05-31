using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Semantic (definition+metadata grouped) view of a transaction for capacity evaluation.
/// </summary>
public class NormalizedInventoryTransaction<TKey>
{
    public IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> Added { get; }
    public IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> Removed { get; }
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    public NormalizedInventoryTransaction(
        List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> added,
        List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> removed)
    {
        Added = added ?? new List<(ItemDefinition<TKey>, InstanceMetadata?, int)>();
        Removed = removed ?? new List<(ItemDefinition<TKey>, InstanceMetadata?, int)>();
    }
}
