namespace Workes.InventorySystem.Layout;

/// <summary>
/// Optional layout capability for rebuilding placement while changing one runtime layout parameter.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This capability is separate from <see cref="IParameterizedInventoryLayout{TKey}"/> because preserving current
/// placement and rebuilding empty placement have different compatibility rules.
/// </remarks>
public interface IParameterizedRepackableInventoryLayout<TKey> :
    IRepackableInventoryLayout<TKey>,
    IParameterizedInventoryLayout<TKey>
{
    /// <summary>
    /// Attempts to create an empty repack target with one runtime parameter changed.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="layout">The empty configured layout when creation succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="failure">A consumer-facing reason when creation fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an empty parameterized repack target was created; otherwise, <see langword="false"/>.</returns>
    bool TryCreateEmptyRepackLayoutWithParameter(
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? failure);
}
