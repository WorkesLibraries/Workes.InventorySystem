namespace Workes.InventorySystem.Layout;

/// <summary>
/// Represents layout-specific data that can be stored and restored.
/// </summary>
/// <remarks>
/// This is infrastructure for layout persistence and custom layout implementations.
/// Normal inventory mutation should still go through <c>Inventory&lt;TKey&gt;</c>.
/// </remarks>
public interface ILayoutPersistentData
{
    /// <summary>
    /// Gets the raw layout context data for persistence.
    /// </summary>
    /// <returns>The layout-specific persistent context.</returns>
    object? GetPersistentContext();
}
