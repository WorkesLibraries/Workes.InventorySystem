using System.Collections.Generic;
namespace Workes.InventorySystem.Tags;

/// <summary>
/// Stores the tags declared for an item definition.
/// </summary>
internal sealed class TagContainer
{
    private readonly HashSet<string> _tags = new(System.StringComparer.Ordinal);

    /// <summary>
    /// Adds a tag to the container.
    /// </summary>
    /// <param name="id">The tag id to add.</param>
    internal void Add(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException("Tag id cannot be null or empty.", nameof(id));

        _tags.Add(id);
    }

    /// <summary>
    /// Determines whether the container has the specified tag.
    /// </summary>
    /// <param name="id">The tag id to search for.</param>
    /// <returns><see langword="true"/> when the tag is present; otherwise, <see langword="false"/>.</returns>
    internal bool Has(string id)
    {
        return _tags.Contains(id);
    }

    /// <summary>
    /// Returns all tags in the container.
    /// </summary>
    /// <returns>The stored tags.</returns>
    internal IEnumerable<string> All()
    {
        foreach (var tag in _tags)
            yield return tag;
    }

    internal IEnumerable<TagKey> AllKeys(TagCatalog catalog)
    {
        foreach (var tag in _tags)
            yield return catalog.GetKey(tag);
    }
}
