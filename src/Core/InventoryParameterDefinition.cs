using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes one runtime-tunable inventory component parameter.
/// </summary>
public sealed class InventoryParameterDefinition
{
    /// <summary>
    /// Gets the stable parameter id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the expected value type for the parameter.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets a short developer-facing description of the parameter.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates a runtime parameter definition.
    /// </summary>
    /// <param name="id">The stable parameter id.</param>
    /// <param name="valueType">The expected value type.</param>
    /// <param name="description">A short developer-facing description.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="valueType"/> is <see langword="null"/>.</exception>
    public InventoryParameterDefinition(string id, Type valueType, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Parameter id cannot be null or empty.", nameof(id));

        Id = id;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        Description = description ?? string.Empty;
    }
}
