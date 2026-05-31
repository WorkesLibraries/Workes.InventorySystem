namespace Workes.InventorySystem.Tags;

public enum TagSource
{
    Schema,
    Definition,
    GeneratedParent
}

public readonly struct ResolvedTag
{
    public TagKey Tag { get; }
    public TagSource Source { get; }
    public TagKey? Origin { get; }

    public ResolvedTag(TagKey tag, TagSource source, TagKey? origin)
    {
        Tag = tag;
        Source = source;
        Origin = origin;
    }
}
