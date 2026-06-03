using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Describes layout-specific placement instructions.
/// </summary>
/// <remarks>
/// Layout contexts may be used while an operation is formulated, such as selecting
/// merge candidates for an add, or while a completed transaction is prepared for
/// commit, such as mapping transaction added-entry indices to layout positions.
/// Context meaning is owned by each layout implementation. The shared
/// <see cref="IsMapped"/> flag only tells inventory orchestration whether this
/// context represents transaction-level added-entry mapping.
/// Normal application code can pass context values to inventory-level methods;
/// custom layouts define context types when built-in addressing models are not enough.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public interface ILayoutContext<TKey>
{
    /// <summary>
    /// Gets whether this context maps transaction added-entry indices to layout-owned placements.
    /// </summary>
    /// <remarks>
    /// A value of <see langword="false"/> means the context describes a direct
    /// operation placement, such as one slot, entry index, grid cell, equipment
    /// slot, or section slot. A value of <see langword="true"/> means the
    /// context maps <see cref="InventoryTransaction{TKey}.Added"/> indices.
    /// </remarks>
    bool IsMapped { get; }
}
