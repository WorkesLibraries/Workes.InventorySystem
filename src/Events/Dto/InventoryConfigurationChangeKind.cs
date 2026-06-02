namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Identifies the kind of inventory component changed by a runtime configuration update.
/// </summary>
public enum InventoryConfigurationChangeKind
{
    /// <summary>
    /// The inventory stack resolver changed.
    /// </summary>
    StackResolver,

    /// <summary>
    /// The inventory capacity policy changed.
    /// </summary>
    CapacityPolicy,

    /// <summary>
    /// The inventory layout changed.
    /// </summary>
    Layout
}
