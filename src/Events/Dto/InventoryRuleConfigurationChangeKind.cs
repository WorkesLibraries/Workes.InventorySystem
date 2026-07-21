namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Identifies the kind of rule configuration mutation represented by an inventory change event.
/// </summary>
public enum InventoryRuleConfigurationChangeKind
{
    /// <summary>A new rule entry was added.</summary>
    Added,

    /// <summary>An existing rule policy or full rule state was replaced.</summary>
    Replaced,

    /// <summary>An existing rule entry was removed.</summary>
    Removed,

    /// <summary>An existing rule's enabled state changed.</summary>
    EnabledChanged,

    /// <summary>An existing rule's priority changed.</summary>
    PriorityChanged
}
