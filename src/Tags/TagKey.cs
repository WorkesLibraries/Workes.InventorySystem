using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Tags;

/// <summary>
/// Identifies a tag using ordinal string equality and optional namespaced hierarchy parts.
/// </summary>
public sealed class TagKey : IEquatable<TagKey>
{
    /// <summary>
    /// Gets the original tag identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the namespace portion of a strict namespaced tag id.
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets the path portion of a strict namespaced tag id.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets the dot-separated path segments for a strict namespaced tag id.
    /// </summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>
    /// Gets whether this tag has valid namespace and path parts.
    /// </summary>
    public bool IsNamespaced => Namespace != null && Path != null;

    /// <summary>
    /// Creates a tag key from an identifier.
    /// </summary>
    /// <param name="id">The tag identifier.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty, or whitespace.</exception>
    /// <remarks>
    /// This constructor accepts non-namespaced ids. Use <see cref="Parse"/> or <see cref="TryParse"/> when a strict
    /// <c>namespace:path.segment</c> format is required.
    /// </remarks>
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

    /// <summary>
    /// Parses a strict namespaced tag id.
    /// </summary>
    /// <param name="id">A tag id in the form <c>namespace:path.segment</c>.</param>
    /// <returns>The parsed tag key.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> does not contain a valid namespace and path, or contains invalid characters.
    /// </exception>
    /// <remarks>Namespace and path segments may contain letters, digits, underscores, and hyphens.</remarks>
    public static TagKey Parse(string id)
    {
        if (!TryParse(id, out var tag) || tag == null)
            throw new ArgumentException($"Invalid namespaced tag id '{id}'.", nameof(id));

        return tag;
    }

    /// <summary>
    /// Attempts to parse a strict namespaced tag id.
    /// </summary>
    /// <param name="id">A tag id in the form <c>namespace:path.segment</c>.</param>
    /// <param name="tag">The parsed tag key when parsing succeeds; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Namespace and path segments may contain letters, digits, underscores, and hyphens.</remarks>
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
