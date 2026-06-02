using System;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Resolves rectangular grid footprints from item definition attributes.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class AttributeGridFootprintProvider<TKey> : IGridFootprintProvider<TKey>
{
    /// <summary>
    /// Gets the definition attribute used as footprint width.
    /// </summary>
    public string WidthAttributeId { get; }

    /// <summary>
    /// Gets the definition attribute used as footprint height.
    /// </summary>
    public string HeightAttributeId { get; }

    /// <summary>
    /// Gets the footprint used when a definition is missing either footprint attribute.
    /// </summary>
    public GridFootprint DefaultFootprint { get; }

    /// <summary>
    /// Creates an attribute-based footprint provider.
    /// </summary>
    /// <param name="widthAttributeId">The definition attribute id used as footprint width.</param>
    /// <param name="heightAttributeId">The definition attribute id used as footprint height.</param>
    /// <param name="defaultFootprint">The footprint used when either attribute is missing.</param>
    /// <exception cref="ArgumentException"><paramref name="widthAttributeId"/> or <paramref name="heightAttributeId"/> is null, empty, or whitespace.</exception>
    public AttributeGridFootprintProvider(
        string widthAttributeId,
        string heightAttributeId,
        GridFootprint? defaultFootprint = null)
    {
        if (string.IsNullOrWhiteSpace(widthAttributeId))
            throw new ArgumentException("Width attribute id cannot be null or empty.", nameof(widthAttributeId));
        if (string.IsNullOrWhiteSpace(heightAttributeId))
            throw new ArgumentException("Height attribute id cannot be null or empty.", nameof(heightAttributeId));

        WidthAttributeId = widthAttributeId;
        HeightAttributeId = heightAttributeId;
        DefaultFootprint = defaultFootprint ?? new GridFootprint(1, 1);
    }

    /// <inheritdoc />
    public GridFootprint GetFootprint(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!definition.Attributes.TryGet(WidthAttributeId, out int width) ||
            !definition.Attributes.TryGet(HeightAttributeId, out int height))
        {
            return DefaultFootprint;
        }

        return new GridFootprint(width, height);
    }
}
