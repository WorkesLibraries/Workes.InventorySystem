using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Tags;

public sealed class TagKey : IEquatable<TagKey>
{
    public string Id { get; }
    public string? Namespace { get; }
    public string? Path { get; }
    public IReadOnlyList<string> Segments { get; }
    public bool IsNamespaced => Namespace != null && Path != null;

    public TagKey(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("TagKey id cannot be null or empty.");

        Id = id;
        ParseParts(id, strict: false, out var tagNamespace, out var tagPath, out var segments);
        Namespace = tagNamespace;
        Path = tagPath;
        Segments = segments;
    }

    private TagKey(string id, string tagNamespace, string path, IReadOnlyList<string> segments)
    {
        Id = id;
        Namespace = tagNamespace;
        Path = path;
        Segments = segments;
    }

    public static TagKey Parse(string id)
    {
        if (!TryParse(id, out var tag) || tag == null)
            throw new ArgumentException($"Invalid namespaced tag id '{id}'.", nameof(id));

        return tag;
    }

    public static bool TryParse(string id, out TagKey? tag)
    {
        tag = null;
        if (!ParseParts(id, strict: true, out var tagNamespace, out var path, out var segments))
            return false;

        tag = new TagKey(id, tagNamespace!, path!, segments);
        return true;
    }

    private static bool ParseParts(
        string id,
        bool strict,
        out string? tagNamespace,
        out string? path,
        out IReadOnlyList<string> segments)
    {
        tagNamespace = null;
        path = null;
        segments = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(id))
            return false;

        var colonIndex = id.IndexOf(':');
        if (colonIndex <= 0 || colonIndex != id.LastIndexOf(':') || colonIndex == id.Length - 1)
            return !strict;

        var parsedNamespace = id.Substring(0, colonIndex);
        var parsedPath = id.Substring(colonIndex + 1);
        if (!IsValidIdentifierPart(parsedNamespace))
            return !strict;

        var parsedSegments = parsedPath.Split(new[] { '.' }, StringSplitOptions.None);
        if (parsedSegments.Length == 0)
            return !strict;

        foreach (var segment in parsedSegments)
        {
            if (!IsValidIdentifierPart(segment))
                return !strict;
        }

        tagNamespace = parsedNamespace;
        path = parsedPath;
        segments = Array.AsReadOnly(parsedSegments);
        return true;
    }

    private static bool IsValidIdentifierPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                continue;
            return false;
        }

        return true;
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
