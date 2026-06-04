namespace Workes.InventorySystem.Core;

/// <summary>
/// Controls how explicit registrations interact with registry-owned auto-increment identity assignment.
/// </summary>
public enum AutoIncrementMode
{
    /// <summary>
    /// Allows explicit registrations and advances generated ids past explicit ids when needed.
    /// </summary>
    FollowExplicitRegistrations,

    /// <summary>
    /// Allows only generated-id registration once enabled.
    /// </summary>
    Strict
}
