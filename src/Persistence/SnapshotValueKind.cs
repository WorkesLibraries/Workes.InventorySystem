namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Identifies the concrete payload shape stored by a <see cref="SnapshotValue"/>.
/// </summary>
public enum SnapshotValueKind
{
    /// <summary>A null value.</summary>
    Null = 0,

    /// <summary>A Boolean scalar.</summary>
    Boolean = 1,

    /// <summary>A string scalar.</summary>
    String = 2,

    /// <summary>An ordered list of encoded values.</summary>
    List = 3,

    /// <summary>A string-keyed object of encoded values.</summary>
    Object = 4
}
