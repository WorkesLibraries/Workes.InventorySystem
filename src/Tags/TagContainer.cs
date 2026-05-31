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
    /// <param name="tag">The tag to add.</param>
    public void Add(TagKey tag)
    {
        _tags.Add(tag);
    }

    /// <summary>
    /// Determines whether the container has the specified tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns><see langword="true"/> when the tag is present; otherwise, <see langword="false"/>.</returns>
    public bool Has(TagKey tag)
    {
        return _tags.Contains(tag);
    }

    /// <summary>
    /// Returns all tags in the container.
    /// </summary>
    /// <returns>The stored tags.</returns>
    public IEnumerable<TagKey> All() => _tags;
}
