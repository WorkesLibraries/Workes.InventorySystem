using System.Collections.Generic;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent data for a fixed-slot layout.
/// </summary>
public class SlotLayoutPersistentData : ILayoutPersistentData
{
    /// <summary>
    /// Gets or sets the map from slot index to inventory storage index.
    /// </summary>
    public List<int?> SlotMap { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => SlotMap;
}
