using System;
using System.Collections;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds a bulk structural transaction by operating on a simulated inventory state.
/// Use <see cref="InventoryTransaction{TKey}.For(Inventory{TKey})"/> to create a local builder, then commit it through
/// the builder or target inventory.
/// </summary>
/// <remarks>
/// Transaction builders stage add/remove/amount-delta changes. Layout move and swap operations remain inventory-level
/// operations because they emit dedicated movement event payloads and are not represented by structural transactions.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class InventoryTransactionBuilder<TKey>
{
    private readonly Inventory<TKey> _targetInventory;
    private readonly Inventory<TKey> _simulation;
    private readonly long _targetInventoryVersion;
    private readonly List<SimulationEntry> _simulationEntries = new();

    private sealed class SimulationEntry
    {
        public int? OriginalIndex { get; }
        public ILayoutContext<TKey>? AddedContext { get; }

        public SimulationEntry(int? originalIndex, ILayoutContext<TKey>? addedContext)
        {
            OriginalIndex = originalIndex;
            AddedContext = addedContext;
        }
    }

    internal InventoryTransactionBuilder(Inventory<TKey> targetInventory, Inventory<TKey> simulation)
    {
        _targetInventory = targetInventory ?? throw new ArgumentNullException(nameof(targetInventory));
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _targetInventoryVersion = targetInventory.Version;

        for (int i = 0; i < _simulation.Items.Count; i++)
            _simulationEntries.Add(new SimulationEntry(i, null));
    }

    /// <summary>
    /// Gets whether the staged operations would produce an empty transaction.
    /// </summary>
    public bool IsEmpty => Build().IsEmpty;

    /// <summary>
    /// Evaluates whether the currently staged transaction can be committed.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the staged transaction can currently be committed; otherwise, <see langword="false"/>.</returns>
    public bool Validate(out InventoryFailure? failure) =>
        _targetInventory.CanCommitTransaction(Build(), out failure);

    /// <summary>
    /// Attempts to commit the currently staged transaction.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the staged transaction commits; otherwise, <see langword="false"/>.</returns>
    public bool TryCommit(out InventoryFailure? failure) =>
        _targetInventory.TryCommitTransaction(this, out failure);

    /// <summary>
    /// Commits the currently staged transaction or throws when expected-success commit is rejected.
    /// </summary>
    /// <exception cref="InventoryOperationException">The staged transaction is rejected.</exception>
    public void Commit() =>
        _targetInventory.CommitTransaction(this);

    /// <summary>
    /// Creates a cross-inventory transaction from this source inventory to a target inventory.
    /// </summary>
    /// <param name="target">The second inventory participating in the transaction.</param>
    /// <returns>A cross-inventory transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This builder already contains staged local operations.</exception>
    public InventoryTransaction<TKey> To(Inventory<TKey> target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (!IsEmpty)
            throw new InvalidOperationException("Cross-inventory transactions must be created from a clean transaction builder.");

        return InventoryTransaction<TKey>.CreateCross(_targetInventory, target);
    }

    /// <summary>
    /// Applies a reusable item delta to this clean transaction builder.
    /// </summary>
    /// <param name="delta">The semantic one-inventory delta to apply.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delta"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The builder already contains staged operations.</exception>
    /// <exception cref="InventoryOperationException">The delta cannot be staged.</exception>
    public InventoryTransactionBuilder<TKey> Apply(InventoryItemDelta<TKey> delta)
    {
        return Apply(delta, plan: null);
    }

    /// <summary>
    /// Applies a reusable item delta to this clean transaction builder using an optional application plan.
    /// </summary>
    /// <param name="delta">The semantic one-inventory delta to apply.</param>
    /// <param name="plan">Optional label-based application guidance.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delta"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The builder already contains staged operations.</exception>
    /// <exception cref="InventoryOperationException">The delta cannot be staged.</exception>
    public InventoryTransactionBuilder<TKey> Apply(
        InventoryItemDelta<TKey> delta,
        InventoryDeltaApplicationPlan<TKey>? plan)
    {
        if (!TryApply(delta, plan, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Delta application was rejected."));
        return this;
    }

    /// <summary>
    /// Attempts to apply a reusable item delta to this clean transaction builder.
    /// </summary>
    /// <param name="delta">The semantic one-inventory delta to apply.</param>
    /// <param name="failure">A consumer-facing reason when staging is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the complete delta is staged; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(InventoryItemDelta<TKey> delta, out InventoryFailure? failure) =>
        TryApply(delta, plan: null, out failure);

    /// <summary>
    /// Attempts to apply a reusable item delta to this clean transaction builder using an optional application plan.
    /// </summary>
    /// <param name="delta">The semantic one-inventory delta to apply.</param>
    /// <param name="plan">Optional label-based application guidance.</param>
    /// <param name="failure">A consumer-facing reason when staging is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the complete delta is staged; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(
        InventoryItemDelta<TKey> delta,
        InventoryDeltaApplicationPlan<TKey>? plan,
        out InventoryFailure? failure)
    {
        if (delta == null)
        {
            failure = InventoryFailures.Transaction("Delta cannot be null.");
            return false;
        }
        if (!IsEmpty)
        {
            failure = InventoryFailures.Transaction("Deltas can only be applied to a clean transaction builder.");
            return false;
        }

        var proposed = InventoryTransaction<TKey>.For(_targetInventory);
        foreach (var operation in delta.Operations)
        {
            if (!proposed.TryApplyOperation(operation, plan, out failure))
                return false;
        }

        var transaction = proposed.Build();
        if (!transaction.IsEmpty)
            MergeAndApply(transaction.ForInventory(_simulation));

        failure = null;
        return true;
    }

    /// <summary>
    /// Adds items to the simulated state.
    /// </summary>
    /// <param name="definition">The item definition to add.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns><see langword="true"/> when the simulated add succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(ItemDefinition<TKey> definition, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        return TryAdd(definition, amount, context, null, out failure);
    }

    /// <summary>
    /// Adds items resolved from a current or migrated definition id to the simulated state.
    /// </summary>
    /// <param name="definitionId">The definition id to resolve through the target inventory's catalog registry.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns><see langword="true"/> when the simulated add succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TKey definitionId, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!_targetInventory.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryAdd(definition, amount, context, null, out failure);
    }

    /// <summary>
    /// Adds items with optional metadata to the simulated state.
    /// </summary>
    /// <param name="definition">The item definition to add.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <param name="metadata">Optional per-instance metadata for the added items.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the simulated add succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryFailure? failure)
    {
        failure = null;
        if (!_simulation.TryFormulateAdd(definition, amount, context, metadata, out var tx, out failure) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>
    /// Adds items with optional metadata resolved from a current or migrated definition id to the simulated state.
    /// </summary>
    /// <param name="definitionId">The definition id to resolve through the target inventory's catalog registry.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <param name="metadata">Optional per-instance metadata for the added items.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the simulated add succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TKey definitionId, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryFailure? failure)
    {
        if (!_targetInventory.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryAdd(definition, amount, context, metadata, out failure);
    }

    /// <summary>
    /// Adds items to the simulated state or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Add(
        ItemDefinition<TKey> definition,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryAdd(definition, out var failure, amount, context), failure);
        return this;
    }

    /// <summary>
    /// Adds items resolved from a current or migrated definition id to the simulated state or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Add(
        TKey definitionId,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryAdd(definitionId, out var failure, amount, context), failure);
        return this;
    }

    /// <summary>
    /// Adds items with optional metadata to the simulated state or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Add(
        ItemDefinition<TKey> definition,
        int amount,
        ILayoutContext<TKey>? context,
        InstanceMetadata? metadata)
    {
        ThrowIfRejected(TryAdd(definition, amount, context, metadata, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Adds items with optional metadata resolved from a definition id to the simulated state or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Add(
        TKey definitionId,
        int amount,
        ILayoutContext<TKey>? context,
        InstanceMetadata? metadata)
    {
        ThrowIfRejected(TryAdd(definitionId, amount, context, metadata, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items from the simulated state.
    /// </summary>
    /// <param name="instance">The item instance to remove from. It may come from the original inventory.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when the simulated removal succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>The instance is resolved to the matching instance in the simulation before removal.</remarks>
    public bool TryRemove(ItemInstance<TKey> instance, out InventoryFailure? failure, int amount = 1)
    {
        failure = null;
        var simulationInstance = ResolveToSimulationInstance(instance);
        if (simulationInstance == null)
        {
            failure = InventoryFailures.Validation("Item not found in inventory.");
            return false;
        }

        return TryRemoveSimulationInstance(simulationInstance, amount, out failure);
    }

    /// <summary>
    /// Removes items from the simulated state or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Remove(ItemInstance<TKey> instance, int amount = 1)
    {
        ThrowIfRejected(TryRemove(instance, out var failure, amount), failure);
        return this;
    }

    /// <summary>
    /// Removes items at the given storage index in the simulated state.
    /// </summary>
    /// <param name="index">The storage index to remove from.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when the simulated removal succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtStorageIndex(int index, out InventoryFailure? failure, int amount = 1)
    {
        failure = null;
        if (!_simulation.TryFormulateRemoveAt(index, amount, out var tx, out failure) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>
    /// Removes items by definition from the simulated state.
    /// </summary>
    /// <param name="definition">The item definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the simulated removal succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out InventoryFailure? failure)
    {
        failure = null;
        if (!_simulation.TryFormulateRemoveByDefinition(definition, amount, ignoreMetadata, out var tx, out failure) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>
    /// Removes items at the given storage index in the simulated state or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveAtStorageIndex(int index, int amount = 1)
    {
        ThrowIfRejected(TryRemoveAtStorageIndex(index, out var failure, amount), failure);
        return this;
    }

    /// <summary>
    /// Removes items occupying the given layout context in the simulated state.
    /// </summary>
    /// <param name="context">The layout context whose occupying item should be removed from.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when the simulated removal succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtContext(
        ILayoutContext<TKey> context,
        out InventoryFailure? failure,
        int amount = 1)
    {
        failure = null;
        if (context == null)
        {
            failure = InventoryFailures.LayoutInvalidContext("Removal context cannot be null.");
            return false;
        }

        var item = _simulation.Layout.GetItemAt(_simulation, context);
        if (item == null)
        {
            failure = InventoryFailures.LayoutInvalidContext("No item exists at the requested removal context.");
            return false;
        }

        return TryRemoveSimulationInstance(item, amount, out failure);
    }

    /// <summary>
    /// Removes items occupying the given layout context or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveAtContext(
        ILayoutContext<TKey> context,
        int amount = 1)
    {
        ThrowIfRejected(TryRemoveAtContext(context, out var failure, amount), failure);
        return this;
    }

    /// <summary>
    /// Removes items resolved from a current or migrated definition id from the simulated state.
    /// </summary>
    /// <param name="definitionId">The definition id to resolve through the target inventory's catalog registry.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the simulated removal succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, bool ignoreMetadata, out InventoryFailure? failure)
    {
        if (!_targetInventory.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryRemoveByDefinition(definition, amount, ignoreMetadata, out failure);
    }

    /// <summary>
    /// Removes items by definition from the simulated state or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveByDefinition(
        ItemDefinition<TKey> definition,
        int amount,
        bool ignoreMetadata)
    {
        ThrowIfRejected(TryRemoveByDefinition(definition, amount, ignoreMetadata, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items resolved from a definition id from the simulated state or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveByDefinition(
        TKey definitionId,
        int amount,
        bool ignoreMetadata)
    {
        ThrowIfRejected(TryRemoveByDefinition(definitionId, amount, ignoreMetadata, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items that match the definition and exact empty metadata from the simulated state.
    /// </summary>
    public bool TryRemove(
        ItemDefinition<TKey> definition,
        int amount,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure) =>
        TryRemove(definition, amount, metadata: null, context, out failure);

    /// <summary>
    /// Removes items that match the definition and exact metadata from the simulated state.
    /// </summary>
    public bool TryRemove(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure) =>
        TryRemoveByDefinition(definition, amount, metadata, InventoryItemDeltaMetadataMatch.Exact, context, out failure);

    /// <summary>
    /// Removes items that match the resolved definition id and exact empty metadata from the simulated state.
    /// </summary>
    public bool TryRemove(
        TKey definitionId,
        int amount,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure) =>
        TryRemove(definitionId, amount, metadata: null, context, out failure);

    /// <summary>
    /// Removes items that match the resolved definition id and exact metadata from the simulated state.
    /// </summary>
    public bool TryRemove(
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure)
    {
        if (!_targetInventory.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryRemove(definition, amount, metadata, context, out failure);
    }

    /// <summary>
    /// Removes items by definition while ignoring item-instance metadata.
    /// </summary>
    public bool TryRemoveAnyMetadata(
        ItemDefinition<TKey> definition,
        int amount,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure) =>
        TryRemoveByDefinition(definition, amount, metadata: null, InventoryItemDeltaMetadataMatch.Any, context, out failure);

    /// <summary>
    /// Removes items by resolved definition id while ignoring item-instance metadata.
    /// </summary>
    public bool TryRemoveAnyMetadata(
        TKey definitionId,
        int amount,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure)
    {
        if (!_targetInventory.TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryRemoveAnyMetadata(definition, amount, context, out failure);
    }

    /// <summary>
    /// Removes items that match exact empty metadata or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Remove(
        ItemDefinition<TKey> definition,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items that match exact empty metadata resolved from a definition id or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Remove(
        TKey definitionId,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items that match exact metadata or throws when expected-success staging is rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Remove(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, metadata, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items that match exact metadata resolved from a definition id or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> Remove(
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, metadata, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items by definition while ignoring item-instance metadata or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveAnyMetadata(
        ItemDefinition<TKey> definition,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemoveAnyMetadata(definition, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Removes items by resolved definition id while ignoring item-instance metadata or throws when rejected.
    /// </summary>
    public InventoryTransactionBuilder<TKey> RemoveAnyMetadata(
        TKey definitionId,
        int amount = 1,
        ILayoutContext<TKey>? context = null)
    {
        ThrowIfRejected(TryRemoveAnyMetadata(definitionId, amount, context, out var failure), failure);
        return this;
    }

    /// <summary>
    /// Builds an <see cref="InventoryTransaction{TKey}"/> targeting the original inventory.
    /// </summary>
    /// <remarks>
    /// Most callers should commit the builder directly through <see cref="TryCommit(out InventoryFailure?)"/> or
    /// <see cref="Commit"/>.
    /// Use this method when code needs to inspect or store the structural transaction before committing it.
    /// </remarks>
    /// <returns>A structural transaction that represents all successful simulated operations.</returns>
    public InventoryTransaction<TKey> Build()
    {
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        var remainingOriginalIndices = new HashSet<int>();

        for (int simulationIndex = 0; simulationIndex < _simulation.Items.Count; simulationIndex++)
        {
            var entry = _simulationEntries[simulationIndex];
            var simulationItem = _simulation.Items[simulationIndex];
            if (entry.OriginalIndex.HasValue)
            {
                int originalIndex = entry.OriginalIndex.Value;
                remainingOriginalIndices.Add(originalIndex);
                int originalAmount = _targetInventory.Items[originalIndex].Amount;
                int delta = simulationItem.Amount - originalAmount;
                if (delta != 0)
                    amountDeltas.Add((originalIndex, delta));
            }
            else
            {
                added.Add((simulationItem, entry.AddedContext));
            }
        }

        for (int originalIndex = 0; originalIndex < _targetInventory.Items.Count; originalIndex++)
        {
            if (!remainingOriginalIndices.Contains(originalIndex))
                removed.Add((originalIndex, _targetInventory.Items[originalIndex]));
        }

        return new InventoryTransaction<TKey>(_targetInventory, amountDeltas, removed, added, _targetInventoryVersion);
    }

    /// <summary>
    /// Builds an inventory transaction after applying an optional transaction-level placement context.
    /// </summary>
    /// <param name="placementContext">Optional layout-specific transaction placement context.</param>
    /// <param name="transaction">The mapped transaction when creation succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="failure">A consumer-facing reason when creation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the mapped transaction is valid; otherwise, <see langword="false"/>.</returns>
    public bool TryBuild(
        ILayoutContext<TKey>? placementContext,
        out InventoryTransaction<TKey>? transaction,
        out InventoryFailure? failure)
    {
        var candidate = Build();
        return _targetInventory.TryPrepareTransaction(candidate, placementContext, out transaction, out failure);
    }

    private ItemInstance<TKey>? ResolveToSimulationInstance(ItemInstance<TKey> instance)
    {
        if (instance == null) return null;

        int targetIndex = -1;
        for (int i = 0; i < _targetInventory.Items.Count; i++)
        {
            if (ReferenceEquals(_targetInventory.Items[i], instance))
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex >= 0)
        {
            for (int i = 0; i < _simulationEntries.Count; i++)
            {
                if (_simulationEntries[i].OriginalIndex == targetIndex)
                    return _simulation.Items[i];
            }
        }

        foreach (var simInst in _simulation.Items)
        {
            if (EqualityComparer<TKey>.Default.Equals(simInst.Definition.Id, instance.Definition.Id) &&
                simInst.Metadata.StructuralEquals(instance.Metadata))
                return simInst;
        }
        return null;
    }

    private bool TryRemoveSimulationInstance(
        ItemInstance<TKey> simulationInstance,
        int amount,
        out InventoryFailure? failure)
    {
        if (!_simulation.TryFormulateRemove(simulationInstance, amount, out var tx, out failure) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    private void MergeAndApply(InventoryTransaction<TKey> tx)
    {
        var removed = new List<(int index, ItemInstance<TKey> instance)>(tx.Removed);
        removed.Sort((a, b) => b.index.CompareTo(a.index));
        foreach (var (index, _) in removed)
            _simulationEntries.RemoveAt(index);

        foreach (var (_, context) in tx.Added)
            _simulationEntries.Add(new SimulationEntry(null, context));

        _simulation.ApplyTransactionSilent(tx);
    }

    private bool TryApplyOperation(
        InventoryItemDeltaOperation<TKey> operation,
        InventoryDeltaApplicationPlan<TKey>? plan,
        out InventoryFailure? failure)
    {
        if (operation == null)
        {
            failure = InventoryFailures.Transaction("Delta operation cannot be null.");
            return false;
        }

        if (!_targetInventory.TryResolveRegisteredDefinitionId(operation.DefinitionId, out var definition, out failure) || definition == null)
            return false;

        if (operation.Kind == InventoryItemDeltaOperationKind.Add)
        {
            ILayoutContext<TKey>? context = null;
            if (plan != null && !plan.TryResolvePlacement(_simulation, operation, out context, out failure))
                return false;
            return TryAdd(definition, operation.Amount, context, operation.Metadata, out failure);
        }

        return TryRemoveByDeltaOperation(definition, operation, plan, out failure);
    }

    private bool TryRemoveByDeltaOperation(
        ItemDefinition<TKey> definition,
        InventoryItemDeltaOperation<TKey> operation,
        InventoryDeltaApplicationPlan<TKey>? plan,
        out InventoryFailure? failure)
    {
        failure = null;
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        int remaining = operation.Amount;

        for (int i = 0; i < _simulation.Items.Count && remaining > 0; i++)
        {
            var instance = _simulation.Items[i];
            if (!EqualityComparer<TKey>.Default.Equals(instance.Definition.Id, definition.Id))
                continue;
            if (!MatchesDeltaMetadata(instance.Metadata, operation))
                continue;
            int take = Math.Min(remaining, instance.Amount);
            if (plan != null)
            {
                var contexts = _simulation.Layout.GetContextsForStorageIndex(_simulation, i);
                if (!plan.TryAcceptRemovalCandidate(
                        _simulation,
                        operation,
                        instance,
                        i,
                        take,
                        contexts,
                        out var accepted,
                        out failure))
                    return false;
                if (!accepted)
                    continue;
            }

            remaining -= take;
            if (take == instance.Amount)
                removed.Add((i, instance));
            else
                amountDeltas.Add((i, -take));
        }

        if (remaining > 0)
        {
            failure = InventoryFailures.Validation("Not enough matching items to remove.");
            return false;
        }

        var tx = new InventoryTransaction<TKey>(
            _simulation,
            amountDeltas,
            removed,
            new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!_simulation.TryPrepareTransaction(tx, null, out var mapped, out failure) || mapped == null)
            return false;

        MergeAndApply(mapped);
        return true;
    }

    private static bool MatchesDeltaMetadata(
        InstanceMetadata metadata,
        InventoryItemDeltaOperation<TKey> operation)
    {
        if (operation.MetadataMatch == InventoryItemDeltaMetadataMatch.Any)
            return true;

        var operationMetadata = operation.Metadata;
        if (operationMetadata == null || operationMetadata.IsEmpty)
            return metadata.IsEmpty;

        return metadata.StructuralEquals(operationMetadata);
    }

    private bool TryRemoveByDefinition(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        InventoryItemDeltaMetadataMatch metadataMatch,
        ILayoutContext<TKey>? context,
        out InventoryFailure? failure)
    {
        failure = null;
        if (definition == null)
        {
            failure = InventoryFailures.Definition("Definition cannot be null.");
            return false;
        }
        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }

        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        int remaining = amount;

        for (int i = 0; i < _simulation.Items.Count && remaining > 0; i++)
        {
            var instance = _simulation.Items[i];
            if (!EqualityComparer<TKey>.Default.Equals(instance.Definition.Id, definition.Id))
                continue;
            if (!MatchesMetadata(instance.Metadata, metadata, metadataMatch))
                continue;
            if (context != null && !StorageIndexHasContext(i, context))
                continue;

            int take = Math.Min(remaining, instance.Amount);
            remaining -= take;
            if (take == instance.Amount)
                removed.Add((i, instance));
            else
                amountDeltas.Add((i, -take));
        }

        if (remaining > 0)
        {
            failure = context == null
                ? InventoryFailures.Validation("Not enough matching items to remove.")
                : InventoryFailures.LayoutInvalidContext("Not enough matching items exist at the requested removal context.");
            return false;
        }

        var tx = new InventoryTransaction<TKey>(
            _simulation,
            amountDeltas,
            removed,
            new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!_simulation.TryPrepareTransaction(tx, null, out var mapped, out failure) || mapped == null)
            return false;

        MergeAndApply(mapped);
        return true;
    }

    private static bool MatchesMetadata(
        InstanceMetadata metadata,
        InstanceMetadata? referenceMetadata,
        InventoryItemDeltaMetadataMatch metadataMatch)
    {
        if (metadataMatch == InventoryItemDeltaMetadataMatch.Any)
            return true;

        if (referenceMetadata == null || referenceMetadata.IsEmpty)
            return metadata.IsEmpty;

        return metadata.StructuralEquals(referenceMetadata);
    }

    private bool StorageIndexHasContext(int storageIndex, ILayoutContext<TKey> context)
    {
        var contexts = _simulation.Layout.GetContextsForStorageIndex(_simulation, storageIndex);
        for (int i = 0; i < contexts.Count; i++)
        {
            if (LayoutContextEquals(contexts[i], context))
                return true;
        }

        return false;
    }

    private static bool LayoutContextEquals(ILayoutContext<TKey> first, ILayoutContext<TKey> second)
    {
        if (ReferenceEquals(first, second))
            return true;
        if (first == null || second == null || first.GetType() != second.GetType())
            return false;

        var properties = first.GetType().GetProperties();
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length != 0)
                continue;
            if (!property.CanRead)
                continue;
            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType != typeof(string))
                continue;

            var firstValue = property.GetValue(first);
            var secondValue = property.GetValue(second);
            if (!Equals(firstValue, secondValue))
                return false;
        }

        return true;
    }

    private static void ThrowIfRejected(bool accepted, InventoryFailure? failure)
    {
        if (!accepted)
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Transaction builder operation was rejected."));
    }
}
