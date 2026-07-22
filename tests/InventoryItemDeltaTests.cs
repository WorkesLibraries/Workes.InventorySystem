using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Tests;

public class InventoryItemDeltaTests
{
    [Test]
    public void AddAndRemove_CreateContextFreeSemanticOperations()
    {
        var apple = new ItemDefinition<string>("apple");
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        var delta = InventoryItemDelta<string>.Create()
            .Add(apple, amount: 3, metadata, label: "reward")
            .Remove("coin", amount: 5, label: "price")
            .Remove("gem", amount: 1, metadata, label: "specific-gem")
            .Remove("dust", 2, ItemMetadataMatch.Any, label: "any-dust");

        Assert.That(delta.Operations, Has.Count.EqualTo(4));

        var add = delta.Operations[0];
        Assert.That(add.Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Add));
        Assert.That(add.Definition, Is.SameAs(apple));
        Assert.That(add.DefinitionId, Is.EqualTo("apple"));
        Assert.That(add.Amount, Is.EqualTo(3));
        Assert.That(add.Label, Is.EqualTo("reward"));
        Assert.That(add.MetadataMatch.Kind, Is.EqualTo(ItemMetadataMatchKind.Exact));
        Assert.That(add.Metadata!.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));

        var exactEmptyRemove = delta.Operations[1];
        Assert.That(exactEmptyRemove.Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Remove));
        Assert.That(exactEmptyRemove.DefinitionId, Is.EqualTo("coin"));
        Assert.That(exactEmptyRemove.Metadata, Is.Null);
        Assert.That(exactEmptyRemove.MetadataMatch.Kind, Is.EqualTo(ItemMetadataMatchKind.Empty));

        var exactMetadataRemove = delta.Operations[2];
        Assert.That(exactMetadataRemove.Metadata!.StructuralEquals(metadata), Is.True);
        Assert.That(exactMetadataRemove.MetadataMatch.Kind, Is.EqualTo(ItemMetadataMatchKind.Exact));

        var wildcardRemove = delta.Operations[3];
        Assert.That(wildcardRemove.Metadata, Is.Null);
        Assert.That(wildcardRemove.MetadataMatch.Kind, Is.EqualTo(ItemMetadataMatchKind.Any));
    }

    [Test]
    public void Metadata_IsDetachedOnIngressAndEgress()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("traits", new List<string> { "sharp" });

        var delta = InventoryItemDelta<string>.Create()
            .Add("knife", amount: 1, metadata, label: "knife");

        metadata.Update<List<string>>("traits", traits =>
        {
            traits.Add("caller-mutated");
            return traits;
        });

        var operationMetadata = delta.Operations.Single().Metadata!;
        operationMetadata.Update<List<string>>("traits", traits =>
        {
            traits.Add("returned-mutated");
            return traits;
        });

        var reread = delta.Operations.Single().Metadata!;
        Assert.That(reread.TryGet<List<string>>("traits", out var traits), Is.True);
        Assert.That(traits, Is.EqualTo(new[] { "sharp" }));
    }

    [Test]
    public void DuplicateLabels_AreRejectedWithoutAppendingOperation()
    {
        var delta = InventoryItemDelta<string>.Create()
            .Add("apple", amount: 1, label: "item");

        var accepted = delta.TryRemove("coin", amount: 1, label: "item", out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(delta.Operations, Has.Count.EqualTo(1));
        Assert.Throws<InventoryOperationException>(() => delta.Remove("coin", amount: 1, label: "item"));
    }

    [Test]
    public void InvalidOperations_ReturnStructuredFailures()
    {
        var delta = InventoryItemDelta<string>.Create();

        Assert.That(delta.TryAdd((string)null!, amount: 1, metadata: null, label: null, out var nullIdFailure), Is.False);
        Assert.That(nullIdFailure?.Kind, Is.EqualTo(InventoryFailureKind.Definition));

        Assert.That(delta.TryAdd("apple", amount: 0, metadata: null, label: null, out var amountFailure), Is.False);
        Assert.That(amountFailure?.Kind, Is.EqualTo(InventoryFailureKind.Validation));

        Assert.That(delta.IsEmpty, Is.True);
    }

    [Test]
    public void Mirror_InvertsAddAndRemoveOperations()
    {
        var delta = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 4, label: "price")
            .Add("book", amount: 1, label: "purchase-item");

        var mirrored = InventoryItemDelta<string>.Mirror(delta);

        Assert.That(mirrored.Operations, Has.Count.EqualTo(2));
        Assert.That(mirrored.Operations[0].Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Add));
        Assert.That(mirrored.Operations[0].DefinitionId, Is.EqualTo("coin"));
        Assert.That(mirrored.Operations[0].Label, Is.EqualTo("price"));
        Assert.That(mirrored.Operations[1].Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Remove));
        Assert.That(mirrored.Operations[1].DefinitionId, Is.EqualTo("book"));
        Assert.That(mirrored.Operations[1].Label, Is.EqualTo("purchase-item"));
    }

    [Test]
    public void Mirror_RejectsWildcardMetadataRemovals()
    {
        var delta = InventoryItemDelta<string>.Create()
            .Remove("gem", 1, ItemMetadataMatch.Any, label: "runtime-selected-gem");

        Assert.That(delta.TryMirror(out var mirrored, out var failure), Is.False);
        Assert.That(mirrored, Is.Null);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(failure?.Message, Does.Contain("Wildcard-metadata remove operations cannot be mirrored"));

        var exception = Assert.Throws<InventoryOperationException>(() => InventoryItemDelta<string>.Mirror(delta));
        Assert.That(exception!.Failure.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
    }

    [Test]
    public void Combine_MergesCompatibleOperationsAndPreservesCombinedLabelIdentity()
    {
        var bookPurchase = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 8, label: "price")
            .Add("book", amount: 1, label: "purchase-item");
        var inkPurchase = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 17, label: "price")
            .Add("ink", amount: 1, label: "purchase-item");

        var combined = InventoryItemDelta<string>.Combine(
            InventoryItemDeltaPart<string>.From(bookPurchase, prefix: "book", count: 2),
            InventoryItemDeltaPart<string>.From(inkPurchase, prefix: "ink"));

        Assert.That(combined.Operations, Has.Count.EqualTo(3));

        var coins = combined.Operations.Single(operation => operation.DefinitionId == "coin");
        Assert.That(coins.Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Remove));
        Assert.That(coins.Amount, Is.EqualTo(33));
        Assert.That(coins.LabelReferences.Select(reference => reference.CombinedLabel),
            Is.EqualTo(new[] { "book.price", "ink.price" }));
        Assert.That(coins.LabelReferences.Select(reference => reference.Amount),
            Is.EqualTo(new[] { 16, 17 }));

        var books = combined.Operations.Single(operation => operation.DefinitionId == "book");
        Assert.That(books.Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Add));
        Assert.That(books.Amount, Is.EqualTo(2));
        Assert.That(books.LabelReferences.Single().OriginalLabel, Is.EqualTo("purchase-item"));
        Assert.That(books.LabelReferences.Single().Prefix, Is.EqualTo("book"));
        Assert.That(books.LabelReferences.Single().CombinedLabel, Is.EqualTo("book.purchase-item"));

        var ink = combined.Operations.Single(operation => operation.DefinitionId == "ink");
        Assert.That(ink.Amount, Is.EqualTo(1));
        Assert.That(ink.LabelReferences.Single().CombinedLabel, Is.EqualTo("ink.purchase-item"));
    }

    [Test]
    public void Combine_CancelsOppositeOperationsToNetSemanticDelta()
    {
        var reward = InventoryItemDelta<string>.Create()
            .Add("coin", amount: 5, label: "reward");
        var fee = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 5, label: "fee");

        var combined = InventoryItemDelta<string>.Combine(
            InventoryItemDeltaPart<string>.From(reward, "reward"),
            InventoryItemDeltaPart<string>.From(fee, "fee"));

        Assert.That(combined.IsEmpty, Is.True);
    }

    [Test]
    public void Combine_CancelsLabelsFromConsumedSideDeterministically()
    {
        var rewards = InventoryItemDelta<string>.Create()
            .Add("coin", amount: 2, label: "small-reward")
            .Add("coin", amount: 3, label: "large-reward");
        var fee = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 3, label: "fee");

        var combined = InventoryItemDelta<string>.Combine(
            InventoryItemDeltaPart<string>.From(rewards, "rewards"),
            InventoryItemDeltaPart<string>.From(fee, "fee"));

        var operation = combined.Operations.Single();
        Assert.That(operation.Kind, Is.EqualTo(InventoryItemDeltaOperationKind.Add));
        Assert.That(operation.Amount, Is.EqualTo(2));
        Assert.That(operation.LabelReferences.Single().CombinedLabel, Is.EqualTo("rewards.large-reward"));
        Assert.That(operation.LabelReferences.Single().Amount, Is.EqualTo(2));
    }

    [Test]
    public void Combine_RejectsDuplicatePrefixes()
    {
        var delta = InventoryItemDelta<string>.Create().Add("coin", amount: 1, label: "coin");

        Assert.Throws<InventoryOperationException>(() =>
            InventoryItemDelta<string>.Combine(
                InventoryItemDeltaPart<string>.From(delta, "same"),
                InventoryItemDeltaPart<string>.From(delta, "same")));
    }

    [Test]
    public void Combine_RejectsDuplicateCombinedLabels()
    {
        var first = InventoryItemDelta<string>.Create()
            .Add("coin", amount: 1, label: "coin");
        var second = InventoryItemDelta<string>.Create()
            .Add("coin", amount: 1, label: "b.coin");

        Assert.Throws<InventoryOperationException>(() =>
            InventoryItemDelta<string>.Combine(
                InventoryItemDeltaPart<string>.From(first, "a.b"),
                InventoryItemDeltaPart<string>.From(second, "a")));
    }
}
