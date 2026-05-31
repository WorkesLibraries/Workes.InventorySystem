namespace Workes.InventorySystem.Layout;

/// <summary>
/// Represents layout-specific data that can be stored and restored.
/// </summary>
public interface ILayoutPersistentData
{
    /// <summary>
    /// Gets the raw layout context data for persistence.
    /// </summary>
    /// <returns>The layout-specific persistent context.</returns>
    object? GetPersistentContext();
}
