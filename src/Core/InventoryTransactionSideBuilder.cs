using System;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Stages manual inventory-local changes for one side of a cross-inventory transaction.
/// </summary>
/// <remarks>
/// Instances are exposed by <see cref="InventoryTransaction{TKey}.FromSide"/> and
/// <see cref="InventoryTransaction{TKey}.ToSide"/>. Each successful operation updates only this side's transaction
/// simulation. The owning cross-inventory transaction validates both sides again during commit.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryTransactionSideBuilder<TKey>
{
    private readonly InventoryTransaction<TKey> _owner;
    private readonly InventoryTransactionBuilder<TKey> _builder;

    internal InventoryTransactionSideBuilder(
        InventoryTransaction<TKey> owner,
        InventoryTransactionBuilder<TKey> builder)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>Gets whether this side currently has no staged structural changes.</summary>
    public bool IsEmpty => _builder.IsEmpty;

    internal InventoryTransaction<TKey> Build() => _builder.Build();

    /// <summary>Adds items to this side's simulated state.</summary>
    public bool TryAdd(ItemDefinition<TKey> definition, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryAdd(definition, out operationFailure, amount, context), out failure);

    /// <summary>Adds items resolved from a current or migrated definition id to this side's simulated state.</summary>
    public bool TryAdd(TKey definitionId, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryAdd(definitionId, out operationFailure, amount, context), out failure);

    /// <summary>Adds items with optional metadata to this side's simulated state.</summary>
    public bool TryAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryAdd(definition, amount, context, metadata, out operationFailure), out failure);

    /// <summary>Adds items with optional metadata resolved from a definition id to this side's simulated state.</summary>
    public bool TryAdd(TKey definitionId, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryAdd(definitionId, amount, context, metadata, out operationFailure), out failure);

    /// <summary>Adds items to this side's simulated state or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Add(ItemDefinition<TKey> definition, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryAdd(definition, out var failure, amount, context), failure);
        return this;
    }

    /// <summary>Adds items resolved from a definition id or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Add(TKey definitionId, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryAdd(definitionId, out var failure, amount, context), failure);
        return this;
    }

    /// <summary>Adds items with optional metadata or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Add(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata)
    {
        ThrowIfRejected(TryAdd(definition, amount, context, metadata, out var failure), failure);
        return this;
    }

    /// <summary>Adds items with optional metadata resolved from a definition id or throws when rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Add(TKey definitionId, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata)
    {
        ThrowIfRejected(TryAdd(definitionId, amount, context, metadata, out var failure), failure);
        return this;
    }

    /// <summary>Removes items from a known item instance in this side's simulated state.</summary>
    public bool TryRemove(ItemInstance<TKey> instance, out InventoryFailure? failure, int amount = 1) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(instance, out operationFailure, amount), out failure);

    /// <summary>Removes items from a known item instance or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(ItemInstance<TKey> instance, int amount = 1)
    {
        ThrowIfRejected(TryRemove(instance, out var failure, amount), failure);
        return this;
    }

    /// <summary>Removes items at the given storage index in this side's simulated state.</summary>
    public bool TryRemoveAtStorageIndex(int index, out InventoryFailure? failure, int amount = 1) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemoveAtStorageIndex(index, out operationFailure, amount), out failure);

    /// <summary>Removes items at the given storage index or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> RemoveAtStorageIndex(int index, int amount = 1)
    {
        ThrowIfRejected(TryRemoveAtStorageIndex(index, out var failure, amount), failure);
        return this;
    }

    /// <summary>Removes items occupying the given layout context in this side's simulated state.</summary>
    public bool TryRemoveAtContext(ILayoutContext<TKey> context, out InventoryFailure? failure, int amount = 1) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemoveAtContext(context, out operationFailure, amount), out failure);

    /// <summary>Removes items occupying the given layout context or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> RemoveAtContext(ILayoutContext<TKey> context, int amount = 1)
    {
        ThrowIfRejected(TryRemoveAtContext(context, out var failure, amount), failure);
        return this;
    }

    /// <summary>Removes items by definition from this side's simulated state.</summary>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemoveByDefinition(definition, amount, metadataMatch, out operationFailure), out failure);

    /// <summary>Removes items by definition id from this side's simulated state.</summary>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, ItemMetadataMatch metadataMatch, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemoveByDefinition(definitionId, amount, metadataMatch, out operationFailure), out failure);

    /// <summary>Removes items by definition or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> RemoveByDefinition(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch)
    {
        ThrowIfRejected(TryRemoveByDefinition(definition, amount, metadataMatch, out var failure), failure);
        return this;
    }

    /// <summary>Removes items by definition id or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> RemoveByDefinition(TKey definitionId, int amount, ItemMetadataMatch metadataMatch)
    {
        ThrowIfRejected(TryRemoveByDefinition(definitionId, amount, metadataMatch, out var failure), failure);
        return this;
    }

    /// <summary>Removes items that match exact empty metadata.</summary>
    public bool TryRemove(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definition, amount, context, out operationFailure), out failure);

    /// <summary>Removes items that match exact metadata.</summary>
    public bool TryRemove(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definition, amount, metadata, context, out operationFailure), out failure);

    /// <summary>Removes items by definition id that match exact empty metadata.</summary>
    public bool TryRemove(TKey definitionId, int amount, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definitionId, amount, context, out operationFailure), out failure);

    /// <summary>Removes items by definition id that match exact metadata.</summary>
    public bool TryRemove(TKey definitionId, int amount, InstanceMetadata? metadata, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definitionId, amount, metadata, context, out operationFailure), out failure);

    /// <summary>Removes items by definition using an explicit metadata selector.</summary>
    public bool TryRemove(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definition, amount, metadataMatch, context, out operationFailure), out failure);

    /// <summary>Removes items by definition id using an explicit metadata selector.</summary>
    public bool TryRemove(TKey definitionId, int amount, ItemMetadataMatch metadataMatch, ILayoutContext<TKey>? context, out InventoryFailure? failure) =>
        TryStage((InventoryTransactionBuilder<TKey> builder, out InventoryFailure? operationFailure) =>
            builder.TryRemove(definitionId, amount, metadataMatch, context, out operationFailure), out failure);

    /// <summary>Removes items that match exact empty metadata or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(ItemDefinition<TKey> definition, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>Removes items by definition id that match exact empty metadata or throws when rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(TKey definitionId, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>Removes items that match exact metadata or throws when expected-success staging is rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, metadata, context, out var failure), failure);
        return this;
    }

    /// <summary>Removes items by definition id that match exact metadata or throws when rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(TKey definitionId, int amount, InstanceMetadata? metadata, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, metadata, context, out var failure), failure);
        return this;
    }

    /// <summary>Removes items by definition using an explicit metadata selector or throws when rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, metadataMatch, context, out var failure), failure);
        return this;
    }

    /// <summary>Removes items by definition id using an explicit metadata selector or throws when rejected.</summary>
    public InventoryTransactionSideBuilder<TKey> Remove(TKey definitionId, int amount, ItemMetadataMatch metadataMatch, ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, metadataMatch, context, out var failure), failure);
        return this;
    }

    private bool TryStage(StageOperation operation, out InventoryFailure? failure)
    {
        if (!_owner.TryCanStageManualCrossOperation(out failure))
            return false;

        if (!operation(_builder, out failure))
            return false;

        _owner.MarkManualCrossStaged();
        failure = null;
        return true;
    }

    private static void ThrowIfRejected(bool accepted, InventoryFailure? failure)
    {
        if (!accepted)
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Cross-inventory manual staging was rejected."));
    }

    private delegate bool StageOperation(InventoryTransactionBuilder<TKey> builder, out InventoryFailure? failure);
}
