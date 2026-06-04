using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Tags;

/// <summary>
/// Declares canonical namespaced tags and their generated parent hierarchy tags.
/// </summary>
public sealed class TagCatalog
{
    private readonly Dictionary<string, TagKey> _tags = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets all tags declared in the catalog, including generated parent tags.
    /// </summary>
    public IEnumerable<TagDefinition> All
    {
        get
        {
            foreach (var tag in _tags.Values)
                yield return new TagDefinition(tag);
        }
    }

    /// <summary>
    /// Defines a namespaced tag from its string identifier.
    /// </summary>
    /// <param name="id">A tag id in the form <c>namespace:path.segment</c>.</param>
    /// <returns>The canonical catalog tag.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not a valid namespaced tag id.</exception>
    public TagDefinition Define(string id)
    {
        return new TagDefinition(DefineKey(TagKey.Parse(id)));
    }

    internal TagKey DefineKey(TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));
        if (!tag.IsNamespaced)
            throw new ArgumentException("Catalog tags must use namespaced hierarchical ids.", nameof(tag));

        var canonical = AddCanonical(tag);
        foreach (var parent in GetHierarchy(tag))
            AddCanonical(parent);

        return canonical;
    }

    /// <summary>
    /// Gets a previously declared tag by string identifier.
    /// </summary>
    /// <param name="id">The tag id to resolve.</param>
    /// <returns>The canonical catalog tag.</returns>
    /// <exception cref="InvalidOperationException">The tag is not declared in this catalog.</exception>
    public TagDefinition Get(string id)
    {
        if (!TryGet(id, out var tag) || tag == null)
            throw new InvalidOperationException($"Tag '{id}' is not declared in this tag catalog.");

        return tag;
    }

    /// <summary>
    /// Attempts to get a declared tag by string identifier.
    /// </summary>
    /// <param name="id">The tag id to resolve.</param>
    /// <param name="tag">The canonical catalog tag when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the tag is declared; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string id, out TagDefinition? tag)
    {
        tag = null;
        if (!TryGetKey(id, out var parsed) || parsed == null)
            return false;

        tag = new TagDefinition(parsed);
        return true;
    }

    /// <summary>
    /// Determines whether a tag id is declared in the catalog.
    /// </summary>
    /// <param name="id">The tag id to search for.</param>
    /// <returns><see langword="true"/> when the tag is declared; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string id)
    {
        return TryGetKey(id, out _);
    }

    /// <summary>
    /// Gets generated parent hierarchy tags for a declared tag id.
    /// </summary>
    /// <param name="id">The tag id whose parents should be generated.</param>
    /// <returns>Parent tags from most specific to least specific.</returns>
    /// <exception cref="InvalidOperationException">The tag is not declared in this catalog.</exception>
    public IReadOnlyCollection<TagDefinition> GetHierarchy(string id)
    {
        var key = GetKey(id);
        var result = new List<TagDefinition>();
        foreach (var parent in GetHierarchy(key))
            result.Add(new TagDefinition(parent));
        return result;
    }

    internal TagKey GetKey(string id)
    {
        if (!TryGetKey(id, out var tag) || tag == null)
            throw new InvalidOperationException($"Tag '{id}' is not declared in this tag catalog.");

        return tag;
    }

    internal bool TryGetKey(string id, out TagKey? tag)
    {
        tag = null;
        if (!TagKey.TryParse(id, out var parsed) || parsed == null)
            return false;

        return TryGetKey(parsed, out tag);
    }

    internal bool TryGetKey(TagKey tag, out TagKey? catalogTag)
    {
        catalogTag = null;
        if (tag == null)
            return false;

        return _tags.TryGetValue(tag.Id, out catalogTag);
    }

    internal bool Contains(TagKey tag)
    {
        if (tag == null)
            return false;

        return _tags.ContainsKey(tag.Id);
    }

    /// <summary>
    /// Gets the generated parent hierarchy for a namespaced tag.
    /// </summary>
    /// <param name="tag">The tag whose parents should be generated.</param>
    /// <returns>Parent tags from most specific to least specific.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <see langword="null"/>.</exception>
    internal IReadOnlyCollection<TagKey> GetHierarchy(TagKey tag)
    {
        if (tag == null)
            throw new ArgumentNullException(nameof(tag));

        var parents = new List<TagKey>();
        if (!tag.IsNamespaced || tag.Namespace == null || tag.Segments.Count <= 1)
            return parents;

        for (int count = tag.Segments.Count - 1; count >= 1; count--)
        {
            var path = string.Join(".", CopySegments(tag.Segments, count));
            parents.Add(TagKey.Parse(tag.Namespace + ":" + path));
        }

        return parents;
    }

    private static string[] CopySegments(IReadOnlyList<string> segments, int count)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = segments[i];
        return result;
    }

    private TagKey AddCanonical(TagKey tag)
    {
        if (_tags.TryGetValue(tag.Id, out var existing))
            return existing;

        _tags.Add(tag.Id, tag);
        return tag;
    }
}
