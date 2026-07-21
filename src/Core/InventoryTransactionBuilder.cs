using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds a bulk structural transaction by operating on a simulated inventory state.
/// Use <see cref="InventoryTransaction{TKey}.From"/> to create a builder, then commit it through the target inventory.
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

        for (int i = 0; i < _simulation.Items.Count; i++)
            _simulationEntries.Add(new SimulationEntry(i, null));
    }

    /// <summary>
    /// Gets whether the staged operations would produce an empty transaction.
    /// </summary>
    public bool IsEmpty => Build().IsEmpty;

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
        if (!_simulation.TryFormulateRemove(simulationInstance, amount, out var tx, out failure) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
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
    /// Builds an <see cref="InventoryTransaction{TKey}"/> targeting the original inventory.
    /// </summary>
    /// <remarks>
    /// Most callers should commit the builder directly through <see cref="Inventory{TKey}.CommitTransaction(InventoryTransactionBuilder{TKey})"/>
    /// or <see cref="Inventory{TKey}.TryCommitTransaction(InventoryTransactionBuilder{TKey}, out InventoryFailure?)"/>.
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

        return new InventoryTransaction<TKey>(_targetInventory, amountDeltas, removed, added);
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
}
