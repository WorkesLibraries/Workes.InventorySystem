using System;
namespace Workes.InventorySystem.Attributes;

public sealed class AttributeKey<T> : IEquatable<AttributeKey<T>>
{
    public string Id { get; }

    public AttributeKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("AttributeKey id cannot be null or empty");

        Id = id;
    }

    public bool Equals(AttributeKey<T>? other)
    {
        return other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is AttributeKey<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(typeof(T), StringComparer.Ordinal.GetHashCode(Id));
    }

    public override string ToString() => Id;
}
