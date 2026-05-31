namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes an attribute required or inherited by an item schema.
/// </summary>
public sealed class SchemaAttribute
{
    /// <summary>
    /// Gets the attribute key object.
    /// </summary>
    public object Key { get; }

    /// <summary>
    /// Gets whether the attribute requirement was inherited from another schema.
    /// </summary>
    public bool Inherited { get; }

    /// <summary>
    /// Creates a schema attribute descriptor.
    /// </summary>
    /// <param name="key">The attribute key object.</param>
    /// <param name="inherited">Whether the requirement was inherited.</param>
    public SchemaAttribute(object key, bool inherited)
    {
        Key = key;
        Inherited = inherited;
    }
}
