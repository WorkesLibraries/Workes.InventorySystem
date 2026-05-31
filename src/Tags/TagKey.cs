using System;
namespace Workes.InventorySystem.Tags;

public sealed class TagKey : IEquatable<TagKey>
{
    public string Id { get; }

    public TagKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("TagKey id cannot be null or empty.");

        Id = id;
    }

    public bool Equals(TagKey? other)
    {
        return other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is TagKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Id);
    }

    public override string ToString() => Id;
}
