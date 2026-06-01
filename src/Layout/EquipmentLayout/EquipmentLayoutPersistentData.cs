using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent state for <see cref="EquipmentLayout{TKey}"/>.
/// </summary>
public sealed class EquipmentLayoutPersistentData : ILayoutPersistentData
{
    /// <summary>
    /// Gets or sets the equipment slot ids in layout order.
    /// </summary>
    public List<string> SlotIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the storage-index map for each equipment slot.
    /// </summary>
    public List<int?> SlotMap { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => SlotMap;
}
