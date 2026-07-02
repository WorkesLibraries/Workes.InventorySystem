using System;
namespace Workes.InventorySystem.Attributes;

internal interface IAttributeKey
{
    string Id { get; }
    Type ValueType { get; }
}

/// <summary>
/// Identifies a typed attribute value.
/// </summary>
/// <typeparam name="T">The value type associated with the attribute.</typeparam>
internal sealed class AttributeKey<T> : IAttributeKey, IEquatable<AttributeKey<T>>
{
    /// <summary>
    /// Gets the stable string identifier for this attribute key.
    /// </summary>
    public string Id { get; }

    public Type ValueType => typeof(T);

    internal AttributeKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("AttributeKey id cannot be null or empty");

        Id = id;
    }

    /// <summary>
    /// Determines whether this key has the same value type and identifier as another key.
    /// </summary>
    /// <param name="other">The key to compare with this key.</param>
    /// <returns><see langword="true"/> when the keys are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(AttributeKey<T>? other)
    {
        return other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AttributeKey<T> other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(typeof(T), StringComparer.Ordinal.GetHashCode(Id));
    }

    /// <inheritdoc />
    public override string ToString() => Id;
}
