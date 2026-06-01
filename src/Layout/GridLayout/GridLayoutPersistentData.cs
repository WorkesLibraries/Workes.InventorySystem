using System.Collections.Generic;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Persistent data for a fixed-size grid layout.
/// </summary>
public class GridLayoutPersistentData : ILayoutPersistentData
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
    /// Gets or sets the automatic placement order used by the layout.
    /// </summary>
    public GridPlacementOrder PlacementOrder { get; set; }

    /// <summary>
    /// Gets or sets the map from row-major cell index to inventory storage index.
    /// </summary>
    public List<int?> CellMap { get; set; } = new();

    /// <inheritdoc />
    public object? GetPersistentContext() => CellMap;
}
