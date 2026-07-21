namespace Workes.InventorySystem.Layout;

/// <summary>
/// Optional layout capability for rebuilding placement through inventory-owned repack.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Implementations create an empty layout with equivalent configuration. The inventory remains responsible for
/// collecting current visible order, placing items through normal automatic-placement behavior, validating the
/// candidate state, committing atomically, and raising events.
/// </remarks>
public interface IRepackableInventoryLayout<TKey> : IInventoryLayout<TKey>
{
    /// <summary>
    /// Attempts to create an empty layout configured like this layout for inventory-owned repack.
    /// </summary>
    /// <param name="layout">The empty configured layout when creation succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A consumer-facing reason when creation fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an empty repack target was created; otherwise, <see langword="false"/>.</returns>
    bool TryCreateEmptyRepackLayout(
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? error);
}
