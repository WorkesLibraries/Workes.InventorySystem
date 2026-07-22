using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Tests;

public class ItemMetadataMatchTests
{
    [Test]
    public void Exact_NullOrEmptyMetadata_NormalizesToEmpty()
    {
        var empty = new InstanceMetadata();

        Assert.That(ItemMetadataMatch.Exact(null), Is.EqualTo(ItemMetadataMatch.Empty));
        Assert.That(ItemMetadataMatch.Exact(empty), Is.EqualTo(ItemMetadataMatch.Empty));
        Assert.That(ItemMetadataMatch.Exact(empty).Kind, Is.EqualTo(ItemMetadataMatchKind.Empty));
    }

    [Test]
    public void Exact_MetadataIsDetachedOnIngressAndEgress()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("traits", new List<string> { "sharp" });

        var match = ItemMetadataMatch.Exact(metadata);
        metadata.Update<List<string>>("traits", traits =>
        {
            traits.Add("caller-mutated");
            return traits;
        });

        var returned = match.Metadata!;
        returned.Update<List<string>>("traits", traits =>
        {
            traits.Add("returned-mutated");
            return traits;
        });

        Assert.That(match.Metadata!.TryGet<List<string>>("traits", out var traits), Is.True);
        Assert.That(traits, Is.EqualTo(new[] { "sharp" }));
    }

    [Test]
    public void Matches_UsesEmptyExactAndAnySemantics()
    {
        var empty = new InstanceMetadata();
        var fresh = new InstanceMetadata();
        fresh.Set("quality", "fresh");
        var stale = new InstanceMetadata();
        stale.Set("quality", "stale");

        Assert.That(ItemMetadataMatch.Empty.Matches(empty), Is.True);
        Assert.That(ItemMetadataMatch.Empty.Matches(fresh), Is.False);
        Assert.That(ItemMetadataMatch.Exact(fresh).Matches(fresh), Is.True);
        Assert.That(ItemMetadataMatch.Exact(fresh).Matches(stale), Is.False);
        Assert.That(ItemMetadataMatch.Any.Matches(empty), Is.True);
        Assert.That(ItemMetadataMatch.Any.Matches(fresh), Is.True);
    }

    [Test]
    public void Equality_IsStructuralForExactMetadata()
    {
        var first = new InstanceMetadata();
        first.Set("quality", "fresh");
        var second = new InstanceMetadata();
        second.Set("quality", "fresh");
        var different = new InstanceMetadata();
        different.Set("quality", "stale");

        Assert.That(ItemMetadataMatch.Exact(first), Is.EqualTo(ItemMetadataMatch.Exact(second)));
        Assert.That(ItemMetadataMatch.Exact(first), Is.Not.EqualTo(ItemMetadataMatch.Exact(different)));
        Assert.That(ItemMetadataMatch.Any, Is.Not.EqualTo(ItemMetadataMatch.Empty));
    }
}
