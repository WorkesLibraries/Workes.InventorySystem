using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Serializable snapshot of an inventory for persistence.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
[System.Serializable]
public class SerializedInventory<TKey>
{
    /// <summary>
    /// Gets or sets the serialized item instances contained by the inventory.
    /// </summary>
    public List<SerializedItem<TKey>> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets layout-specific persistent data.
    /// </summary>
    public object? LayoutData { get; set; }
}
