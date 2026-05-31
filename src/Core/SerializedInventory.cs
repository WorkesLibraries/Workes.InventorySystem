using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Serializable snapshot of an inventory for persistence.
/// </summary>
[System.Serializable]
public class SerializedInventory<TKey>
{
    public List<SerializedItem<TKey>> Items { get; set; } = new();
    public object? LayoutData { get; set; }
}
