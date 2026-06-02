using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that can create a replacement resolver with one runtime parameter changed.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public interface IParameterizedStackResolver<TKey> : IStackResolver<TKey>
{
    /// <summary>
    /// Gets the runtime parameters supported by this resolver.
    /// </summary>
    IReadOnlyCollection<InventoryParameterDefinition> Parameters { get; }

    /// <summary>
    /// Attempts to create a replacement resolver with one parameter changed.
    /// </summary>
    /// <param name="inventory">The inventory requesting the change.</param>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="resolver">The replacement resolver when creation succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A consumer-facing reason when creation fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a replacement resolver was created; otherwise, <see langword="false"/>.</returns>
    bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IStackResolver<TKey>? resolver,
        out string? error);
}
