using System.Collections.Generic;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent data for the entry layout: stores the structural ordering
/// of inventory item indices as maintained by <see cref="EntryLayout{TKey}"/>.
/// </summary>
public class EntryLayoutPersistentData : ILayoutPersistentData
{
    /// <summary>
    /// Structural order of item indices. Each entry is a storage index in the
    /// underlying inventory; the list position represents the layout slot.
    /// </summary>
    public List<int> Order { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => Order;
}
