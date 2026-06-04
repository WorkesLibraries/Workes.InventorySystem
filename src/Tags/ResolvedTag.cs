namespace Workes.InventorySystem.Tags;

/// <summary>
/// Identifies where a resolved tag came from.
/// </summary>
public enum TagSource
{
    /// <summary>
    /// The tag was declared by an item schema.
    /// </summary>
    Schema,

    /// <summary>
    /// The tag was declared directly by an item definition.
    /// </summary>
    Definition,

    /// <summary>
    /// The tag was generated as a parent of a declared hierarchical tag.
    /// </summary>
    GeneratedParent
}

/// <summary>
/// Represents a tag resolved from a definition, schema, or generated hierarchy.
/// </summary>
public readonly struct ResolvedTag
{
    /// <summary>
    /// Gets the resolved tag id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the source of the resolved tag.
    /// </summary>
    public TagSource Source { get; }

    /// <summary>
    /// Gets the original tag id that produced this tag when it was generated from a hierarchy.
    /// </summary>
    public string? OriginId { get; }

    /// <summary>
    /// Creates a resolved tag descriptor.
    /// </summary>
    /// <param name="tag">The resolved tag key.</param>
    /// <param name="source">The source of the resolved tag.</param>
    /// <param name="origin">The original tag when this tag is generated from a parent hierarchy; otherwise, <see langword="null"/>.</param>
    internal ResolvedTag(TagKey tag, TagSource source, TagKey? origin)
    {
        Id = tag.Id;
        Source = source;
        OriginId = origin?.Id;
    }

    /// <inheritdoc />
    public override string ToString() => Id;
}
