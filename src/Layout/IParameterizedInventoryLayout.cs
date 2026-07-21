using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Inventory layout that can create a replacement layout with one runtime parameter changed.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract. Normal runtime layout tuning should go through
/// <see cref="Inventory{TKey}.TrySetLayoutParameter(string, object?, out InventoryFailure?)"/>
/// so the inventory can validate current contents before committing the change.
/// This contract preserves current placement. Layouts that also support rebuilding
/// placement after a parameter change implement <see cref="IParameterizedRepackableInventoryLayout{TKey}"/>.
/// </remarks>
public interface IParameterizedInventoryLayout<TKey> : IInventoryLayout<TKey>
{
    /// <summary>
    /// Gets the runtime parameters supported by this layout.
    /// </summary>
    IReadOnlyCollection<InventoryParameterDefinition> Parameters { get; }

    /// <summary>
    /// Attempts to create a replacement layout with one parameter changed.
    /// </summary>
    /// <param name="inventory">The inventory requesting the change.</param>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="layout">The replacement layout when creation succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A consumer-facing reason when creation fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a replacement layout was created; otherwise, <see langword="false"/>.</returns>
    bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? error);
}
