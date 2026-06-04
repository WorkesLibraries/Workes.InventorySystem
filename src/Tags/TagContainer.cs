using System.Collections.Generic;
namespace Workes.InventorySystem.Tags;

/// <summary>
/// Stores the tags declared for an item definition.
/// </summary>
public class TagContainer
{
    private readonly HashSet<TagKey> _tags = new();

    /// <summary>
    /// Adds a tag to the container.
    /// </summary>
    /// <param name="id">The tag id to add.</param>
    public void Add(string id)
    {
        _tags.Add(TagKey.Parse(id));
    }

    /// <summary>
    /// Determines whether the container has the specified tag.
    /// </summary>
    /// <param name="id">The tag id to search for.</param>
    /// <returns><see langword="true"/> when the tag is present; otherwise, <see langword="false"/>.</returns>
    public bool Has(string id)
    {
        return _tags.Contains(TagKey.Parse(id));
    }

    /// <summary>
    /// Returns all tags in the container.
    /// </summary>
    /// <returns>The stored tags.</returns>
    public IEnumerable<string> All()
    {
        foreach (var tag in _tags)
            yield return tag.Id;
    }

    internal void Add(TagKey tag)
    {
        _tags.Add(tag);
    }

    internal bool Has(TagKey tag)
    {
        return _tags.Contains(tag);
    }

    internal IEnumerable<TagKey> AllKeys() => _tags;
}
