using System;

namespace Workes.InventorySystem.Attributes;

/// <summary>
/// Describes an attribute key declared in an attribute catalog.
/// </summary>
public sealed class AttributeDefinition
{
    /// <summary>
    /// Gets the stable attribute identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the value type associated with the attribute identifier.
    /// </summary>
    public Type ValueType { get; }

    internal object Key { get; }

    internal AttributeDefinition(string id, Type valueType, object key)
    {
        Id = id;
        ValueType = valueType;
        Key = key;
    }
}
