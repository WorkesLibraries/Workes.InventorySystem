using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Tags;

public sealed class TagCatalog
{
    private readonly Dictionary<string, TagKey> _tags = new(StringComparer.Ordinal);

    public IEnumerable<TagKey> All => _tags.Values;

    public TagKey Define(string id)
    {
        return Define(TagKey.Parse(id));
    }

    public TagKey Define(TagKey tag)
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

    public TagKey Get(string id)
    {
        if (!TryGet(id, out var tag) || tag == null)
            throw new InvalidOperationException($"Tag '{id}' is not declared in this tag catalog.");

        return tag;
    }

    public bool TryGet(string id, out TagKey? tag)
    {
        tag = null;
        if (!TagKey.TryParse(id, out var parsed) || parsed == null)
            return false;

        return TryGet(parsed, out tag);
    }

    public bool TryGet(TagKey tag, out TagKey? catalogTag)
    {
        catalogTag = null;
        if (tag == null)
            return false;

        return _tags.TryGetValue(tag.Id, out catalogTag);
    }

    public bool Contains(TagKey tag)
    {
        if (tag == null)
            return false;

        return _tags.ContainsKey(tag.Id);
    }

    public IReadOnlyCollection<TagKey> GetHierarchy(TagKey tag)
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
