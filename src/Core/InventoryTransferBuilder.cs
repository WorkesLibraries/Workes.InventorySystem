using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds a cross-inventory transfer from a source inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
/// <remarks>
/// Builders created with <see cref="InventoryTransfer.From{TKey}(Inventory{TKey})"/> stage outgoing removals only.
/// Builders created through <see cref="To"/> also validate target additions while staging.
/// </remarks>
[Obsolete("InventoryTransferBuilder is retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
public sealed class InventoryTransferBuilder<TKey>
{
    private readonly List<PlannedOperation> _operations = new();

    private abstract class PlannedOperation
    {
        protected PlannedOperation(ILayoutContext<TKey>? targetContext)
        {
            TargetContext = targetContext;
        }

        public ILayoutContext<TKey>? TargetContext { get; }

        public abstract bool ApplySource(Inventory<TKey> source, InventoryTransactionBuilder<TKey> sourceBuilder, out InventoryFailure? failure);
    }

    private sealed class ItemRemoveOperation : PlannedOperation
    {
        private readonly ItemInstance<TKey> _item;
        private readonly int _amount;

        public ItemRemoveOperation(ItemInstance<TKey> item, int amount, ILayoutContext<TKey>? targetContext)
            : base(targetContext)
        {
            _item = item;
            _amount = amount;
        }

        public override bool ApplySource(Inventory<TKey> source, InventoryTransactionBuilder<TKey> sourceBuilder, out InventoryFailure? failure) =>
            sourceBuilder.TryRemove(_item, out failure, _amount);
    }

    private sealed class StorageIndexRemoveOperation : PlannedOperation
    {
        private readonly int _index;
        private readonly int _amount;

        public StorageIndexRemoveOperation(int index, int amount, ILayoutContext<TKey>? targetContext)
            : base(targetContext)
        {
            _index = index;
            _amount = amount;
        }

        public override bool ApplySource(Inventory<TKey> source, InventoryTransactionBuilder<TKey> sourceBuilder, out InventoryFailure? failure) =>
            sourceBuilder.TryRemoveAtStorageIndex(_index, out failure, _amount);
    }

    private sealed class DefinitionRemoveOperation : PlannedOperation
    {
        private readonly ItemDefinition<TKey> _definition;
        private readonly int _amount;
        private readonly bool _ignoreMetadata;

        public DefinitionRemoveOperation(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata)
            : base(targetContext: null)
        {
            _definition = definition;
            _amount = amount;
            _ignoreMetadata = ignoreMetadata;
        }

        public override bool ApplySource(Inventory<TKey> source, InventoryTransactionBuilder<TKey> sourceBuilder, out InventoryFailure? failure) =>
            sourceBuilder.TryRemoveByDefinition(_definition, _amount, _ignoreMetadata, out failure);
    }

    internal InventoryTransferBuilder(Inventory<TKey> source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    private InventoryTransferBuilder(Inventory<TKey> source, Inventory<TKey> target)
        : this(source)
    {
        Target = target;
    }

    /// <summary>
    /// Gets the source inventory whose items are planned to leave.
    /// </summary>
    public Inventory<TKey> Source { get; }

    /// <summary>
    /// Gets the target inventory when this builder is target-bound; otherwise, <see langword="null"/>.
    /// </summary>
    public Inventory<TKey>? Target { get; }

    /// <summary>
    /// Gets whether this builder validates target additions while staging removals.
    /// </summary>
    public bool IsTargetBound => Target != null;

    /// <summary>
    /// Creates a target-bound transfer builder that validates source removals and target additions while staging.
    /// </summary>
    /// <param name="target">The inventory that should receive staged entries.</param>
    /// <returns>A transfer builder for this source and <paramref name="target"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The builder is already modified, already target-bound, or cannot target <paramref name="target"/>.</exception>
    public InventoryTransferBuilder<TKey> To(Inventory<TKey> target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (IsTargetBound)
            throw new InvalidOperationException("Transfer builder is already target-bound.");
        if (_operations.Count > 0)
            throw new InvalidOperationException("Target must be bound before staging transfer removals.");
        if (!InventoryTransfer.TryValidateCompatibility(Source, target, out var failure))
            throw new InvalidOperationException(failure?.Message ?? "Transfer target is incompatible.");

        return new InventoryTransferBuilder<TKey>(Source, target);
    }

    /// <summary>
    /// Gets whether the builder contains no outgoing items.
    /// </summary>
    public bool IsEmpty => _operations.Count == 0;

    /// <summary>
    /// Gets a snapshot of the outgoing entries currently planned by this builder.
    /// </summary>
    public IReadOnlyList<InventoryTransferEntry<TKey>> Entries
    {
        get
        {
            if (!TryBuildSourceTransaction(out var transaction, out _) || transaction == null)
                return Array.Empty<InventoryTransferEntry<TKey>>();

            return BuildEntries(transaction);
        }
    }

    /// <summary>
    /// Plans to remove an amount from a source item instance for transfer.
    /// </summary>
    /// <param name="item">The source item instance to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(ItemInstance<TKey> item, int amount, out InventoryFailure? failure)
    {
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        return TryStage(new ItemRemoveOperation(item, amount, targetContext: null), out failure);
    }

    /// <summary>
    /// Plans to remove an amount from a source item instance and place it through a direct target context.
    /// </summary>
    /// <param name="item">The source item instance to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="targetContext">The direct target layout context for this incoming entry.</param>
    /// <param name="failure">A consumer-facing reason when the removal or target addition is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This builder is not target-bound.</exception>
    public bool TryRemove(ItemInstance<TKey> item, int amount, ILayoutContext<TKey>? targetContext, out InventoryFailure? failure)
    {
        EnsureTargetBoundForContext();
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        return TryStage(new ItemRemoveOperation(item, amount, targetContext), out failure);
    }

    /// <summary>
    /// Plans to remove an amount from the source item at a storage index for transfer.
    /// </summary>
    /// <param name="index">The source storage index to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtStorageIndex(int index, int amount, out InventoryFailure? failure)
    {
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        return TryStage(new StorageIndexRemoveOperation(index, amount, targetContext: null), out failure);
    }

    /// <summary>
    /// Plans to remove an amount from the source item at a storage index and place it through a direct target context.
    /// </summary>
    /// <param name="index">The source storage index to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="targetContext">The direct target layout context for this incoming entry.</param>
    /// <param name="failure">A consumer-facing reason when the removal or target addition is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This builder is not target-bound.</exception>
    public bool TryRemoveAtStorageIndex(int index, int amount, ILayoutContext<TKey>? targetContext, out InventoryFailure? failure)
    {
        EnsureTargetBoundForContext();
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        return TryStage(new StorageIndexRemoveOperation(index, amount, targetContext), out failure);
    }

    /// <summary>
    /// Plans to remove an amount by item definition for transfer.
    /// </summary>
    /// <param name="definition">The definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out InventoryFailure? failure)
    {
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        return TryStage(new DefinitionRemoveOperation(definition, amount, ignoreMetadata), out failure);
    }

    /// <summary>
    /// Plans to remove an amount by a current or migrated item definition id for transfer.
    /// </summary>
    /// <param name="definitionId">The definition id to resolve through the source inventory's catalog registry.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the removal is planned; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, bool ignoreMetadata, out InventoryFailure? failure)
    {
        if (amount <= 0)
        {
            failure = InventoryFailures.Transfer("Amount must be greater than zero.");
            return false;
        }

        if (!Source.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryRemoveByDefinition(definition, amount, ignoreMetadata, out failure);
    }

    /// <summary>
    /// Evaluates whether this target-bound transfer can still commit against the current live inventories.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer can commit; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This builder is not target-bound.</exception>
    public bool CanCommit(out InventoryFailure? failure)
    {
        EnsureTargetBoundForCommit();
        if (!TryBuildTargetBoundTransactions(out var sourceTransaction, out var targetTransaction, out failure))
            return false;

        return InventoryTransfer.CanCommitTargetBound(Source, sourceTransaction!, Target!, targetTransaction!, out failure);
    }

    /// <summary>
    /// Attempts to commit this target-bound transfer.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer commits; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This builder is not target-bound.</exception>
    public bool TryCommit(out InventoryFailure? failure)
    {
        EnsureTargetBoundForCommit();
        if (!TryBuildTargetBoundTransactions(out var sourceTransaction, out var targetTransaction, out failure))
            return false;

        return InventoryTransfer.TryCommitTargetBound(Source, sourceTransaction!, Target!, targetTransaction!, out failure);
    }

    /// <summary>Commits this target-bound transfer or throws when it is rejected.</summary>
    /// <exception cref="InvalidOperationException">This builder is not target-bound.</exception>
    /// <exception cref="InventoryOperationException">The transfer is rejected.</exception>
    public void Commit()
    {
        if (!TryCommit(out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
    }

    internal InventoryTransaction<TKey> BuildSourceTransaction()
    {
        if (!TryBuildSourceTransaction(out var transaction, out var failure) || transaction == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return transaction;
    }

    internal bool TryBuildSourceTransaction(out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
    {
        return TryReplay(_operations, buildTarget: false, out transaction, out _, out failure);
    }

    internal bool TryBuildTargetBoundTransactions(
        out InventoryTransaction<TKey>? sourceTransaction,
        out InventoryTransaction<TKey>? targetTransaction,
        out InventoryFailure? failure)
    {
        EnsureTargetBoundForCommit();
        return TryReplay(_operations, buildTarget: true, out sourceTransaction, out targetTransaction, out failure);
    }

    internal static IReadOnlyList<InventoryTransferEntry<TKey>> BuildEntries(InventoryTransaction<TKey> transaction)
    {
        var entries = new List<InventoryTransferEntry<TKey>>();

        foreach (var (index, delta) in transaction.AmountDeltas)
        {
            if (delta >= 0)
                continue;

            var sourceInstance = transaction.Inventory.Items[index];
            entries.Add(new InventoryTransferEntry<TKey>(
                sourceInstance.Definition,
                -delta,
                CloneMetadataOrNull(sourceInstance.Metadata),
                sourceInstance));
        }

        foreach (var (_, instance) in transaction.Removed)
        {
            entries.Add(new InventoryTransferEntry<TKey>(
                instance.Definition,
                instance.Amount,
                CloneMetadataOrNull(instance.Metadata),
                instance));
        }

        return entries;
    }

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? metadata)
    {
        if (metadata == null || metadata.IsEmpty)
            return null;

        return metadata.Clone();
    }

    private bool TryStage(PlannedOperation operation, out InventoryFailure? failure)
    {
        var proposed = new List<PlannedOperation>(_operations) { operation };
        if (!TryReplay(proposed, buildTarget: IsTargetBound, out _, out _, out failure))
            return false;

        _operations.Add(operation);
        failure = null;
        return true;
    }

    private bool TryReplay(
        IReadOnlyList<PlannedOperation> operations,
        bool buildTarget,
        out InventoryTransaction<TKey>? sourceTransaction,
        out InventoryTransaction<TKey>? targetTransaction,
        out InventoryFailure? failure)
    {
        sourceTransaction = null;
        targetTransaction = null;

        if (buildTarget && Target == null)
        {
            failure = InventoryFailures.Transfer("Transfer builder is not target-bound.");
            return false;
        }

        var sourceBuilder = InventoryTransaction<TKey>.From(Source);
        var targetBuilder = buildTarget ? InventoryTransaction<TKey>.From(Target!) : null;
        var cumulativeEntries = new List<InventoryTransferEntry<TKey>>();

        foreach (var operation in operations)
        {
            var beforeEntries = cumulativeEntries;
            if (!operation.ApplySource(Source, sourceBuilder, out failure))
                return false;

            var afterEntries = new List<InventoryTransferEntry<TKey>>(BuildEntries(sourceBuilder.Build()));
            var newEntries = DiffEntries(beforeEntries, afterEntries);
            if (newEntries.Count == 0)
            {
                failure = InventoryFailures.Transfer("Transfer contains no items.");
                return false;
            }

            if (buildTarget)
            {
                if (operation.TargetContext != null && newEntries.Count != 1)
                {
                    failure = InventoryFailures.Layout("Direct target context can only be used for a single incoming transfer entry.");
                    return false;
                }

                foreach (var entry in newEntries)
                {
                    var addContext = operation.TargetContext;
                    if (!targetBuilder!.TryAdd(entry.Definition, entry.Amount, addContext, CloneMetadataOrNull(entry.Metadata), out failure))
                        return false;
                }
            }

            cumulativeEntries = afterEntries;
        }

        sourceTransaction = sourceBuilder.Build();
        targetTransaction = targetBuilder?.Build();
        if (operations.Count > 0 && (sourceTransaction.IsEmpty || (buildTarget && targetTransaction!.IsEmpty)))
        {
            failure = InventoryFailures.Transfer("Transfer contains no items.");
            return false;
        }

        failure = null;
        return true;
    }

    private static List<InventoryTransferEntry<TKey>> DiffEntries(
        IReadOnlyList<InventoryTransferEntry<TKey>> before,
        IReadOnlyList<InventoryTransferEntry<TKey>> after)
    {
        var entries = new List<InventoryTransferEntry<TKey>>();
        foreach (var afterEntry in after)
        {
            var previousAmount = FindEntryAmount(before, afterEntry.SourceInstance);
            var delta = afterEntry.Amount - previousAmount;
            if (delta <= 0)
                continue;

            entries.Add(new InventoryTransferEntry<TKey>(
                afterEntry.Definition,
                delta,
                CloneMetadataOrNull(afterEntry.Metadata),
                afterEntry.SourceInstance));
        }

        return entries;
    }

    private static int FindEntryAmount(IReadOnlyList<InventoryTransferEntry<TKey>> entries, ItemInstance<TKey>? sourceInstance)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].SourceInstance, sourceInstance))
                return entries[i].Amount;
        }

        return 0;
    }

    private void EnsureTargetBoundForContext()
    {
        if (!IsTargetBound)
            throw new InvalidOperationException("Target-context removals require a target-bound transfer builder. Call To(target) before staging removals.");
    }

    private void EnsureTargetBoundForCommit()
    {
        if (!IsTargetBound)
            throw new InvalidOperationException("Transfer builder is not target-bound. Commit through the source inventory or call To(target) before staging removals.");
    }
}
