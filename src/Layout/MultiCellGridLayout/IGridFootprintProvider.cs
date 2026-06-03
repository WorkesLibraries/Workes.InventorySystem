using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Resolves grid footprints for item definitions.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract used by <see cref="MultiCellGridLayout{TKey}"/>.
/// Normal application code configures a footprint provider when creating the layout;
/// the layout calls the provider during inventory validation and placement.
/// </remarks>
public interface IGridFootprintProvider<TKey>
{
    /// <summary>
    /// Gets the footprint for an item definition.
    /// </summary>
    /// <param name="definition">The item definition to evaluate.</param>
    /// <returns>The grid footprint occupied by the definition.</returns>
    GridFootprint GetFootprint(ItemDefinition<TKey> definition);
}
