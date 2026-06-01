using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent state for <see cref="MultiCellGridLayout{TKey}"/>.
/// </summary>
public sealed class MultiCellGridLayoutPersistentData : ILayoutPersistentData
{
    /// <summary>
    /// Gets or sets the grid width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the grid height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the automatic placement order.
    /// </summary>
    public GridPlacementOrder PlacementOrder { get; set; }

    /// <summary>
    /// Gets or sets the row-major cell map.
    /// </summary>
    public List<int?> CellMap { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => CellMap;
}
