using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Mutable inventory that owns item instances, layout state, capacity validation, stacking behavior, rules, and change events.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class Inventory<TKey>
{
    private readonly List<ItemInstance<TKey>> _items = new();

    private readonly IStackResolver<TKey> _stackResolver;
    private readonly ICapacityPolicy<TKey> _capacityPolicy;
    private readonly IInventoryLayout<TKey> _layout;
    private readonly RuleContainer<TKey> _rules;

    /// <summary>
    /// Gets inventory-level attributes.
    /// </summary>
    public AttributeContainer Attributes { get; } = new();

    /// <summary>
    /// Gets the manager that created this inventory.
    /// </summary>
    public InventoryManager<TKey> Manager { get; }

    /// <summary>
    /// Gets the item catalog from <see cref="Manager"/>.
    /// </summary>
    public ItemCatalog<TKey> Catalog => Manager.Catalog;

    /// <summary>
    /// Occurs after a mutating operation changes inventory contents or layout positions.
    /// </summary>
    /// <remarks>Committed transactions produce a single event containing all grouped changes.</remarks>
    public event EventHandler<InventoryChangedEventArgs<TKey>>? Changed;

    /// <summary>
    /// Creates an inventory with explicit policies, layout, and rules.
    /// </summary>
    /// <param name="manager">The inventory manager that owns the shared catalog.</param>
    /// <param name="stackResolver">The stack resolver used for add and merge operations.</param>
    /// <param name="capacityPolicy">The capacity policy used before committing transactions.</param>
    /// <param name="layout">The layout that maps storage indices to placement positions.</param>
    /// <param name="rules">The rule container used before committing transactions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> is <see langword="null"/>.</exception>
    public Inventory(
        InventoryManager<TKey> manager,
        IStackResolver<TKey> stackResolver,
        ICapacityPolicy<TKey> capacityPolicy,
        IInventoryLayout<TKey> layout,
        RuleContainer<TKey> rules)
    {
        if (manager == null)
            throw new ArgumentNullException("Manager cannot be null");
        Manager = manager;
        _stackResolver = stackResolver;
        _capacityPolicy = capacityPolicy;
        _layout = layout;
        _rules = rules;
    }

    /// <summary>
    /// Gets item instances in inventory storage-index order.
    /// </summary>
    /// <remarks>Storage order is not necessarily the same as visual or layout order.</remarks>
    public IReadOnlyList<ItemInstance<TKey>> Items => _items;

    /// <summary>
    /// Gets the number of item instances or stacks.
    /// </summary>
    public int InstanceCount => _items.Count;

    /// <summary>
    /// Gets the sum of all item instance amounts.
    /// </summary>
    public int TotalItemCount => _items.Sum(i => i.Amount);

    /// <summary>
    /// Gets the layout used by this inventory.
    /// </summary>
    public IInventoryLayout<TKey> Layout => _layout;

    private int GetItemIndex(ItemInstance<TKey> instance)
    {
        return _items.IndexOf(instance);
    }

    private void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        _layout.OnItemRemoved(this, index);
    }

    private void SetAmountAt(int index, int amount)
    {
        if (amount <= 0)
        {
            RemoveAt(index);
            return;
        }

        _items[index].SetAmount(amount);
    }

    private void AddItem(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)
    {
        _items.Add(instance);
        _layout.OnItemAdded(this, _items.Count - 1, context);
    }

    /// <summary>Internal: adds an item and notifies layout without firing Changed. Used for simulation seeding.</summary>
    internal void AddItemSilent(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)
    {
        _items.Add(instance);
        _layout.OnItemAdded(this, _items.Count - 1, context);
    }

    /// <summary>Internal: applies a transaction without firing Changed. Used for simulation state updates.</summary>
    internal void ApplyTransactionSilent(InventoryTransaction<TKey> transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (transaction.Inventory != this)
            throw new InvalidOperationException("Transaction does not belong to this inventory.");
        if (transaction.IsApplied)
            throw new InvalidOperationException("Transaction has already been applied.");

        foreach (var (index, delta) in transaction.AmountDeltas)
            _items[index].AddAmount(delta);

        var removed = new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed);
        removed.Sort((a, b) => b.index.CompareTo(a.index));
        foreach (var (index, _) in removed)
        {
            _items.RemoveAt(index);
            _layout.OnItemRemoved(this, index);
        }

        foreach (var (instance, context) in transaction.Added)
        {
            _items.Add(instance);
            _layout.OnItemAdded(this, _items.Count - 1, context);
        }

        transaction.MarkApplied();
    }

    /// <summary>Internal: creates a simulation clone of this inventory for bulk transaction building.</summary>
    internal static Inventory<TKey> CreateSimulationClone(Inventory<TKey> source)
    {
        var clonedLayout = source._layout.Clone();
        var clone = new Inventory<TKey>(source.Manager, source._stackResolver, source._capacityPolicy, clonedLayout, source._rules);

        foreach (var item in source._items)
        {
            var meta = item.Metadata.IsEmpty ? null : CloneMetadata(item.Metadata);
            var clonedInstance = new ItemInstance<TKey>(item.Definition, item.Amount, meta);
            clone.AddItemSilent(clonedInstance, null);
        }

        return clone;
    }

    /// <summary>
    /// Creates a transaction builder seeded with the current inventory state.
    /// Use for bulk operations that should produce a single Changed event on commit.
    /// </summary>
    /// <returns>A transaction builder that simulates changes against a clone of the current inventory state.</returns>
    public InventoryTransactionBuilder<TKey> CreateTransactionBuilder()
    {
        var simulation = CreateSimulationClone(this);
        return new InventoryTransactionBuilder<TKey>(this, simulation);
    }

    /// <summary>Builds a semantic (normalized) view of the transaction for capacity evaluation. Groups by definition and metadata (structural equality) so e.g. 90 apples and 10 apples[metadata] are distinct.</summary>
    /// <param name="transaction">The structural transaction to normalize.</param>
    /// <returns>A semantic transaction grouped by item definition and structurally equal metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> targets another inventory.</exception>
    public NormalizedInventoryTransaction<TKey> GenerateNormalizedInventoryTransaction(InventoryTransaction<TKey> transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (transaction.Inventory != this)
            throw new InvalidOperationException("Transaction does not belong to this inventory.");

        var addedList = new List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)>();
        var removedList = new List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)>();

        void MergeInto(List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> list, ItemDefinition<TKey> def, InstanceMetadata? meta, int amt)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var (d, m, a) = list[i];
                if (!EqualityComparer<TKey>.Default.Equals(d.Id, def.Id))
                    continue;
                bool metaMatch = (m == null || m.IsEmpty) && (meta == null || meta.IsEmpty)
                    || (m != null && meta != null && m.StructuralEquals(meta));
                if (!metaMatch)
                    continue;
                list[i] = (d, m, a + amt);
                return;
            }
            list.Add((def, meta, amt));
        }

        foreach (var (index, delta) in transaction.AmountDeltas)
        {
            var inst = _items[index];
            var def = inst.Definition;
            var meta = inst.Metadata.IsEmpty ? null : inst.Metadata;
            if (delta > 0)
                MergeInto(addedList, def, meta, delta);
            else
                MergeInto(removedList, def, meta, -delta);
        }

        foreach (var (_, instance) in transaction.Removed)
        {
            var meta = instance.Metadata.IsEmpty ? null : instance.Metadata;
            MergeInto(removedList, instance.Definition, meta, instance.Amount);
        }

        foreach (var (instance, _) in transaction.Added)
        {
            var meta = instance.Metadata.IsEmpty ? null : instance.Metadata;
            MergeInto(addedList, instance.Definition, meta, instance.Amount);
        }

        return new NormalizedInventoryTransaction<TKey>(addedList, removedList);
    }

    /// <summary>Internal: generates a transaction for adding items. Optional metadata groups by definition+metadata (e.g. 10 apples vs 10 apples[metadata]).</summary>
    internal bool TryFormulateAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        transaction = null;
        error = null;
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        var prototypeMeta = metadata != null && !metadata.IsEmpty ? CloneMetadata(metadata) : null;
        var prototype = new ItemInstance<TKey>(definition, 1, prototypeMeta);
        int maxStack = _stackResolver != null
            ? _stackResolver.ResolveMaxStackSize(this, prototype)
            : 1;

        var amountDeltas = new List<(int index, int delta)>();
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        int remaining = amount;

        foreach (int i in _layout.GetMergeCandidates(this, prototype, context))
        {
            if (remaining <= 0) break;
            if (i < 0 || i >= _items.Count) continue;
            var existing = _items[i];
            if (!existing.IsStackCompatible(prototype)) continue;
            int room = maxStack - existing.Amount;
            if (room <= 0) continue;
            int add = Math.Min(remaining, room);
            amountDeltas.Add((i, add));
            remaining -= add;
        }

        while (remaining > 0)
        {
            int chunk = Math.Min(remaining, maxStack);
            var chunkMeta = metadata != null && !metadata.IsEmpty ? CloneMetadata(metadata) : null;
            var instance = new ItemInstance<TKey>(definition, chunk, chunkMeta);
            if (!_layout.CanAcceptNewItem(this, instance, context, out error))
                return false;
            added.Add((instance, context));
            remaining -= chunk;
        }

        var tx = new InventoryTransaction<TKey>(this, amountDeltas, new List<(int index, ItemInstance<TKey> instance)>(), added);
        if (!ValidateTransactionConstraints(tx, context, out error))
            return false;
        transaction = tx;
        return true;
    }

    private static InstanceMetadata CloneMetadata(InstanceMetadata source)
    {
        var clone = new InstanceMetadata();
        if (source != null && !source.IsEmpty)
            clone.RestoreMetadata(new Dictionary<string, object>(source.ToDictionary()));
        return clone;
    }

    internal bool TryFormulateAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, out InventoryTransaction<TKey>? transaction, out string? error)
        => TryFormulateAdd(definition, amount, context, null, out transaction, out error);

    /// <summary>Internal: generates a transaction for removing from a specific instance.</summary>
    internal bool TryFormulateRemove(ItemInstance<TKey> instance, int amount, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        transaction = null;
        error = null;
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }
        int index = _items.IndexOf(instance);
        if (index == -1)
        {
            error = "Item not found in inventory.";
            return false;
        }
        if (instance.Amount < amount)
        {
            error = "Not enough quantity to remove.";
            return false;
        }
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        if (instance.Amount == amount)
            removed.Add((index, instance));
        else
            amountDeltas.Add((index, -amount));
        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!ValidateTransactionConstraints(tx, null, out error))
            return false;
        transaction = tx;
        return true;
    }

    /// <summary>Internal: generates a transaction for removing at a storage index.</summary>
    internal bool TryFormulateRemoveAt(int index, int amount, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        transaction = null;
        error = null;
        if (index < 0 || index >= _items.Count)
        {
            error = "Index out of range.";
            return false;
        }
        var inst = _items[index];
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }
        if (inst.Amount < amount)
        {
            error = "Not enough quantity to remove.";
            return false;
        }
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        if (inst.Amount == amount)
            removed.Add((index, inst));
        else
            amountDeltas.Add((index, -amount));
        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!ValidateTransactionConstraints(tx, null, out error))
            return false;
        transaction = tx;
        return true;
    }

    /// <summary>Internal: generates a transaction for removing by definition. When ignoreMetadata is false, uses first matching instance's metadata as reference.</summary>
    internal bool TryFormulateRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        if (ignoreMetadata)
            return TryFormulateRemoveByDefinition(definition, amount, (InstanceMetadata?)null, out transaction, out error);
        transaction = null;
        error = null;
        if (definition == null) { error = "Definition cannot be null."; return false; }
        if (amount <= 0) { error = "Amount must be greater than zero."; return false; }
        InstanceMetadata? firstMeta = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var inst = _items[i];
            if (!EqualityComparer<TKey>.Default.Equals(inst.Definition.Id, definition.Id)) continue;
            firstMeta = inst.Metadata;
            break;
        }
        return TryFormulateRemoveByDefinition(definition, amount, firstMeta, out transaction, out error);
    }

    /// <summary>Internal: when referenceMetadata is null/empty, any metadata matches; otherwise only instances with structurally equal metadata match.</summary>
    internal bool TryFormulateRemoveByDefinition(ItemDefinition<TKey> definition, int amount, InstanceMetadata? referenceMetadata, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        transaction = null;
        error = null;
        if (definition == null)
        {
            error = "Definition cannot be null.";
            return false;
        }
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        int remaining = amount;
        bool matchMetadata = referenceMetadata != null && !referenceMetadata.IsEmpty;

        for (int i = 0; i < _items.Count && remaining > 0; i++)
        {
            var inst = _items[i];
            if (!EqualityComparer<TKey>.Default.Equals(inst.Definition.Id, definition.Id))
                continue;
            if (matchMetadata && !inst.Metadata.StructuralEquals(referenceMetadata!))
                continue;
            int take = Math.Min(remaining, inst.Amount);
            remaining -= take;
            if (inst.Amount == take)
                removed.Add((i, inst));
            else
                amountDeltas.Add((i, -take));
        }

        if (remaining > 0)
        {
            error = "Not enough matching items to remove.";
            return false;
        }

        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!ValidateTransactionConstraints(tx, null, out error))
            return false;
        transaction = tx;
        return true;
    }

    private bool ValidateTransactionConstraints(InventoryTransaction<TKey> tx, ILayoutContext<TKey>? context, out string? error)
    {
        error = null;
        var normalized = GenerateNormalizedInventoryTransaction(tx);
        if (!_capacityPolicy.CanApply(this, normalized, out error))
            return false;
        if (!_rules.CanApply(this, normalized, tx, out error))
            return false;
        if (!_layout.CanSatisfyPlacement(this, tx, context, out error))
            return false;
        return true;
    }

    /// <summary>Converts a normalized (semantic) transaction into an inventory-specific structural transaction. Public for custom policies and cross-inventory use. Supports single add and/or single remove; multiple definitions may require multiple calls.</summary>
    /// <param name="normalized">The semantic transaction to convert.</param>
    /// <param name="transaction">The structural transaction when conversion succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A consumer-facing reason when conversion is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
    public bool TryFormulateFromNormalized(NormalizedInventoryTransaction<TKey> normalized, out InventoryTransaction<TKey>? transaction, out string? error)
    {
        transaction = null;
        error = null;
        if (normalized == null)
        {
            error = "Normalized transaction cannot be null.";
            return false;
        }
        if (normalized.IsEmpty)
        {
            error = "Normalized transaction is empty.";
            return false;
        }

        if (normalized.Removed.Count == 0 && normalized.Added.Count == 1)
        {
            var (def, meta, amount) = normalized.Added[0];
            return TryFormulateAdd(def, amount, null, meta, out transaction, out error);
        }
        if (normalized.Added.Count == 0 && normalized.Removed.Count == 1)
        {
            var (def, meta, amount) = normalized.Removed[0];
            return TryFormulateRemoveByDefinition(def, amount, meta, out transaction, out error);
        }
        if (normalized.Added.Count == 0 && normalized.Removed.Count > 1)
        {
            error = "Normalized transaction with multiple removed definitions is not yet supported for conversion; use a single removed definition.";
            return false;
        }

        error = "Normalized transaction with multiple added definitions is not yet supported for conversion; use single-definition adds or remove-only.";
        return false;
    }

    /// <summary>Merges two structural transactions so that applying the result is equivalent to applying first then second. Second was formulated against the state after first.</summary>
    internal static InventoryTransaction<TKey> MergeTransactions(InventoryTransaction<TKey> first, InventoryTransaction<TKey> second)
    {
        if (first.Inventory != second.Inventory)
            throw new InvalidOperationException("Cannot merge transactions for different inventories.");
        int n = first.Inventory.Items.Count;
        var firstRemovedIndices = new HashSet<int>();
        foreach (var (idx, _) in first.Removed)
            firstRemovedIndices.Add(idx);
        int removedCount = first.Removed.Count;
        var afterFirstIndexToOriginal = new List<int>();
        for (int i = 0; i < n; i++)
            if (!firstRemovedIndices.Contains(i))
                afterFirstIndexToOriginal.Add(i);

        var mergedDeltas = new List<(int index, int delta)>(first.AmountDeltas);
        foreach (var (afterIndex, delta) in second.AmountDeltas)
        {
            if (afterIndex < afterFirstIndexToOriginal.Count)
                mergedDeltas.Add((afterFirstIndexToOriginal[afterIndex], delta));
        }
        var mergedRemoved = new List<(int index, ItemInstance<TKey> instance)>(first.Removed);
        foreach (var (afterIndex, instance) in second.Removed)
        {
            if (afterIndex < afterFirstIndexToOriginal.Count)
                mergedRemoved.Add((afterFirstIndexToOriginal[afterIndex], instance));
        }
        var mergedAdded = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>(first.Added);
        mergedAdded.AddRange(second.Added);

        return new InventoryTransaction<TKey>(first.Inventory, mergedDeltas, mergedRemoved, mergedAdded);
    }

    /// <summary>Executes a transaction. Transaction must reference this inventory and must not already be applied. Fires a single Changed event.</summary>
    /// <param name="transaction">The structural transaction to commit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> targets another inventory or has already been applied.</exception>
    public void CommitTransaction(InventoryTransaction<TKey> transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (transaction.Inventory != this)
            throw new InvalidOperationException("Transaction does not belong to this inventory.");
        if (transaction.IsApplied)
            throw new InvalidOperationException("Transaction has already been applied.");

        var changedEventArgs = new InventoryChangedEventArgs<TKey>();

        foreach (var (index, delta) in transaction.AmountDeltas)
        {
            _items[index].AddAmount(delta);
            changedEventArgs.Modified.Add(new ItemModified<TKey>(_items[index], index));
        }

        var removed = new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed);
        removed.Sort((a, b) => b.index.CompareTo(a.index));
        foreach (var (index, instance) in removed)
        {
            RemoveAt(index);
            changedEventArgs.Removed.Add(new ItemRemoved<TKey>(instance, index));
        }

        foreach (var (instance, context) in transaction.Added)
        {
            AddItem(instance, context);
            changedEventArgs.Added.Add(new ItemAdded<TKey>(instance, _items.Count - 1));
        }

        transaction.MarkApplied();

        bool hasChanges = transaction.AmountDeltas.Count > 0 || transaction.Removed.Count > 0 || transaction.Added.Count > 0;
        if (hasChanges)
            Changed?.Invoke(this, changedEventArgs);
    }

    /// <summary>
    /// Attempts to add an amount of an item definition to the inventory.
    /// </summary>
    /// <param name="definition">The item definition to add.</param>
    /// <param name="error">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns><see langword="true"/> when the item is added and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(ItemDefinition<TKey> definition, out string? error, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!TryFormulateAdd(definition, amount, context, out var tx, out error) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to remove an amount from a specific item instance.
    /// </summary>
    /// <param name="instance">The item instance to remove from.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(ItemInstance<TKey> instance, out string? error, int amount = 1)
    {
        if (!TryFormulateRemove(instance, amount, out var tx, out error) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to remove an amount from the item at a storage index.
    /// </summary>
    /// <param name="index">The storage index to remove from.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtStorageIndex(int index, out string? error, int amount = 1)
    {
        if (!TryFormulateRemoveAt(index, amount, out var tx, out error) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to remove an amount by item definition.
    /// </summary>
    /// <param name="definition">The item definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="ignoreMetadata">Whether metadata should be ignored when selecting matching instances.</param>
    /// <param name="error">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, bool ignoreMetadata, out string? error)
    {
        if (!TryFormulateRemoveByDefinition(definition, amount, ignoreMetadata, out var tx, out error) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to move an item between two layout contexts.
    /// </summary>
    /// <param name="contextFrom">The source layout context.</param>
    /// <param name="contextTo">The destination layout context.</param>
    /// <param name="error">A consumer-facing reason when the move is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the move succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryMove(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        var item = _layout.GetAt(this, contextFrom);

        if (item == null)
        {
            error = "Item not found in inventory.";
            return false;
        }

        if (!_layout.TryMove(this, contextFrom, contextTo, out error))
            return false;


        var changedEventArgs = new InventoryChangedEventArgs<TKey>(
            moved: new List<ItemMoved<TKey>>
            {
                new ItemMoved<TKey>(item, contextFrom, contextTo)
            }
        );

        Changed?.Invoke(this, changedEventArgs);

        return true;
    }

    /// <summary>
    /// Attempts to swap two items between layout contexts.
    /// </summary>
    /// <param name="contextFrom">The first layout context.</param>
    /// <param name="contextTo">The second layout context.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the swap succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TrySwap(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error)
    {
        error = null;

        var itemFrom = _layout.GetAt(this, contextFrom);
        var itemTo = _layout.GetAt(this, contextTo);

        if (itemFrom == null || itemTo == null)
        {
            error = "One or both of the items not found in inventory.";
            return false;
        }

        if (!_layout.TrySwap(this, contextFrom, contextTo, out error))
        {
            return false;
        }

        var changedEventArgs = new InventoryChangedEventArgs<TKey>(
            swapped: new List<ItemSwapped<TKey>>
            {
                new ItemSwapped<TKey>(contextFrom, contextTo, itemTo, itemFrom) // Swapped items to reflect where the items are after the swap
            }
        );

        Changed?.Invoke(this, changedEventArgs);

        return true;
    }

    /// <summary>
    /// Attempts to move an amount from one compatible stack into another.
    /// </summary>
    /// <param name="contextFrom">The source stack layout context.</param>
    /// <param name="contextTo">The destination stack layout context.</param>
    /// <param name="error">A consumer-facing reason when the merge move is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">Optional exact amount to move; when omitted, as much as possible is moved.</param>
    /// <returns><see langword="true"/> when the merge move succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryMergeMove(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out string? error, int? amount = null)
    {
        error = null;

        var itemFrom = _layout.GetAt(this, contextFrom);
        var itemTo = _layout.GetAt(this, contextTo);

        if (itemFrom == null || itemTo == null)
        {
            error = "One or both of the items not found in inventory.";
            return false;
        }

        if (!itemFrom.IsStackCompatible(itemTo))
        {
            error = "Items are not stack compatible.";
            return false;
        }

        if (amount.HasValue && amount.Value <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        if (amount.HasValue && amount.Value > itemFrom.Amount)
        {
            error = "Not enough quantity to move.";
            return false;
        }

        int targetStack = itemTo.Amount;
        int targetMaxStack = _stackResolver.ResolveMaxStackSize(this, itemTo);

        if (targetStack == targetMaxStack)
        {
            error = "Target stack is already at max size, no items can be moved.";
            return false;
        }

        int room = targetMaxStack - targetStack;

        if (amount.HasValue && room < amount.Value)
        {
            error = "Not enough room in target stack to move the requested amount.";
            return false;
        }

        int amountToMove = amount.HasValue ? amount.Value : Math.Min(room, itemFrom.Amount);
        List<(int index, int delta)> amountDeltas = new List<(int index, int delta)>()
        {
            (GetItemIndex(itemTo), amountToMove)
        };

        bool allItemsMoved = amountToMove == itemFrom.Amount;
        List<(int index, ItemInstance<TKey> instance)> removed = new();
        if (allItemsMoved)
            removed.Add((GetItemIndex(itemFrom), itemFrom));
        else
            amountDeltas.Add((GetItemIndex(itemFrom), -amountToMove));

        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!ValidateTransactionConstraints(tx, null, out error))
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Removes all item instances from the inventory.
    /// </summary>
    /// <remarks>A change event is fired only when the inventory was not already empty.</remarks>
    public void Clear()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        _layout.OnInventoryCleared(this);
        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(cleared: true));
    }

    /// <summary>
    /// Replaces the entire inventory with the given entries (e.g. for load/restore).
    /// Clears current contents, then adds each entry with its optional layout context.
    /// Caller must ensure entries are valid for the current capacity and layout.
    /// Fires a single Changed event for the entire replacement.
    /// </summary>
    /// <param name="entries">Definitions, amounts, and layout contexts to add after clearing current contents.</param>
    public void ReplaceContents(IEnumerable<(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey> context)> entries)
    {
        Clear();
        if (entries == null)
            return;

        var builder = CreateTransactionBuilder();
        foreach (var (definition, amount, context) in entries)
        {
            if (definition == null || amount <= 0)
                continue;
            builder.TryAdd(definition, out _, amount, context);
        }
        CommitTransaction(builder.ToInventoryTransaction());
    }

    /// <summary>
    /// Serializes item instances and layout data for persistence.
    /// </summary>
    /// <returns>A serialized inventory snapshot.</returns>
    public SerializedInventory<TKey> Serialize()
    {
        var serialized = new SerializedInventory<TKey>();

        foreach (var item in _items)
        {
            serialized.Items.Add(new SerializedItem<TKey>
            {
                DefinitionId = item.Definition.Id,
                Amount = item.Amount,
                Metadata = item.Metadata.ToDictionary()
            });
        }

        serialized.LayoutData = _layout.GetPersistentData();

        return serialized;
    }

    /// <summary>
    /// Replaces inventory contents from serialized item and layout data.
    /// </summary>
    /// <param name="data">The serialized inventory snapshot to restore.</param>
    /// <param name="strict">Whether failed item restoration should throw instead of being skipped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="strict"/> is <see langword="true"/> and an item cannot be restored, or layout data is invalid for this layout.
    /// </exception>
    public void Deserialize(SerializedInventory<TKey> data, bool strict = false)
    {
        if (data == null)
            throw new ArgumentNullException("Data cannot be null");

        Clear();

        var builder = CreateTransactionBuilder();
        foreach (var serializedItem in data.Items)
        {
            var definition = Manager.Registry.Resolve(serializedItem.DefinitionId);

            InstanceMetadata? metadata = null;
            if (serializedItem.Metadata != null && serializedItem.Metadata.Count > 0)
            {
                metadata = new InstanceMetadata();
                metadata.RestoreMetadata(serializedItem.Metadata);
            }

            builder.TryAdd(definition, serializedItem.Amount, null, metadata, out var error);
            if (strict && error != null)
                throw new InvalidOperationException($"Failed to deserialize item {serializedItem.DefinitionId}: {error}");
        }
        CommitTransaction(builder.ToInventoryTransaction());

        _layout.RestorePersistentData(data.LayoutData as ILayoutPersistentData);
    }
}
