using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Builds a bulk transaction by operating on a simulated inventory state.
/// Use <see cref="Inventory{TKey}.CreateTransactionBuilder"/> to create.
/// Add/remove operations update the simulated state; call <see cref="ToInventoryTransaction"/>
/// to get the merged transaction, then <see cref="Inventory{TKey}.CommitTransaction"/> on the original inventory.
/// </summary>
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

    /// <summary>Adds items to the simulated state. Returns false if the operation would fail.</summary>
    public bool TryAdd(ItemDefinition<TKey> definition, out string? error, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        return TryAdd(definition, amount, context, null, out error);
    }

    /// <summary>Adds items with optional metadata. Returns false if the operation would fail.</summary>
    public bool TryAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out string? error)
    {
        error = null;
        if (!_simulation.TryFormulateAdd(definition, amount, context, metadata, out var tx, out error) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>Removes items from the simulated state. Returns false if the operation would fail.
    /// The instance may be from the original inventory; it will be resolved to the matching instance in the simulation.</summary>
    public bool TryRemove(ItemInstance<TKey> instance, out string? error, int amount = 1)
    {
        error = null;
        var simulationInstance = ResolveToSimulationInstance(instance);
        if (simulationInstance == null)
        {
            error = "Item not found in inventory.";
            return false;
        }
        if (!_simulation.TryFormulateRemove(simulationInstance, amount, out var tx, out error) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>Removes items at the given storage index. Returns false if the operation would fail.</summary>
    public bool TryRemoveAtStorageIndex(int index, out string? error, int amount = 1)
    {
        error = null;
        if (!_simulation.TryFormulateRemoveAt(index, amount, out var tx, out error) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>Removes items by definition. Returns false if the operation would fail.</summary>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out string? error)
    {
        error = null;
        if (!_simulation.TryFormulateRemoveByDefinition(definition, amount, ignoreMetadata, out var tx, out error) || tx == null)
            return false;

        MergeAndApply(tx);
        return true;
    }

    /// <summary>
    /// Produces an <see cref="InventoryTransaction{TKey}"/> targeting the original inventory.
    /// Call <see cref="Inventory{TKey}.CommitTransaction"/> with the result.
    /// </summary>
    public InventoryTransaction<TKey> ToInventoryTransaction()
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
