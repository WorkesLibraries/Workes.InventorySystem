using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent state for <see cref="SectionedLayout{TKey}"/>.
/// </summary>
public sealed class SectionedLayoutPersistentData : ILayoutPersistentData
{
    /// <summary>
    /// Gets or sets section ids in layout order.
    /// </summary>
    public List<string> SectionIds { get; set; } = new();

    /// <summary>
    /// Gets or sets section slot counts in layout order.
    /// </summary>
    public List<int> SectionSlotCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the flattened section-slot map.
    /// </summary>
    public List<int?> SlotMap { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => SlotMap;
}
