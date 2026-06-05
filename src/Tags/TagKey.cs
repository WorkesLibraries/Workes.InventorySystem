using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Tags;

/// <summary>
/// Identifies a tag using ordinal string equality and catalog-mode-specific hierarchy parts.
/// </summary>
internal sealed class TagKey : IEquatable<TagKey>
{
    /// <summary>
    /// Gets the original tag identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the catalog mode this key was parsed for.
    /// </summary>
    public TagCatalogMode Mode { get; }

    /// <summary>
    /// Gets the namespace portion of a strict namespaced tag id.
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets the path portion of a strict namespaced tag id.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the dot-separated path segments for a strict namespaced tag id.
    /// </summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>
    /// Gets whether this tag has valid namespace and path parts.
    /// </summary>
    public bool IsNamespaced => Mode == TagCatalogMode.Namespaced;

    private TagKey(string id, TagCatalogMode mode, string? tagNamespace, string path, IReadOnlyList<string> segments)
    {
        Id = id;
        Mode = mode;
        Namespace = tagNamespace;
        Path = path;
        Segments = segments;
    }

    /// <summary>
    /// Parses a tag id for the specified catalog mode.
    /// </summary>
    /// <param name="id">The tag id to parse.</param>
    /// <param name="mode">The tag catalog mode.</param>
    /// <returns>The parsed tag key.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not valid for <paramref name="mode"/>.</exception>
    internal static TagKey Parse(string id, TagCatalogMode mode)
    {
        if (!TryParse(id, mode, out var tag) || tag == null)
        {
            var expected = mode == TagCatalogMode.Namespaced
                ? "namespaced tag id"
                : "non-namespaced tag id";
            throw new ArgumentException($"Invalid {expected} '{id}'.", nameof(id));
        }

        return tag;
    }

    /// <summary>
    /// Attempts to parse a tag id for the specified catalog mode.
    /// </summary>
    /// <param name="id">The tag id to parse.</param>
    /// <param name="mode">The tag catalog mode.</param>
    /// <param name="tag">The parsed tag key when parsing succeeds; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    internal static bool TryParse(string id, TagCatalogMode mode, out TagKey? tag)
    {
        tag = null;

        return mode == TagCatalogMode.Namespaced
            ? TryParseNamespaced(id, out tag)
            : TryParseNonNamespaced(id, out tag);
    }

    private static bool TryParseNamespaced(string id, out TagKey? tag)
    {
        tag = null;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        var colonIndex = id.IndexOf(':');
        if (colonIndex <= 0 || colonIndex != id.LastIndexOf(':') || colonIndex == id.Length - 1)
            return false;

        var parsedNamespace = id.Substring(0, colonIndex);
        var parsedPath = id.Substring(colonIndex + 1);
        if (!IsValidIdentifierPart(parsedNamespace))
            return false;

        if (!TryParseSegments(parsedPath, out var parsedSegments))
            return false;

        tag = new TagKey(id, TagCatalogMode.Namespaced, parsedNamespace, parsedPath, parsedSegments);
        return true;
    }

    private static bool TryParseNonNamespaced(string id, out TagKey? tag)
    {
        tag = null;

        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (id.IndexOf(':') >= 0)
            return false;
        if (!TryParseSegments(id, out var parsedSegments))
            return false;

        tag = new TagKey(id, TagCatalogMode.NonNamespaced, null, id, parsedSegments);
        return true;
    }

    private static bool TryParseSegments(string path, out IReadOnlyList<string> segments)
    {
        segments = Array.Empty<string>();
        var parsedSegments = path.Split(new[] { '.' }, StringSplitOptions.None);
        if (parsedSegments.Length == 0)
            return false;

        foreach (var segment in parsedSegments)
        {
            if (!IsValidIdentifierPart(segment))
                return false;
        }

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

    /// <summary>
    /// Determines whether this tag has the same identifier as another tag.
    /// </summary>
    /// <param name="other">The tag to compare with this tag.</param>
    /// <returns><see langword="true"/> when the identifiers match using ordinal comparison; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TagKey? other)
    {
        return other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is TagKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Id);
    }

    /// <inheritdoc />
    public override string ToString() => Id;
}
