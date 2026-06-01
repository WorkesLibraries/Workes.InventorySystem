using System;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Describes the rectangular grid footprint occupied by an item.
/// </summary>
public sealed class GridFootprint
{
    /// <summary>
    /// Gets the footprint width in grid cells.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the footprint height in grid cells.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Creates a rectangular grid footprint.
    /// </summary>
    /// <param name="width">The footprint width in grid cells.</param>
    /// <param name="height">The footprint height in grid cells.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less than or equal to zero.</exception>
    public GridFootprint(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Footprint width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Footprint height must be greater than zero.");

        Width = width;
        Height = height;
    }
}
