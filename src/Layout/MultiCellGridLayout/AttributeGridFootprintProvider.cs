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
    public AttributeKey<int> WidthAttribute { get; }

    /// <summary>
    /// Gets the definition attribute used as footprint height.
    /// </summary>
    public AttributeKey<int> HeightAttribute { get; }

    /// <summary>
    /// Gets the footprint used when a definition is missing either footprint attribute.
    /// </summary>
    public GridFootprint DefaultFootprint { get; }

    /// <summary>
    /// Creates an attribute-based footprint provider.
    /// </summary>
    /// <param name="widthAttribute">The definition attribute used as footprint width.</param>
    /// <param name="heightAttribute">The definition attribute used as footprint height.</param>
    /// <param name="defaultFootprint">The footprint used when either attribute is missing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="widthAttribute"/> or <paramref name="heightAttribute"/> is <see langword="null"/>.</exception>
    public AttributeGridFootprintProvider(
        AttributeKey<int> widthAttribute,
        AttributeKey<int> heightAttribute,
        GridFootprint? defaultFootprint = null)
    {
        WidthAttribute = widthAttribute ?? throw new ArgumentNullException(nameof(widthAttribute));
        HeightAttribute = heightAttribute ?? throw new ArgumentNullException(nameof(heightAttribute));
        DefaultFootprint = defaultFootprint ?? new GridFootprint(1, 1);
    }

    /// <inheritdoc />
    public GridFootprint GetFootprint(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!definition.Attributes.TryGet(WidthAttribute, out int width) ||
            !definition.Attributes.TryGet(HeightAttribute, out int height))
        {
            return DefaultFootprint;
        }

        return new GridFootprint(width, height);
    }
}
