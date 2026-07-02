using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Serialized representation of a single item instance.
/// </summary>
[System.Serializable]
[System.Obsolete("Use InventorySnapshotEntry in the portable InventorySnapshot model.")]
public class SerializedItem<TKey>
{
    /// <summary>
    /// Gets or sets the identifier of the item definition to resolve during deserialization.
    /// </summary>
    public TKey DefinitionId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the item amount stored in this serialized instance.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Gets or sets the per-instance metadata values stored with this item.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
