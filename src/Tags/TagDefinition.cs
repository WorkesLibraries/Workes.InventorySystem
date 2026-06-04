using System.Collections.Generic;

namespace Workes.InventorySystem.Tags;

/// <summary>
/// Describes a catalog-declared tag using public string metadata.
/// </summary>
public sealed class TagDefinition
{
    internal TagDefinition(TagKey key)
    {
        Id = key.Id;
        Namespace = key.Namespace!;
        Path = key.Path!;
        Segments = key.Segments;
    }

    /// <summary>
    /// Gets the full tag id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the namespace portion of the tag id.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the path portion of the tag id.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the dot-separated path segments.
    /// </summary>
    public IReadOnlyList<string> Segments { get; }

    /// <inheritdoc />
    public override string ToString() => Id;

    /// <summary>
    /// Converts a tag definition to its string id.
    /// </summary>
    /// <param name="definition">The tag definition.</param>
    /// <returns>The tag id.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public static implicit operator string(TagDefinition definition)
    {
        if (definition == null)
            throw new System.ArgumentNullException(nameof(definition));

        return definition.Id;
    }
}
