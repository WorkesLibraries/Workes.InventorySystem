using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Layout;

/// <summary>
/// Marker interface for layout-specific placement instructions.
/// </summary>
/// <remarks>
/// Layout contexts may be used while an operation is formulated, such as selecting
/// merge candidates for an add, or while a completed transaction is prepared for
/// commit, such as mapping transaction added-entry indices to layout positions.
/// Context meaning is owned by each layout implementation.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public interface ILayoutContext<TKey>
{
}
