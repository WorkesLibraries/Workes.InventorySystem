using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Tags;
namespace Workes.InventorySystem.Core;

public class ItemDefinition<TKey>
{
    public TKey Id { get; }

    public DefinitionSchema Schema { get; } = new();
    public AttributeContainer Attributes { get; } = new();
    public TagContainer Tags { get; } = new();

    public ItemDefinition(TKey id, AttributeContainer? attributes = null, TagContainer? tags = null)
    {
        Id = id;
        Attributes = attributes ?? new AttributeContainer();
        Tags = tags ?? new TagContainer();
    }

    public void Validate()
    {
        Schema.Validate(Attributes);
    }

    public bool HasTag(TagKey tag)
    {
        return Tags.Has(tag);
    }
}
