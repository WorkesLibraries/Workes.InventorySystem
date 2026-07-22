using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Persistence;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Mutable inventory that owns item instances, layout state, capacity validation, stacking behavior, rules, and change events.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public partial class Inventory<TKey> : IInstanceMetadataOwner, IInventoryMetadataOwner
{
    private readonly List<ItemInstance<TKey>> _items = new();

    private IStackResolver<TKey> _stackResolver;
    private ICapacityPolicy<TKey> _capacityPolicy;
    private IInventoryLayout<TKey> _layout;
    private readonly RuleContainer<TKey> _rules;
    private long _version;

    internal long Version => _version;

    private sealed class ProposedItemState
    {
        public ProposedItemState(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata)
        {
            Definition = definition;
            Amount = amount;
            Metadata = metadata;
        }

        public ItemDefinition<TKey> Definition { get; }

        public int Amount { get; }

        public InstanceMetadata? Metadata { get; }
    }

    private sealed class StackMutationEntry
    {
        public StackMutationEntry(
            int? originalIndex,
            int layoutOrder,
            int splitSequence,
            ItemDefinition<TKey> definition,
            int originalAmount,
            int amount,
            InstanceMetadata? metadata)
        {
            OriginalIndex = originalIndex;
            LayoutOrder = layoutOrder;
            SplitSequence = splitSequence;
            Definition = definition;
            OriginalAmount = originalAmount;
            Amount = amount;
            Metadata = metadata;
        }

        public int? OriginalIndex { get; }

        public int LayoutOrder { get; }

        public int SplitSequence { get; }

        public ItemDefinition<TKey> Definition { get; }

        public int OriginalAmount { get; }

        public int Amount { get; set; }

        public InstanceMetadata? Metadata { get; }
    }

    /// <summary>Gets schema-free, portable metadata owned by this inventory.</summary>
    public InventoryMetadata Metadata { get; } = new();

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
            throw new ArgumentNullException(nameof(manager));
        Manager = manager;
        _stackResolver = stackResolver;
        _capacityPolicy = capacityPolicy;
        _layout = layout;
        _rules = rules;
        Metadata.AttachOwner(this);
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

    /// <summary>
    /// Gets the stack resolver used by this inventory.
    /// </summary>
    public IStackResolver<TKey> StackResolver => _stackResolver;

    /// <summary>
    /// Gets the capacity policy used by this inventory.
    /// </summary>
    public ICapacityPolicy<TKey> CapacityPolicy => _capacityPolicy;

    /// <summary>
    /// Gets the inventory-owned rules by rule id.
    /// </summary>
    public IReadOnlyDictionary<string, IRulePolicy<TKey>> Rules => _rules.Rules;

    /// <summary>
    /// Gets the maximum stack size for the registered item definition without per-instance metadata.
    /// </summary>
    /// <param name="definition">The registered definition to evaluate.</param>
    /// <returns>The maximum amount one compatible stack may contain.</returns>
    /// <exception cref="InventoryOperationException">The definition is not registered, or stack-size resolution fails.</exception>
    public int GetMaxStackSize(ItemDefinition<TKey> definition)
    {
        if (!TryGetMaxStackSize(definition, out var maxStack, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return maxStack;
    }

    /// <summary>
    /// Gets the maximum stack size for the definition resolved from a current or migrated definition id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <returns>The maximum amount one compatible stack may contain.</returns>
    /// <exception cref="InventoryOperationException">The definition id cannot be resolved, or stack-size resolution fails.</exception>
    public int GetMaxStackSize(TKey definitionId)
    {
        if (!TryGetMaxStackSize(definitionId, out var maxStack, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return maxStack;
    }

    /// <summary>
    /// Gets the maximum stack size for a registered definition and proposed per-instance metadata.
    /// </summary>
    /// <param name="definition">The registered definition to evaluate.</param>
    /// <param name="metadata">Optional proposed per-instance metadata.</param>
    /// <returns>The maximum amount one compatible stack may contain.</returns>
    /// <exception cref="InventoryOperationException">The definition is not registered, metadata is invalid, or stack-size resolution fails.</exception>
    public int GetMaxStackSize(ItemDefinition<TKey> definition, InstanceMetadata? metadata)
    {
        if (!TryGetMaxStackSize(definition, metadata, out var maxStack, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return maxStack;
    }

    /// <summary>
    /// Gets the maximum stack size for a resolved definition id and proposed per-instance metadata.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="metadata">Optional proposed per-instance metadata.</param>
    /// <returns>The maximum amount one compatible stack may contain.</returns>
    /// <exception cref="InventoryOperationException">The definition id cannot be resolved, metadata is invalid, or stack-size resolution fails.</exception>
    public int GetMaxStackSize(TKey definitionId, InstanceMetadata? metadata)
    {
        if (!TryGetMaxStackSize(definitionId, metadata, out var maxStack, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return maxStack;
    }

    /// <summary>
    /// Gets the maximum stack size for an item instance or prototype, including its metadata.
    /// </summary>
    /// <param name="item">The item instance or prototype to evaluate.</param>
    /// <returns>The maximum amount one compatible stack may contain.</returns>
    /// <exception cref="InventoryOperationException">The instance definition is not registered, or stack-size resolution fails.</exception>
    public int GetMaxStackSize(ItemInstance<TKey> item)
    {
        if (!TryGetMaxStackSize(item, out var maxStack, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return maxStack;
    }

    /// <summary>
    /// Attempts to get the maximum stack size for the registered item definition without per-instance metadata.
    /// </summary>
    public bool TryGetMaxStackSize(
        ItemDefinition<TKey> definition,
        out int maxStack,
        out InventoryFailure? failure) =>
        TryGetMaxStackSize(definition, metadata: null, out maxStack, out failure);

    /// <summary>
    /// Attempts to get the maximum stack size for the definition resolved from a current or migrated definition id.
    /// </summary>
    public bool TryGetMaxStackSize(
        TKey definitionId,
        out int maxStack,
        out InventoryFailure? failure)
    {
        maxStack = 0;
        if (!TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryGetMaxStackSize(definition, metadata: null, out maxStack, out failure);
    }

    /// <summary>
    /// Attempts to get the maximum stack size for a registered definition and proposed per-instance metadata.
    /// </summary>
    public bool TryGetMaxStackSize(
        ItemDefinition<TKey> definition,
        InstanceMetadata? metadata,
        out int maxStack,
        out InventoryFailure? failure)
    {
        maxStack = 0;
        if (!TryValidateRegisteredDefinition(definition, out failure))
            return false;

        var prototypeMetadata = metadata != null && !metadata.IsEmpty
            ? CloneMetadata(metadata)
            : null;
        var prototype = new ItemInstance<TKey>(definition, amount: 1, prototypeMetadata);
        return TryResolveMaxStackSize(prototype, out maxStack, out failure);
    }

    /// <summary>
    /// Attempts to get the maximum stack size for a resolved definition id and proposed per-instance metadata.
    /// </summary>
    public bool TryGetMaxStackSize(
        TKey definitionId,
        InstanceMetadata? metadata,
        out int maxStack,
        out InventoryFailure? failure)
    {
        maxStack = 0;
        if (!TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryGetMaxStackSize(definition, metadata, out maxStack, out failure);
    }

    /// <summary>
    /// Attempts to get the maximum stack size for an item instance or prototype, including its metadata.
    /// </summary>
    public bool TryGetMaxStackSize(
        ItemInstance<TKey> item,
        out int maxStack,
        out InventoryFailure? failure)
    {
        maxStack = 0;
        if (item == null)
        {
            failure = InventoryFailures.Validation("Item instance cannot be null.");
            return false;
        }

        return TryGetMaxStackSize(
            item.Definition,
            item.Metadata.IsEmpty ? null : item.Metadata,
            out maxStack,
            out failure);
    }

    /// <summary>
    /// Counts the total amount of items that use the exact item definition instance.
    /// </summary>
    /// <param name="definition">The item definition instance to count.</param>
    /// <returns>The summed amount across matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public int Count(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        int count = 0;
        foreach (var item in _items)
        {
            if (ReferenceEquals(item.Definition, definition))
                count += item.Amount;
        }

        return count;
    }

    /// <summary>
    /// Counts the total amount of items that use the exact item definition instance and match item metadata.
    /// </summary>
    /// <param name="definition">The item definition instance to count.</param>
    /// <param name="metadataMatch">How item metadata should be matched.</param>
    /// <returns>The summed amount across matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public int Count(ItemDefinition<TKey> definition, ItemMetadataMatch metadataMatch)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        int count = 0;
        foreach (var item in _items)
        {
            if (ReferenceEquals(item.Definition, definition) && metadataMatch.Matches(item.Metadata))
                count += item.Amount;
        }

        return count;
    }

    /// <summary>
    /// Counts the total amount of items that use the exact item definition instance and structurally equal metadata.
    /// </summary>
    public int Count(ItemDefinition<TKey> definition, InstanceMetadata? metadata) =>
        Count(definition, ItemMetadataMatch.Exact(metadata));

    /// <summary>
    /// Counts the total amount of items whose definition resolves from a current or migrated definition id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <returns>The summed amount across matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No registered definition can be resolved from <paramref name="definitionId"/>.</exception>
    public int Count(TKey definitionId) =>
        Count(ResolveRegisteredDefinitionId(definitionId));

    /// <summary>
    /// Counts the total amount of items whose definition resolves from an id and whose metadata matches the selector.
    /// </summary>
    public int Count(TKey definitionId, ItemMetadataMatch metadataMatch) =>
        Count(ResolveRegisteredDefinitionId(definitionId), metadataMatch);

    /// <summary>
    /// Counts the total amount of items whose definition resolves from an id and whose metadata structurally equals the supplied metadata.
    /// </summary>
    public int Count(TKey definitionId, InstanceMetadata? metadata) =>
        Count(ResolveRegisteredDefinitionId(definitionId), metadata);

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount of the exact item definition instance.
    /// </summary>
    /// <param name="definition">The item definition instance to search for.</param>
    /// <param name="amount">The minimum amount required.</param>
    /// <returns><see langword="true"/> when the requested amount exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than or equal to zero.</exception>
    public bool Contains(ItemDefinition<TKey> definition, int amount = 1)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");

        return Count(definition) >= amount;
    }

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount matching a definition and item metadata selector.
    /// </summary>
    public bool Contains(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");

        return Count(definition, metadataMatch) >= amount;
    }

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount matching a definition and exact metadata.
    /// </summary>
    public bool Contains(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata) =>
        Contains(definition, amount, ItemMetadataMatch.Exact(metadata));

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount of the definition resolved from an id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="amount">The minimum amount required.</param>
    /// <returns><see langword="true"/> when the requested amount exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is less than or equal to zero.</exception>
    /// <exception cref="InvalidOperationException">No registered definition can be resolved from <paramref name="definitionId"/>.</exception>
    public bool Contains(TKey definitionId, int amount = 1) =>
        Contains(ResolveRegisteredDefinitionId(definitionId), amount);

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount matching a resolved definition id and item metadata selector.
    /// </summary>
    public bool Contains(TKey definitionId, int amount, ItemMetadataMatch metadataMatch) =>
        Contains(ResolveRegisteredDefinitionId(definitionId), amount, metadataMatch);

    /// <summary>
    /// Determines whether the inventory contains at least the requested amount matching a resolved definition id and exact metadata.
    /// </summary>
    public bool Contains(TKey definitionId, int amount, InstanceMetadata? metadata) =>
        Contains(ResolveRegisteredDefinitionId(definitionId), amount, metadata);

    /// <summary>
    /// Finds item instances that use the exact item definition instance.
    /// </summary>
    /// <param name="definition">The item definition instance to search for.</param>
    /// <returns>A snapshot list of matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ItemInstance<TKey>> Find(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        return FindWhere(item => ReferenceEquals(item.Definition, definition));
    }

    /// <summary>
    /// Finds item instances that use the exact item definition instance and match item metadata.
    /// </summary>
    public IReadOnlyList<ItemInstance<TKey>> Find(ItemDefinition<TKey> definition, ItemMetadataMatch metadataMatch)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        return FindWhere(item => ReferenceEquals(item.Definition, definition) && metadataMatch.Matches(item.Metadata));
    }

    /// <summary>
    /// Finds item instances that use the exact item definition instance and structurally equal metadata.
    /// </summary>
    public IReadOnlyList<ItemInstance<TKey>> Find(ItemDefinition<TKey> definition, InstanceMetadata? metadata) =>
        Find(definition, ItemMetadataMatch.Exact(metadata));

    /// <summary>
    /// Finds item instances whose definition resolves from a current or migrated definition id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <returns>A snapshot list of matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitionId"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No registered definition can be resolved from <paramref name="definitionId"/>.</exception>
    public IReadOnlyList<ItemInstance<TKey>> Find(TKey definitionId) =>
        Find(ResolveRegisteredDefinitionId(definitionId));

    /// <summary>
    /// Finds item instances whose definition resolves from an id and whose metadata matches the selector.
    /// </summary>
    public IReadOnlyList<ItemInstance<TKey>> Find(TKey definitionId, ItemMetadataMatch metadataMatch) =>
        Find(ResolveRegisteredDefinitionId(definitionId), metadataMatch);

    /// <summary>
    /// Finds item instances whose definition resolves from an id and whose metadata structurally equals the supplied metadata.
    /// </summary>
    public IReadOnlyList<ItemInstance<TKey>> Find(TKey definitionId, InstanceMetadata? metadata) =>
        Find(ResolveRegisteredDefinitionId(definitionId), metadata);


    /// <summary>
    /// Finds item instances whose definitions satisfy a catalog-resolved tag.
    /// </summary>
    /// <param name="tagId">The tag id to resolve through this inventory's catalog.</param>
    /// <returns>A snapshot list of matching item instances.</returns>
    /// <exception cref="InvalidOperationException">The tag is not declared in the catalog.</exception>
    public IReadOnlyList<ItemInstance<TKey>> FindByTag(string tagId)
    {
        if (tagId == null)
            throw new ArgumentNullException(nameof(tagId));

        return FindWhere(item => Catalog.Satisfies(item.Definition, tagId));
    }

    /// <summary>
    /// Counts the total amount of items whose definitions satisfy a catalog-resolved tag.
    /// </summary>
    /// <param name="tagId">The tag id to resolve through this inventory's catalog.</param>
    /// <returns>The summed amount across matching item instances.</returns>
    /// <exception cref="InvalidOperationException">The tag is not declared in the catalog.</exception>
    public int CountByTag(string tagId)
    {
        if (tagId == null)
            throw new ArgumentNullException(nameof(tagId));

        int count = 0;
        foreach (var item in _items)
        {
            if (Catalog.Satisfies(item.Definition, tagId))
                count += item.Amount;
        }

        return count;
    }

    /// <summary>
    /// Determines whether any item definition in the inventory satisfies every provided catalog-resolved tag.
    /// </summary>
    /// <param name="tagIds">The tag ids that one item definition must satisfy.</param>
    /// <returns><see langword="true"/> when at least one item satisfies every tag; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="tagIds"/> is null, empty, or contains invalid ids.</exception>
    public bool ContainsAllTags(params string[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            throw new ArgumentException("At least one tag is required.", nameof(tagIds));

        foreach (var tagId in tagIds)
        {
            if (tagId == null)
                throw new ArgumentException("Tags cannot contain null.", nameof(tagIds));
        }

        foreach (var tagId in tagIds)
        {
            Catalog.Tags.GetKey(tagId);
        }

        foreach (var item in _items)
        {
            bool satisfiesAll = true;
            foreach (var tagId in tagIds)
            {
                if (!Catalog.Satisfies(item.Definition, tagId))
                {
                    satisfiesAll = false;
                    break;
                }
            }

            if (satisfiesAll)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds item instances that match a predicate.
    /// </summary>
    /// <param name="predicate">The predicate used to select item instances.</param>
    /// <returns>A snapshot list of matching item instances.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ItemInstance<TKey>> FindWhere(Func<ItemInstance<TKey>, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var matches = new List<ItemInstance<TKey>>();
        foreach (var item in _items)
        {
            if (predicate(item))
                matches.Add(item);
        }

        return matches;
    }

    private int GetItemIndex(ItemInstance<TKey> instance)
    {
        return _items.IndexOf(instance);
    }

    private void RemoveAt(int index)
    {
        _items[index].DetachOwner(this);
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
        instance.AttachOwner(this);
        _layout.OnItemAdded(this, _items.Count - 1, context);
    }

    /// <summary>Internal: adds an item and notifies layout without firing Changed. Used for simulation seeding.</summary>
    internal void AddItemSilent(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)
    {
        _items.Add(instance);
        instance.AttachOwner(this);
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
            _items[index].DetachOwner(this);
            _items.RemoveAt(index);
            _layout.OnItemRemoved(this, index);
        }

        foreach (var (instance, context) in transaction.Added)
        {
            _items.Add(instance);
            instance.AttachOwner(this);
            _layout.OnItemAdded(this, _items.Count - 1, context);
        }

        ReconcileLayoutAfterMutation();
        transaction.MarkApplied();
    }

    /// <summary>Internal: creates a simulation clone of this inventory for bulk transaction building.</summary>
    internal static Inventory<TKey> CreateSimulationClone(Inventory<TKey> source)
    {
        var clonedLayout = source._layout.Clone();
        var clone = new Inventory<TKey>(source.Manager, source._stackResolver, source._capacityPolicy, clonedLayout, source._rules.Clone());
        clone.Metadata.ReplaceDirect(source.Metadata);

        foreach (var item in source._items)
        {
            var meta = item.Metadata.IsEmpty ? null : CloneMetadata(item.Metadata);
            var clonedInstance = new ItemInstance<TKey>(item.Definition, item.Amount, meta);
            clone._items.Add(clonedInstance);
            clonedInstance.AttachOwner(clone);
        }

        return clone;
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
    internal bool TryFormulateAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, InstanceMetadata? metadata, out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
    {
        transaction = null;
        failure = null;
        if (!TryValidateRegisteredDefinition(definition, out failure))
            return false;

        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }

        var prototypeMeta = metadata != null && !metadata.IsEmpty ? CloneMetadata(metadata) : null;
        var prototype = new ItemInstance<TKey>(definition, 1, prototypeMeta);
        if (!TryResolveMaxStackSize(prototype, out int maxStack, out failure))
            return false;

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
            if (!_layout.CanAcceptNewItem(this, instance, context, out failure))
                return false;
            added.Add((instance, context));
            remaining -= chunk;
        }

        var tx = new InventoryTransaction<TKey>(this, amountDeltas, new List<(int index, ItemInstance<TKey> instance)>(), added);
        if (!TryPrepareTransaction(tx, null, out var mappedTx, out failure) || mappedTx == null)
            return false;
        transaction = mappedTx;
        return true;
    }

    private static InstanceMetadata CloneMetadata(InstanceMetadata source) =>
        source.Clone();

    private bool TryValidateRegisteredDefinition(ItemDefinition<TKey>? definition, out InventoryFailure? failure)
    {
        if (definition == null)
        {
            failure = InventoryFailures.Definition("Item definition cannot be null.");
            return false;
        }

        if (definition.Id == null || !Manager.Registry.TryGet(definition.Id, out var registeredDefinition))
        {
            failure = InventoryFailures.Definition($"Item definition '{definition.Id}' is not registered in this inventory's item catalog.");
            return false;
        }

        if (!ReferenceEquals(registeredDefinition, definition))
        {
            failure = InventoryFailures.Definition($"Item definition '{definition.Id}' is not the registered definition instance for this inventory's item catalog.");
            return false;
        }

        failure = null;
        return true;
    }

    internal bool TryResolveRegisteredDefinitionId(
        TKey definitionId,
        out ItemDefinition<TKey>? definition,
        out InventoryFailure? failure)
    {
        definition = null;

        if (definitionId == null)
        {
            failure = InventoryFailures.Definition("Item definition id cannot be null.");
            return false;
        }

        try
        {
            definition = Manager.Registry.Resolve(definitionId);
            failure = null;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            failure = InventoryFailures.DefinitionUnresolved(ex.Message);
            return false;
        }
    }

    private ItemDefinition<TKey> ResolveRegisteredDefinitionId(TKey definitionId)
    {
        if (!TryResolveRegisteredDefinitionId(definitionId, out var definition, out var failure) || definition == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        return definition;
    }

    private bool TryValidateTransactionDefinitions(InventoryTransaction<TKey> transaction, out InventoryFailure? failure)
    {
        foreach (var (instance, _) in transaction.Added)
        {
            if (!TryValidateRegisteredDefinition(instance.Definition, out failure))
                return false;
        }

        failure = null;
        return true;
    }

    internal bool TryApplyMetadataMutation(
        InstanceMetadata metadata,
        InstanceMetadata proposedMetadata,
        out InventoryFailure? failure)
    {
        if (metadata == null)
        {
            failure = InventoryFailures.Metadata("Metadata cannot be null.");
            return false;
        }

        int index = -1;
        ItemInstance<TKey>? item = null;
        for (int i = 0; i < _items.Count; i++)
        {
            if (ReferenceEquals(_items[i].Metadata, metadata))
            {
                index = i;
                item = _items[i];
                break;
            }
        }

        if (item == null)
        {
            failure = InventoryFailures.Metadata("Metadata does not belong to this inventory.");
            return false;
        }

        var beforeMetadata = metadata.ToDictionary();

        var proposedInstance = new ItemInstance<TKey>(
            item.Definition,
            item.Amount,
            proposedMetadata.IsEmpty ? null : proposedMetadata);
        if (!TryResolveMaxStackSize(proposedInstance, out int maxStack, out failure))
            return false;

        if (item.Amount > maxStack)
        {
            failure = InventoryFailures.Metadata($"Metadata mutation would make stack amount {item.Amount} exceed maximum stack size {maxStack}.");
            return false;
        }

        _layout.TryGetContextForStorageIndex(this, index, out var context);
        var transaction = new InventoryTransaction<TKey>(
            this,
            new List<(int index, int delta)>(),
            new List<(int index, ItemInstance<TKey> instance)> { (index, item) },
            new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> { (proposedInstance, context) });

        if (!TryPrepareTransaction(transaction, null, out _, out failure))
            return false;

        var layoutContextsBefore = CaptureLayoutContextsForReconciliation();
        metadata.ReplaceDirect(proposedMetadata);
        var reconciliation = ReconcileLayoutAfterMutation();
        var layoutContexts = _layout.GetContextsForStorageIndex(this, index);
        var metadataChanged = new ItemMetadataChanged<TKey>(
            item,
            index,
            beforeMetadata,
            metadata.ToDictionary(),
            layoutContexts);
        var moved = BuildReflowMovements(layoutContextsBefore);

        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
            moved: moved,
            metadataChanged: new[] { metadataChanged },
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
            requiresFullRefresh: reconciliation.RequiresFullRefresh));
        failure = null;
        return true;
    }

    bool IInstanceMetadataOwner.TryApplyMetadataMutation(
        InstanceMetadata metadata,
        InstanceMetadata proposedMetadata,
        out InventoryFailure? failure)
    {
        return TryApplyMetadataMutation(metadata, proposedMetadata, out failure);
    }

    bool IInventoryMetadataOwner.TryApplyMetadataMutation(
        InventoryMetadata metadata,
        InventoryMetadata proposedMetadata,
        out InventoryFailure? failure)
    {
        if (!ReferenceEquals(metadata, Metadata))
        {
            failure = InventoryFailures.Metadata("Metadata does not belong to this inventory.");
            return false;
        }

        var candidate = CreateSimulationClone(this);
        candidate.Metadata.ReplaceDirect(proposedMetadata);
        if (!TryValidateReplacement(candidate, out _, out failure))
            return false;

        var before = Metadata.Clone();
        var layoutContextsBefore = CaptureLayoutContextsForReconciliation();
        Metadata.ReplaceDirect(proposedMetadata);
        var reconciliation = ReconcileLayoutAfterMutation();
        var moved = BuildReflowMovements(layoutContextsBefore);

        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
            moved: moved,
            inventoryMetadataChanged: new InventoryMetadataChanged(before, Metadata),
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
            requiresFullRefresh: reconciliation.RequiresFullRefresh,
            origin: InventoryChangeOrigin.Operation));
        failure = null;
        return true;
    }

    internal bool TrySplitAndSetMetadata(
        ItemInstance<TKey> instance,
        int amount,
        string key,
        object? value,
        out ItemInstance<TKey>? metadataStack,
        out InventoryFailure? failure)
    {
        metadataStack = null;
        if (instance == null)
        {
            failure = InventoryFailures.Validation("Item instance cannot be null.");
            return false;
        }

        int index = GetItemIndex(instance);
        if (index < 0)
        {
            failure = InventoryFailures.Validation("Item not found in inventory.");
            return false;
        }

        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }

        if (amount > instance.Amount)
        {
            failure = InventoryFailures.Validation("Not enough quantity to split.");
            return false;
        }

        if (amount == instance.Amount)
        {
            if (!instance.Metadata.TrySet(key, value, out failure))
                return false;

            metadataStack = instance;
            return true;
        }

        var splitMetadata = instance.Metadata.Clone();
        splitMetadata.SetDirect(key, value);
        var splitInstance = new ItemInstance<TKey>(instance.Definition, amount, splitMetadata);
        if (!TryResolveMaxStackSize(splitInstance, out int maxStack, out failure))
            return false;

        if (amount > maxStack)
        {
            failure = InventoryFailures.Stacking($"Split amount {amount} exceeds maximum stack size {maxStack}.");
            return false;
        }

        var transaction = new InventoryTransaction<TKey>(
            this,
            new List<(int index, int delta)> { (index, -amount) },
            new List<(int index, ItemInstance<TKey> instance)>(),
            new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> { (splitInstance, null) });

        if (!TryPrepareTransaction(transaction, null, out var mappedTransaction, out failure) || mappedTransaction == null)
            return false;

        ApplyPreparedTransaction(mappedTransaction);
        metadataStack = splitInstance;
        failure = null;
        return true;
    }

    internal bool TryFormulateAdd(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context, out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
        => TryFormulateAdd(definition, amount, context, null, out transaction, out failure);

    /// <summary>Internal: generates a transaction for removing from a specific instance.</summary>
    internal bool TryFormulateRemove(ItemInstance<TKey> instance, int amount, out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
    {
        transaction = null;
        failure = null;
        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }
        int index = _items.IndexOf(instance);
        if (index == -1)
        {
            failure = InventoryFailures.Validation("Item not found in inventory.");
            return false;
        }
        if (instance.Amount < amount)
        {
            failure = InventoryFailures.Validation("Not enough quantity to remove.");
            return false;
        }
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        if (instance.Amount == amount)
            removed.Add((index, instance));
        else
            amountDeltas.Add((index, -amount));
        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!TryPrepareTransaction(tx, null, out var mappedTx, out failure) || mappedTx == null)
            return false;
        transaction = mappedTx;
        return true;
    }

    /// <summary>Internal: generates a transaction for removing at a storage index.</summary>
    internal bool TryFormulateRemoveAt(int index, int amount, out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
    {
        transaction = null;
        failure = null;
        if (index < 0 || index >= _items.Count)
        {
            failure = InventoryFailures.Validation("Index out of range.");
            return false;
        }
        var inst = _items[index];
        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }
        if (inst.Amount < amount)
        {
            failure = InventoryFailures.Validation("Not enough quantity to remove.");
            return false;
        }
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        if (inst.Amount == amount)
            removed.Add((index, inst));
        else
            amountDeltas.Add((index, -amount));
        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!TryPrepareTransaction(tx, null, out var mappedTx, out failure) || mappedTx == null)
            return false;
        transaction = mappedTx;
        return true;
    }

    /// <summary>Internal: generates a transaction for removing by definition using an item metadata selector.</summary>
    internal bool TryFormulateRemoveByDefinition(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch, out InventoryTransaction<TKey>? transaction, out InventoryFailure? failure)
    {
        transaction = null;
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

        for (int i = 0; i < _items.Count && remaining > 0; i++)
        {
            var inst = _items[i];
            if (!EqualityComparer<TKey>.Default.Equals(inst.Definition.Id, definition.Id))
                continue;
            if (!metadataMatch.Matches(inst.Metadata))
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
            failure = InventoryFailures.Validation("Not enough matching items to remove.");
            return false;
        }

        var tx = new InventoryTransaction<TKey>(this, amountDeltas, removed, new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>());
        if (!TryPrepareTransaction(tx, null, out var mappedTx, out failure) || mappedTx == null)
            return false;
        transaction = mappedTx;
        return true;
    }

    internal bool TryPrepareTransaction(
        InventoryTransaction<TKey> tx,
        ILayoutContext<TKey>? placementContext,
        out InventoryTransaction<TKey>? mappedTransaction,
        out InventoryFailure? failure)
    {
        mappedTransaction = null;
        failure = null;
        if (tx == null)
        {
            failure = InventoryFailures.Transaction("Transaction cannot be null.");
            return false;
        }
        if (tx.Inventory != this)
        {
            failure = InventoryFailures.Transaction("Transaction does not belong to this inventory.");
            return false;
        }
        if (tx.IsApplied)
        {
            failure = InventoryFailures.Transaction("Transaction has already been applied.");
            return false;
        }
        if (tx.InventoryVersion != _version)
        {
            failure = InventoryFailures.Transaction("Inventory changed after the transaction was created. Rebuild the transaction against the current inventory state.");
            return false;
        }

        if (!_layout.TryApplyPlacementContext(this, tx, placementContext, out mappedTransaction, out failure) || mappedTransaction == null)
            return false;

        if (!ValidateTransactionConstraints(mappedTransaction, out failure))
        {
            mappedTransaction = null;
            return false;
        }

        return true;
    }

    private bool ValidateTransactionConstraints(InventoryTransaction<TKey> tx, out InventoryFailure? failure)
    {
        failure = null;
        if (!TryValidateTransactionDefinitions(tx, out failure))
            return false;

        var normalized = GenerateNormalizedInventoryTransaction(tx);
        if (!_capacityPolicy.CanApply(this, normalized, out failure))
            return false;
        if (!_rules.CanApply(this, normalized, tx, out failure))
            return false;
        if (!_layout.CanSatisfyPlacement(this, tx, out failure))
            return false;
        return true;
    }

    /// <summary>
    /// Adds or replaces an inventory rule after validating the current inventory contents against the proposed rule set.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add or replace.</param>
    /// <param name="failure">A consumer-facing reason when the rule change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the rule is applied; otherwise, <see langword="false"/>.</returns>
    public bool TrySetRule(string id, IRulePolicy<TKey> rule, out InventoryFailure? failure)
    {
        return TryApplyRuleMutation(
            id,
            _rules.GetRuleStateSnapshot(id) == null
                ? InventoryRuleConfigurationChangeKind.Added
                : InventoryRuleConfigurationChangeKind.Replaced,
            rules => rules.Set(id, rule),
            out failure);
    }

    /// <summary>
    /// Adds or replaces an inventory rule with explicit priority and enabled state after validating current contents.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add or replace.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <param name="failure">A consumer-facing reason when the rule change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the rule is applied; otherwise, <see langword="false"/>.</returns>
    public bool TrySetRule(string id, IRulePolicy<TKey> rule, int priority, bool enabled, out InventoryFailure? failure)
    {
        return TryApplyRuleMutation(
            id,
            _rules.GetRuleStateSnapshot(id) == null
                ? InventoryRuleConfigurationChangeKind.Added
                : InventoryRuleConfigurationChangeKind.Replaced,
            rules => rules.Set(id, rule, priority, enabled),
            out failure);
    }

    /// <summary>
    /// Removes an inventory rule by id after validating current contents against the proposed rule set.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="failure">A consumer-facing reason when the rule change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the rule exists and is removed; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveRule(string id, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            failure = InventoryFailures.Rules("Rule id cannot be null or empty.");
            return false;
        }

        if (!Rules.ContainsKey(id))
        {
            failure = InventoryFailures.Rules($"Rule '{id}' was not found.");
            return false;
        }

        return TryApplyRuleMutation(
            id,
            InventoryRuleConfigurationChangeKind.Removed,
            rules => rules.Remove(id),
            out failure);
    }

    /// <summary>
    /// Changes whether an inventory rule is enabled after validating current contents against the proposed rule set.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="enabled">The new enabled state.</param>
    /// <param name="failure">A consumer-facing reason when the rule change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the rule exists and is updated; otherwise, <see langword="false"/>.</returns>
    public bool TrySetRuleEnabled(string id, bool enabled, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            failure = InventoryFailures.Rules("Rule id cannot be null or empty.");
            return false;
        }

        if (!Rules.ContainsKey(id))
        {
            failure = InventoryFailures.Rules($"Rule '{id}' was not found.");
            return false;
        }

        var beforeState = _rules.GetRuleStateSnapshot(id);
        if (beforeState != null && beforeState.Enabled == enabled)
        {
            failure = null;
            return true;
        }

        return TryApplyRuleMutation(
            id,
            InventoryRuleConfigurationChangeKind.EnabledChanged,
            rules => rules.TrySetEnabled(id, enabled),
            out failure);
    }

    /// <summary>
    /// Changes an inventory rule priority after validating current contents against the proposed rule set.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="priority">The new priority. Higher values run first.</param>
    /// <param name="failure">A consumer-facing reason when the rule change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the rule exists and is updated; otherwise, <see langword="false"/>.</returns>
    public bool TrySetRulePriority(string id, int priority, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            failure = InventoryFailures.Rules("Rule id cannot be null or empty.");
            return false;
        }

        if (!Rules.ContainsKey(id))
        {
            failure = InventoryFailures.Rules($"Rule '{id}' was not found.");
            return false;
        }

        var beforeState = _rules.GetRuleStateSnapshot(id);
        if (beforeState != null && beforeState.Priority == priority)
        {
            failure = null;
            return true;
        }

        return TryApplyRuleMutation(
            id,
            InventoryRuleConfigurationChangeKind.PriorityChanged,
            rules => rules.TrySetPriority(id, priority),
            out failure);
    }

    /// <summary>
    /// Adds or replaces an inventory rule, throwing when the rule change is rejected.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add or replace.</param>
    /// <exception cref="InventoryOperationException">The proposed rule set rejects current inventory contents.</exception>
    public void SetRule(string id, IRulePolicy<TKey> rule)
    {
        if (!TrySetRule(id, rule, out var failure))
            ThrowMutationFailure(failure, "Rule change failed.");
    }

    /// <summary>
    /// Adds or replaces an inventory rule with explicit priority and enabled state, throwing when the rule change is rejected.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="rule">The rule to add or replace.</param>
    /// <param name="priority">The rule priority. Higher values run first.</param>
    /// <param name="enabled">Whether the rule participates in validation.</param>
    /// <exception cref="InventoryOperationException">The proposed rule set rejects current inventory contents.</exception>
    public void SetRule(string id, IRulePolicy<TKey> rule, int priority, bool enabled)
    {
        if (!TrySetRule(id, rule, priority, enabled, out var failure))
            ThrowMutationFailure(failure, "Rule change failed.");
    }

    /// <summary>
    /// Removes an inventory rule by id, throwing when the rule change is rejected.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <exception cref="InventoryOperationException">The rule does not exist or the proposed rule set rejects current contents.</exception>
    public void RemoveRule(string id)
    {
        if (!TryRemoveRule(id, out var failure))
            ThrowMutationFailure(failure, "Rule change failed.");
    }

    /// <summary>
    /// Changes whether an inventory rule is enabled, throwing when the rule change is rejected.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="enabled">The new enabled state.</param>
    /// <exception cref="InventoryOperationException">The rule does not exist or the proposed rule set rejects current contents.</exception>
    public void SetRuleEnabled(string id, bool enabled)
    {
        if (!TrySetRuleEnabled(id, enabled, out var failure))
            ThrowMutationFailure(failure, "Rule change failed.");
    }

    /// <summary>
    /// Changes an inventory rule priority, throwing when the rule change is rejected.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="priority">The new priority. Higher values run first.</param>
    /// <exception cref="InventoryOperationException">The rule does not exist or the proposed rule set rejects current contents.</exception>
    public void SetRulePriority(string id, int priority)
    {
        if (!TrySetRulePriority(id, priority, out var failure))
            ThrowMutationFailure(failure, "Rule change failed.");
    }

    private bool TryApplyRuleMutation(
        string ruleId,
        InventoryRuleConfigurationChangeKind changeKind,
        Action<RuleContainer<TKey>> mutate,
        out InventoryFailure? failure)
    {
        if (mutate == null)
            throw new ArgumentNullException(nameof(mutate));

        if (string.IsNullOrWhiteSpace(ruleId))
        {
            failure = InventoryFailures.Rules("Rule id cannot be null or empty.");
            return false;
        }

        var previousRules = _rules.Clone();
        var previousState = previousRules.GetRuleStateSnapshot(ruleId);
        var proposedRules = _rules.Clone();
        try
        {
            mutate(proposedRules);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException)
        {
            failure = InventoryFailures.Rules(ex.Message);
            return false;
        }

        if (!ValidateCurrentContentsAgainstRules(proposedRules, out failure))
            return false;

        _rules.ReplaceWith(proposedRules);
        var currentRules = proposedRules.Clone();
        var currentState = currentRules.GetRuleStateSnapshot(ruleId);
        FireRuleConfigurationChanged(
            ruleId,
            changeKind,
            previousState,
            currentState,
            previousRules,
            currentRules);
        failure = null;
        return true;
    }

    private bool ValidateCurrentContentsAgainstRules(RuleContainer<TKey> rules, out InventoryFailure? failure)
    {
        var validationInventory = new Inventory<TKey>(
            Manager,
            _stackResolver,
            _capacityPolicy,
            _layout.Clone(),
            rules);
        validationInventory.Metadata.ReplaceDirect(Metadata);
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();

        foreach (var item in _items)
        {
            var metadata = item.Metadata.IsEmpty ? null : CloneMetadata(item.Metadata);
            added.Add((new ItemInstance<TKey>(item.Definition, item.Amount, metadata), null));
        }

        var transaction = new InventoryTransaction<TKey>(
            validationInventory,
            new List<(int index, int delta)>(),
            new List<(int index, ItemInstance<TKey> instance)>(),
            added);
        var normalized = validationInventory.GenerateNormalizedInventoryTransaction(transaction);

        if (rules.CanApply(validationInventory, normalized, transaction, out failure))
            return true;

        failure = InventoryFailures.Rules($"Rule change would make current inventory contents invalid: {failure}");
        return false;
    }

    /// <summary>
    /// Attempts to change a stack resolver parameter after validating current stack amounts.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetStackResolverParameter(string parameterId, object? value, out InventoryFailure? failure)
        => TrySetStackResolverParameter(parameterId, value, InventoryParameterMutationActions.None, out failure);

    /// <summary>
    /// Attempts to change a stack resolver parameter after validating current stack amounts, optionally rebuilding current stacks.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">Actions controlling stack splitting, stack compression, and layout repack.</param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetStackResolverParameter(
        string parameterId,
        object? value,
        InventoryParameterMutationActions actions,
        out InventoryFailure? failure)
    {
        if (!ValidateParameterId(parameterId, out failure))
            return false;
        if (!ValidateStackResolverMutationActions(actions, out failure))
            return false;

        if (_stackResolver is not IParameterizedStackResolver<TKey> parameterized)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Current stack resolver does not support runtime parameters.");
            return false;
        }

        if (!parameterized.TryCreateWithParameter(this, parameterId, value, out var proposedResolver, out failure) || proposedResolver == null)
            return false;

        var previous = _stackResolver;
        if (actions == InventoryParameterMutationActions.None)
        {
            if (!ValidateCurrentContentsAgainstStackResolver(proposedResolver, out failure))
                return false;

            _stackResolver = proposedResolver;
            FireConfigurationChanged(InventoryConfigurationChangeKind.StackResolver, parameterId, value, previous, proposedResolver, requiresFullRefresh: false);
            return true;
        }

        if (!TryCreateStackMutationEntries(proposedResolver, actions, out var entries, out var changedShape, out failure) || entries == null)
            return false;

        if (HasAction(actions, InventoryParameterMutationActions.RepackLayout))
        {
            var proposedContents = CreateProposedContentsFromStackMutationEntries(entries);
            if (!TryCreateEmptyLayoutLike(_layout, out var proposedLayout, out failure) || proposedLayout == null)
                return false;

            if (!TryValidateProposedContents(proposedContents, proposedResolver, _capacityPolicy, proposedLayout, out failure))
                return false;

            ApplyConfigurationRebuild(
                proposedContents,
                proposedResolver,
                _capacityPolicy,
                proposedLayout,
                InventoryConfigurationChangeKind.StackResolver,
                parameterId,
                value,
                previous,
                proposedResolver);
            return true;
        }

        if (!changedShape)
        {
            _stackResolver = proposedResolver;
            FireConfigurationChanged(InventoryConfigurationChangeKind.StackResolver, parameterId, value, previous, proposedResolver, requiresFullRefresh: false);
            return true;
        }

        if (!TryCreateStackMutationTransaction(entries, out var transaction, out failure) || transaction == null)
            return false;

        ApplyStackParameterMutationTransaction(transaction, previous, proposedResolver, parameterId, value);
        return true;
    }

    /// <summary>
    /// Changes a stack resolver parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetStackResolverParameter(string parameterId, object? value)
        => SetStackResolverParameter(parameterId, value, InventoryParameterMutationActions.None);

    /// <summary>
    /// Changes a stack resolver parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">Actions controlling stack splitting, stack compression, and layout repack.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetStackResolverParameter(string parameterId, object? value, InventoryParameterMutationActions actions)
    {
        if (!TrySetStackResolverParameter(parameterId, value, actions, out var failure))
            ThrowMutationFailure(failure, "Stack resolver parameter change failed.");
    }

    /// <summary>
    /// Attempts to change a capacity policy parameter after validating current contents against the proposed policy.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetCapacityPolicyParameter(string parameterId, object? value, out InventoryFailure? failure)
        => TrySetCapacityPolicyParameter(parameterId, value, InventoryParameterMutationActions.None, out failure);

    /// <summary>
    /// Attempts to change a capacity policy parameter after validating current contents against the proposed policy.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">Mutation actions. Capacity parameter changes reject any action other than <see cref="InventoryParameterMutationActions.None"/>.</param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetCapacityPolicyParameter(
        string parameterId,
        object? value,
        InventoryParameterMutationActions actions,
        out InventoryFailure? failure)
    {
        if (!ValidateParameterId(parameterId, out failure))
            return false;
        if (!ValidateCapacityMutationActions(actions, out failure))
            return false;

        if (_capacityPolicy is not IParameterizedCapacityPolicy<TKey> parameterized)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Current capacity policy does not support runtime parameters.");
            return false;
        }

        if (!parameterized.TryCreateWithParameter(this, parameterId, value, out var proposedPolicy, out failure) || proposedPolicy == null)
            return false;

        if (!ValidateCurrentContentsAgainstCapacityPolicy(proposedPolicy, out failure))
            return false;

        var previous = _capacityPolicy;
        _capacityPolicy = proposedPolicy;
        FireConfigurationChanged(InventoryConfigurationChangeKind.CapacityPolicy, parameterId, value, previous, proposedPolicy, requiresFullRefresh: false);
        return true;
    }

    /// <summary>
    /// Changes a capacity policy parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetCapacityPolicyParameter(string parameterId, object? value)
        => SetCapacityPolicyParameter(parameterId, value, InventoryParameterMutationActions.None);

    /// <summary>
    /// Changes a capacity policy parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">Mutation actions. Capacity parameter changes reject any action other than <see cref="InventoryParameterMutationActions.None"/>.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetCapacityPolicyParameter(string parameterId, object? value, InventoryParameterMutationActions actions)
    {
        if (!TrySetCapacityPolicyParameter(parameterId, value, actions, out var failure))
            ThrowMutationFailure(failure, "Capacity policy parameter change failed.");
    }

    /// <summary>
    /// Attempts to change a layout parameter after validating that current placements are preserved.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetLayoutParameter(string parameterId, object? value, out InventoryFailure? failure)
        => TrySetLayoutParameter(parameterId, value, InventoryParameterMutationActions.None, out failure);

    /// <summary>
    /// Attempts to change a layout parameter after validating current contents, optionally re-packing placements.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">
    /// Actions controlling layout repack. Layout parameter changes reject stack mutation actions, and repack requires
    /// the active layout to implement <see cref="IParameterizedRepackableInventoryLayout{TKey}"/>.
    /// </param>
    /// <param name="failure">A consumer-facing reason when the parameter change is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter change is committed; otherwise, <see langword="false"/>.</returns>
    public bool TrySetLayoutParameter(
        string parameterId,
        object? value,
        InventoryParameterMutationActions actions,
        out InventoryFailure? failure)
    {
        if (!ValidateParameterId(parameterId, out failure))
            return false;
        if (!ValidateLayoutMutationActions(actions, out failure))
            return false;

        if (_layout is not IParameterizedInventoryLayout<TKey> parameterized)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Current layout does not support runtime parameters.");
            return false;
        }

        if (!TryCreateLayoutWithParameter(parameterized, parameterId, value, actions, out var proposedLayout, out failure) || proposedLayout == null)
            return false;

        if (!HasAction(actions, InventoryParameterMutationActions.RepackLayout) && !ValidateCurrentContentsAgainstLayout(proposedLayout, out failure))
            return false;

        var previous = _layout;
        if (!HasAction(actions, InventoryParameterMutationActions.RepackLayout))
        {
            var layoutContextsBefore = CaptureLayoutContextsByInstance();
            _layout = proposedLayout;
            var reconciliation = ReconcileLayoutAfterMutation();
            var moved = BuildReflowMovements(layoutContextsBefore);
            FireConfigurationChanged(
                InventoryConfigurationChangeKind.Layout,
                parameterId,
                value,
                previous,
                proposedLayout,
                requiresFullRefresh: true,
                moved: moved,
                affectedLayoutContexts: reconciliation.AffectedLayoutContexts);
            return true;
        }

        var repackContextsBefore = CaptureLayoutContextsByInstance();
        var orderedStorageIndices = GetStorageIndicesInCurrentLayoutOrder();
        var proposedContents = CreateCurrentContentsSnapshotInCurrentLayoutOrder();
        if (!TryValidateProposedContents(proposedContents, _stackResolver, _capacityPolicy, proposedLayout, out failure))
            return false;

        ApplyLayoutParameterRepack(
            proposedLayout,
            orderedStorageIndices,
            repackContextsBefore,
            parameterId,
            value,
            previous);
        return true;
    }

    /// <summary>
    /// Changes a layout parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetLayoutParameter(string parameterId, object? value)
        => SetLayoutParameter(parameterId, value, InventoryParameterMutationActions.None);

    /// <summary>
    /// Changes a layout parameter or throws when the parameter change is rejected.
    /// </summary>
    /// <param name="parameterId">The parameter id.</param>
    /// <param name="value">The proposed parameter value.</param>
    /// <param name="actions">Actions controlling layout repack. Layout parameter changes reject stack mutation actions.</param>
    /// <exception cref="InventoryOperationException">The parameter change is rejected.</exception>
    public void SetLayoutParameter(string parameterId, object? value, InventoryParameterMutationActions actions)
    {
        if (!TrySetLayoutParameter(parameterId, value, actions, out var failure))
            ThrowMutationFailure(failure, "Layout parameter change failed.");
    }

    private static bool ValidateParameterId(string parameterId, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter id cannot be null or empty.");
            return false;
        }

        failure = null;
        return true;
    }

    private static bool HasAction(
        InventoryParameterMutationActions actions,
        InventoryParameterMutationActions action)
        => (actions & action) == action;

    private static bool ValidateStackResolverMutationActions(InventoryParameterMutationActions actions, out InventoryFailure? failure)
    {
        var supported =
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.CompressCompatibleStacks;
        var unsupported = actions & ~supported;
        if (unsupported != InventoryParameterMutationActions.None)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Stack resolver parameter changes do not support mutation action value '{actions}'.");
            return false;
        }

        failure = null;
        return true;
    }

    private static bool ValidateLayoutMutationActions(InventoryParameterMutationActions actions, out InventoryFailure? failure)
    {
        var supported = InventoryParameterMutationActions.RepackLayout;
        var unsupported = actions & ~supported;
        if (unsupported != InventoryParameterMutationActions.None)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Layout parameter changes only support the RepackLayout mutation action.");
            return false;
        }

        failure = null;
        return true;
    }

    private static bool ValidateCapacityMutationActions(InventoryParameterMutationActions actions, out InventoryFailure? failure)
    {
        if (actions != InventoryParameterMutationActions.None)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Capacity policy parameter changes do not support mutation actions.");
            return false;
        }

        failure = null;
        return true;
    }

    private bool TryResolveMaxStackSize(ItemInstance<TKey> instance, out int maxStack, out InventoryFailure? failure)
    {
        return TryResolveMaxStackSize(_stackResolver, instance, out maxStack, out failure);
    }

    private bool TryResolveMaxStackSize(IStackResolver<TKey> resolver, ItemInstance<TKey> instance, out int maxStack, out InventoryFailure? failure)
    {
        maxStack = 0;
        if (resolver == null)
        {
            failure = InventoryFailures.Stacking("Stack resolver cannot be null.");
            return false;
        }

        try
        {
            maxStack = resolver.ResolveMaxStackSize(this, instance);
        }
        catch (InvalidOperationException ex)
        {
            failure = InventoryFailures.Stacking(ex.Message);
            return false;
        }

        if (maxStack <= 0)
        {
            failure = InventoryFailures.Definition($"Stack resolver returned invalid max stack size '{maxStack}' for item definition '{instance.Definition.Id}'.");
            return false;
        }

        failure = null;
        return true;
    }

    private bool ValidateCurrentContentsAgainstStackResolver(IStackResolver<TKey> resolver, out InventoryFailure? failure)
    {
        foreach (var item in _items)
        {
            if (!TryResolveMaxStackSize(resolver, item, out int maxStack, out failure))
                return false;

            if (item.Amount > maxStack)
            {
                failure = InventoryFailures.ConfigurationUnsupportedParameter($"Stack resolver parameter change would make current stack '{item.Definition.Id}' amount {item.Amount} exceed max stack size {maxStack}.");
                return false;
            }
        }

        failure = null;
        return true;
    }

    private bool ValidateCurrentContentsAgainstCapacityPolicy(ICapacityPolicy<TKey> policy, out InventoryFailure? failure)
    {
        var normalized = new NormalizedInventoryTransaction<TKey>(
            new List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)>(),
            new List<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)>());

        if (policy.CanApply(this, normalized, out failure))
            return true;

        failure = InventoryFailures.ConfigurationUnsupportedParameter($"Capacity policy parameter change would make current inventory contents invalid: {failure}");
        return false;
    }

    private bool ValidateCurrentContentsAgainstLayout(IInventoryLayout<TKey> layout, out InventoryFailure? failure)
    {
        if (layout.GetType() != _layout.GetType())
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Layout parameter change must keep the same layout type.");
            return false;
        }

        for (int storageIndex = 0; storageIndex < _items.Count; storageIndex++)
        {
            var before = _layout.GetContextsForStorageIndex(this, storageIndex);
            var after = layout.GetContextsForStorageIndex(this, storageIndex);
            if (!ContextListsEqual(before, after))
            {
                failure = InventoryFailures.ConfigurationUnsupportedParameter($"Layout parameter change would not preserve placement for item definition '{_items[storageIndex].Definition.Id}'.");
                return false;
            }
        }

        failure = null;
        return true;
    }

    private List<ProposedItemState> CreateCurrentContentsSnapshotInCurrentLayoutOrder()
    {
        var contents = new List<ProposedItemState>(_items.Count);
        foreach (int index in GetStorageIndicesInCurrentLayoutOrder())
        {
            var item = _items[index];
            var metadata = item.Metadata.IsEmpty ? null : CloneMetadata(item.Metadata);
            contents.Add(new ProposedItemState(item.Definition, item.Amount, metadata));
        }

        return contents;
    }

    private List<int> GetStorageIndicesInCurrentLayoutOrder()
    {
        var orderedIndices = new List<int>(_items.Count);
        var seen = new HashSet<int>();

        foreach (var context in _layout.GetAddressableContexts(this))
        {
            var item = _layout.GetItemAt(this, context);
            if (item == null)
                continue;

            int storageIndex = IndexOfOwnedItemReference(item);
            if (storageIndex >= 0 && seen.Add(storageIndex))
                orderedIndices.Add(storageIndex);
        }

        for (int index = 0; index < _items.Count; index++)
        {
            if (seen.Add(index))
                orderedIndices.Add(index);
        }

        return orderedIndices;
    }

    private Dictionary<int, int> GetCurrentLayoutOrderByStorageIndex()
    {
        var orderedIndices = GetStorageIndicesInCurrentLayoutOrder();
        var orderByIndex = new Dictionary<int, int>(orderedIndices.Count);
        for (int order = 0; order < orderedIndices.Count; order++)
            orderByIndex[orderedIndices[order]] = order;

        return orderByIndex;
    }

    private int IndexOfOwnedItemReference(ItemInstance<TKey> item)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            if (ReferenceEquals(_items[index], item))
                return index;
        }

        return -1;
    }

    private bool TryCreateStackMutationEntries(
        IStackResolver<TKey> resolver,
        InventoryParameterMutationActions actions,
        out List<StackMutationEntry>? entries,
        out bool changedShape,
        out InventoryFailure? failure)
    {
        entries = CreateCurrentStackMutationEntries();
        changedShape = false;
        failure = null;

        if (!TryApplySplitOversizedStacks(entries, resolver, HasAction(actions, InventoryParameterMutationActions.SplitOversizedStacks), out var splitChanged, out failure))
        {
            entries = null;
            return false;
        }

        changedShape = splitChanged;

        if (HasAction(actions, InventoryParameterMutationActions.CompressCompatibleStacks))
        {
            if (!TryApplyCompressCompatibleStacks(entries, resolver, out var compressionChanged, out failure))
            {
                entries = null;
                return false;
            }

            changedShape = changedShape || compressionChanged;
        }

        return true;
    }

    private List<StackMutationEntry> CreateCurrentStackMutationEntries()
    {
        var entries = new List<StackMutationEntry>(_items.Count);
        var layoutOrderByIndex = GetCurrentLayoutOrderByStorageIndex();
        for (int index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var metadata = item.Metadata.IsEmpty ? null : CloneMetadata(item.Metadata);
            int layoutOrder = layoutOrderByIndex.TryGetValue(index, out var order) ? order : index;
            entries.Add(new StackMutationEntry(index, layoutOrder, 0, item.Definition, item.Amount, item.Amount, metadata));
        }

        return entries;
    }

    private bool TryApplySplitOversizedStacks(
        List<StackMutationEntry> entries,
        IStackResolver<TKey> resolver,
        bool splitOversizedStacks,
        out bool changedShape,
        out InventoryFailure? failure)
    {
        changedShape = false;
        failure = null;
        int originalEntryCount = entries.Count;

        for (int i = 0; i < originalEntryCount; i++)
        {
            var entry = entries[i];
            if (entry.Amount <= 0)
                continue;

            var prototype = new ItemInstance<TKey>(entry.Definition, 1, CloneMetadataOrNull(entry.Metadata));
            if (!TryResolveMaxStackSize(resolver, prototype, out int maxStack, out failure))
                return false;

            if (entry.Amount <= maxStack)
                continue;

            if (!splitOversizedStacks)
            {
                failure = InventoryFailures.ConfigurationUnsupportedParameter($"Stack resolver parameter change would make current stack '{entry.Definition.Id}' amount {entry.Amount} exceed max stack size {maxStack}.");
                return false;
            }

            int remaining = entry.Amount - maxStack;
            entry.Amount = maxStack;
            changedShape = true;
            int splitSequence = 1;

            while (remaining > 0)
            {
                int chunk = Math.Min(remaining, maxStack);
                entries.Add(new StackMutationEntry(
                    null,
                    entry.LayoutOrder,
                    splitSequence++,
                    entry.Definition,
                    0,
                    chunk,
                    CloneMetadataOrNull(entry.Metadata)));
                remaining -= chunk;
            }
        }

        return true;
    }

    private bool TryApplyCompressCompatibleStacks(
        List<StackMutationEntry> entries,
        IStackResolver<TKey> resolver,
        out bool changedShape,
        out InventoryFailure? failure)
    {
        changedShape = false;
        failure = null;

        for (int targetIndex = 0; targetIndex < entries.Count; targetIndex++)
        {
            var target = entries[targetIndex];
            if (target.Amount <= 0)
                continue;

            var targetPrototype = new ItemInstance<TKey>(target.Definition, 1, CloneMetadataOrNull(target.Metadata));
            if (!TryResolveMaxStackSize(resolver, targetPrototype, out int targetMaxStack, out failure))
                return false;

            for (int sourceIndex = targetIndex + 1; sourceIndex < entries.Count && target.Amount < targetMaxStack; sourceIndex++)
            {
                var source = entries[sourceIndex];
                if (source.Amount <= 0 || !StackMutationEntriesAreCompatible(target, source))
                    continue;

                int move = Math.Min(targetMaxStack - target.Amount, source.Amount);
                target.Amount += move;
                source.Amount -= move;
                changedShape = true;
            }
        }

        return true;
    }

    private bool TryCreateStackMutationTransaction(
        IReadOnlyList<StackMutationEntry> entries,
        out InventoryTransaction<TKey>? transaction,
        out InventoryFailure? failure)
    {
        var amountDeltas = new List<(int index, int delta)>();
        var removed = new List<(int index, ItemInstance<TKey> instance)>();
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();

        foreach (var entry in entries)
        {
            if (entry.OriginalIndex.HasValue)
            {
                int index = entry.OriginalIndex.Value;
                if (entry.Amount <= 0)
                    removed.Add((index, _items[index]));
                else if (entry.Amount != entry.OriginalAmount)
                    amountDeltas.Add((index, entry.Amount - entry.OriginalAmount));

                continue;
            }

            if (entry.Amount <= 0)
                continue;

            added.Add((new ItemInstance<TKey>(entry.Definition, entry.Amount, CloneMetadataOrNull(entry.Metadata)), null));
        }

        var candidate = new InventoryTransaction<TKey>(this, amountDeltas, removed, added);
        if (!TryPrepareTransaction(candidate, null, out var prepared, out failure) || prepared == null)
        {
            transaction = null;
            return false;
        }

        transaction = prepared;
        return true;
    }

    private List<ProposedItemState> CreateProposedContentsFromStackMutationEntries(IReadOnlyList<StackMutationEntry> entries)
    {
        var contents = new List<ProposedItemState>();
        foreach (var entry in entries.OrderBy(entry => entry.LayoutOrder).ThenBy(entry => entry.SplitSequence))
        {
            if (entry.Amount <= 0)
                continue;

            contents.Add(new ProposedItemState(entry.Definition, entry.Amount, CloneMetadataOrNull(entry.Metadata)));
        }

        return contents;
    }

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? metadata)
    {
        return metadata != null && !metadata.IsEmpty ? CloneMetadata(metadata) : null;
    }

    private static bool StackMutationEntriesAreCompatible(StackMutationEntry left, StackMutationEntry right)
    {
        if (!ReferenceEquals(left.Definition, right.Definition))
            return false;

        return MetadataStructurallyEqual(left.Metadata, right.Metadata);
    }

    private static bool MetadataStructurallyEqual(InstanceMetadata? left, InstanceMetadata? right)
    {
        bool leftEmpty = left == null || left.IsEmpty;
        bool rightEmpty = right == null || right.IsEmpty;
        if (leftEmpty || rightEmpty)
            return leftEmpty && rightEmpty;

        return left!.StructuralEquals(right!);
    }

    private bool TryValidateProposedContents(
        IReadOnlyList<ProposedItemState> proposedContents,
        IStackResolver<TKey> stackResolver,
        ICapacityPolicy<TKey> capacityPolicy,
        IInventoryLayout<TKey> layout,
        out InventoryFailure? failure)
    {
        var validationInventory = new Inventory<TKey>(
            Manager,
            stackResolver,
            capacityPolicy,
            layout,
            _rules.Clone());
        validationInventory.Metadata.ReplaceDirect(Metadata);

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>(proposedContents.Count);
        foreach (var state in proposedContents)
        {
            var metadata = state.Metadata != null && !state.Metadata.IsEmpty ? CloneMetadata(state.Metadata) : null;
            added.Add((new ItemInstance<TKey>(state.Definition, state.Amount, metadata), null));
        }

        var transaction = new InventoryTransaction<TKey>(
            validationInventory,
            new List<(int index, int delta)>(),
            new List<(int index, ItemInstance<TKey> instance)>(),
            added);

        return validationInventory.TryPrepareTransaction(transaction, null, out _, out failure);
    }

    private bool TryCreateLayoutWithParameter(
        IParameterizedInventoryLayout<TKey> parameterized,
        string parameterId,
        object? value,
        InventoryParameterMutationActions actions,
        out IInventoryLayout<TKey>? layout,
        out InventoryFailure? failure)
    {
        if (!HasAction(actions, InventoryParameterMutationActions.RepackLayout))
            return parameterized.TryCreateWithParameter(this, parameterId, value, out layout, out failure);

        if (parameterized is not IParameterizedRepackableInventoryLayout<TKey> repackable)
        {
            layout = null;
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Current layout type '{parameterized.GetType().Name}' does not support parameterized inventory-owned repack.");
            return false;
        }

        return repackable.TryCreateEmptyRepackLayoutWithParameter(
            parameterId,
            value,
            out layout,
            out failure);
    }

    private bool TryCreateEmptyLayoutLike(IInventoryLayout<TKey> source, out IInventoryLayout<TKey>? layout, out InventoryFailure? failure)
    {
        if (source is not IRepackableInventoryLayout<TKey> repackable)
        {
            layout = null;
            failure = InventoryFailures.Layout($"Current layout type '{source.GetType().Name}' does not support inventory-owned repack.");
            return false;
        }

        return repackable.TryCreateEmptyRepackLayout(out layout, out failure);
    }

    private void ApplyConfigurationRebuild(
        IReadOnlyList<ProposedItemState> proposedContents,
        IStackResolver<TKey> stackResolver,
        ICapacityPolicy<TKey> capacityPolicy,
        IInventoryLayout<TKey> layout,
        InventoryConfigurationChangeKind kind,
        string parameterId,
        object? value,
        object previousComponent,
        object currentComponent)
    {
        var removedEvents = new List<ItemRemoved<TKey>>(_items.Count);
        for (int i = 0; i < _items.Count; i++)
            removedEvents.Add(new ItemRemoved<TKey>(_items[i], i, _layout.GetContextsForStorageIndex(this, i)));

        _stackResolver = stackResolver;
        _capacityPolicy = capacityPolicy;
        _layout = layout;
        foreach (var item in _items)
            item.DetachOwner(this);
        _items.Clear();

        var addedInstances = new List<ItemInstance<TKey>>(proposedContents.Count);
        foreach (var state in proposedContents)
        {
            var metadata = state.Metadata != null && !state.Metadata.IsEmpty ? CloneMetadata(state.Metadata) : null;
            var instance = new ItemInstance<TKey>(state.Definition, state.Amount, metadata);
            AddItem(instance, null);
            addedInstances.Add(instance);
        }

        var reconciliation = ReconcileLayoutAfterMutation();
        var addedEvents = new List<ItemAdded<TKey>>(addedInstances.Count);
        foreach (var instance in addedInstances)
        {
            int index = _items.IndexOf(instance);
            addedEvents.Add(new ItemAdded<TKey>(
                instance,
                index,
                _layout.GetContextsForStorageIndex(this, index)));
        }

        bool requiresFullRefresh =
            kind == InventoryConfigurationChangeKind.Layout ||
            reconciliation.RequiresFullRefresh;
        var change = new InventoryConfigurationChanged<TKey>(
            kind,
            parameterId,
            value,
            previousComponent,
            currentComponent,
            requiresFullRefresh);

        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
            added: addedEvents,
            removed: removedEvents,
            configurationChanged: new[] { change },
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
            requiresFullRefresh: requiresFullRefresh));
    }

    private void ApplyLayoutRepack(
        IInventoryLayout<TKey> proposedLayout,
        IReadOnlyList<int> orderedStorageIndices,
        IReadOnlyDictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>> before)
    {
        _layout = proposedLayout;
        foreach (var storageIndex in orderedStorageIndices)
            _layout.OnItemAdded(this, storageIndex, null);

        var reconciliation = ReconcileLayoutAfterMutation();
        var moved = BuildReflowMovements(before, cause: ItemMovementCause.Repack);

        if (moved.Count > 0 || reconciliation.AffectedLayoutContexts.Count > 0 || reconciliation.RequiresFullRefresh)
        {
            Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
                moved: moved,
                affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
                requiresFullRefresh: reconciliation.RequiresFullRefresh));
        }
    }

    private void ApplyLayoutParameterRepack(
        IInventoryLayout<TKey> proposedLayout,
        IReadOnlyList<int> orderedStorageIndices,
        IReadOnlyDictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>> before,
        string parameterId,
        object? value,
        IInventoryLayout<TKey> previousLayout)
    {
        _layout = proposedLayout;
        foreach (var storageIndex in orderedStorageIndices)
            _layout.OnItemAdded(this, storageIndex, null);

        var reconciliation = ReconcileLayoutAfterMutation();
        var moved = BuildReflowMovements(before, cause: ItemMovementCause.Repack);

        FireConfigurationChanged(
            InventoryConfigurationChangeKind.Layout,
            parameterId,
            value,
            previousLayout,
            proposedLayout,
            requiresFullRefresh: true,
            moved: moved,
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts);
    }

    private void FireConfigurationChanged(
        InventoryConfigurationChangeKind kind,
        string parameterId,
        object? value,
        object previousComponent,
        object currentComponent,
        bool requiresFullRefresh,
        IEnumerable<ItemMoved<TKey>>? moved = null,
        IEnumerable<ILayoutContext<TKey>>? affectedLayoutContexts = null)
    {
        var change = new InventoryConfigurationChanged<TKey>(
            kind,
            parameterId,
            value,
            previousComponent,
            currentComponent,
            requiresFullRefresh);

        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
            moved: moved,
            configurationChanged: new[] { change },
            affectedLayoutContexts: affectedLayoutContexts));
    }

    private void FireRuleConfigurationChanged(
        string ruleId,
        InventoryRuleConfigurationChangeKind changeKind,
        InventoryRuleState<TKey>? previousState,
        InventoryRuleState<TKey>? currentState,
        RuleContainer<TKey> previousRules,
        RuleContainer<TKey> currentRules)
    {
        var ruleChange = new InventoryRuleConfigurationChanged<TKey>(
            ruleId,
            changeKind,
            previousState,
            currentState);
        var change = new InventoryConfigurationChanged<TKey>(
            InventoryConfigurationChangeKind.Rules,
            ruleId,
            value: null,
            previousRules,
            currentRules,
            requiresFullRefresh: false,
            ruleChange: ruleChange);

        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
            configurationChanged: new[] { change },
            requiresFullRefresh: false));
    }

    private void ApplyStackParameterMutationTransaction(
        InventoryTransaction<TKey> transaction,
        IStackResolver<TKey> previousResolver,
        IStackResolver<TKey> proposedResolver,
        string parameterId,
        object? value)
    {
        var change = new InventoryConfigurationChanged<TKey>(
            InventoryConfigurationChangeKind.StackResolver,
            parameterId,
            value,
            previousResolver,
            proposedResolver,
            requiresFullRefresh: false);

        _stackResolver = proposedResolver;
        var args = ApplyPreparedTransactionCore(transaction, configurationChanged: new[] { change });
        if (args != null)
            Changed?.Invoke(this, args);
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

    /// <summary>Internal: executes a transaction. Transaction must reference this inventory and must not already be applied. Fires a single Changed event.</summary>
    /// <param name="transaction">The structural transaction to commit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="transaction"/> targets another inventory or has already been applied.</exception>
    internal void CommitTransaction(InventoryTransaction<TKey> transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (transaction.Inventory != this)
            throw new InvalidOperationException("Transaction does not belong to this inventory.");
        if (transaction.IsApplied)
            throw new InvalidOperationException("Transaction has already been applied.");
        if (!TryCommitTransaction(transaction, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
    }

    /// <summary>
    /// Internal: builds and commits a transaction builder or throws when commit is rejected.
    /// </summary>
    /// <param name="builder">The builder targeting this inventory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InventoryOperationException">The built transaction is rejected.</exception>
    internal void CommitTransaction(InventoryTransactionBuilder<TKey> builder)
    {
        CommitTransaction(builder, null);
    }

    /// <summary>
    /// Internal: builds and commits a transaction builder after applying a transaction-level placement context.
    /// </summary>
    /// <param name="builder">The builder targeting this inventory.</param>
    /// <param name="placementContext">Optional layout-specific transaction placement context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InventoryOperationException">The built transaction is rejected.</exception>
    internal void CommitTransaction(InventoryTransactionBuilder<TKey> builder, ILayoutContext<TKey>? placementContext)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (!TryCommitTransaction(builder, placementContext, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
    }

    /// <summary>
    /// Internal: attempts to execute a transaction after validating all inventory constraints.
    /// </summary>
    /// <param name="transaction">The structural transaction to commit.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction is committed; otherwise, <see langword="false"/>.</returns>
    internal bool TryCommitTransaction(InventoryTransaction<TKey> transaction, out InventoryFailure? failure)
    {
        return TryCommitTransaction(transaction, null, out failure);
    }

    internal bool CanCommitTransaction(InventoryTransaction<TKey> transaction, out InventoryFailure? failure)
    {
        return TryPrepareTransaction(transaction, null, out _, out failure);
    }

    /// <summary>
    /// Internal: attempts to build and commit a transaction builder.
    /// </summary>
    /// <param name="builder">The builder targeting this inventory.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the built transaction is committed; otherwise, <see langword="false"/>.</returns>
    internal bool TryCommitTransaction(InventoryTransactionBuilder<TKey> builder, out InventoryFailure? failure)
    {
        return TryCommitTransaction(builder, null, out failure);
    }

    /// <summary>
    /// Internal: attempts to build and commit a transaction builder after applying a transaction-level placement context.
    /// </summary>
    /// <param name="builder">The builder targeting this inventory.</param>
    /// <param name="placementContext">Optional layout-specific transaction placement context.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the built transaction is committed; otherwise, <see langword="false"/>.</returns>
    internal bool TryCommitTransaction(InventoryTransactionBuilder<TKey> builder, ILayoutContext<TKey>? placementContext, out InventoryFailure? failure)
    {
        if (builder == null)
        {
            failure = InventoryFailures.Transaction("Transaction builder cannot be null.");
            return false;
        }

        if (!builder.TryBuild(placementContext, out var transaction, out failure) || transaction == null)
            return false;

        return TryCommitTransaction(transaction, out failure);
    }

    /// <summary>
    /// Internal: attempts to execute a transaction after applying a transaction-level placement context.
    /// </summary>
    /// <param name="transaction">The structural transaction to commit.</param>
    /// <param name="placementContext">Optional layout-specific transaction placement context.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction is committed; otherwise, <see langword="false"/>.</returns>
    internal bool TryCommitTransaction(
        InventoryTransaction<TKey> transaction,
        ILayoutContext<TKey>? placementContext,
        out InventoryFailure? failure)
    {
        if (!TryPrepareTransaction(transaction, placementContext, out var mappedTransaction, out failure) || mappedTransaction == null)
            return false;

        ApplyPreparedTransaction(mappedTransaction);
        return true;
    }

    /// <summary>
    /// Evaluates whether a staged transfer can be committed from this source inventory to a target inventory.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that would receive the transfer entries.</param>
    /// <param name="failure">A consumer-facing reason when commit would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer can be committed; otherwise, <see langword="false"/>.</returns>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public bool CanCommitTransfer(InventoryTransferBuilder<TKey> builder, Inventory<TKey> target, out InventoryFailure? failure)
    {
        return CanCommitTransfer(builder, target, null, out failure);
    }

    /// <summary>
    /// Evaluates whether a staged transfer can be committed from this source inventory to a target inventory.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that would receive the transfer entries.</param>
    /// <param name="targetContext">Optional target layout context for incoming entries.</param>
    /// <param name="failure">A consumer-facing reason when commit would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer can be committed; otherwise, <see langword="false"/>.</returns>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public bool CanCommitTransfer(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        ILayoutContext<TKey>? targetContext,
        out InventoryFailure? failure)
    {
        if (builder == null)
        {
            failure = InventoryFailures.Transfer("Transfer builder cannot be null.");
            return false;
        }
        if (target == null)
        {
            failure = InventoryFailures.Transfer("Target inventory cannot be null.");
            return false;
        }

        return InventoryTransfer.CanCommitBuilder(this, builder, target, targetContext, out failure);
    }

    /// <summary>
    /// Attempts to commit a staged transfer from this source inventory to a target inventory.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that should receive the transfer entries.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full transfer is committed; otherwise, <see langword="false"/>.</returns>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public bool TryCommitTransfer(InventoryTransferBuilder<TKey> builder, Inventory<TKey> target, out InventoryFailure? failure)
    {
        return TryCommitTransfer(builder, target, null, out failure);
    }

    /// <summary>
    /// Attempts to commit a staged transfer from this source inventory to a target inventory.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that should receive the transfer entries.</param>
    /// <param name="targetContext">Optional target layout context for incoming entries.</param>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full transfer is committed; otherwise, <see langword="false"/>.</returns>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public bool TryCommitTransfer(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        ILayoutContext<TKey>? targetContext,
        out InventoryFailure? failure)
    {
        if (builder == null)
        {
            failure = InventoryFailures.Transfer("Transfer builder cannot be null.");
            return false;
        }
        if (target == null)
        {
            failure = InventoryFailures.Transfer("Target inventory cannot be null.");
            return false;
        }

        return InventoryTransfer.TryCommitBuilder(this, builder, target, targetContext, out failure);
    }

    /// <summary>
    /// Commits a staged transfer from this source inventory to a target inventory, or throws when rejected.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that should receive the transfer entries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InventoryOperationException">The transfer is rejected.</exception>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public void CommitTransfer(InventoryTransferBuilder<TKey> builder, Inventory<TKey> target)
    {
        CommitTransfer(builder, target, null);
    }

    /// <summary>
    /// Commits a staged transfer from this source inventory to a target inventory, or throws when rejected.
    /// </summary>
    /// <param name="builder">The transfer builder created from this inventory.</param>
    /// <param name="target">The target inventory that should receive the transfer entries.</param>
    /// <param name="targetContext">Optional target layout context for incoming entries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InventoryOperationException">The transfer is rejected.</exception>
    [Obsolete("Transfer APIs are retained for backwards compatibility. Use InventoryTransaction<TKey>.From(source).To(target) with FromSide/ToSide staging for one-way cross-inventory movement.")]
    public void CommitTransfer(InventoryTransferBuilder<TKey> builder, Inventory<TKey> target, ILayoutContext<TKey>? targetContext)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (!TryCommitTransfer(builder, target, targetContext, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
    }

    /// <summary>
    /// Evaluates whether this inventory can transfer an amount from one of its item instances to a target inventory.
    /// </summary>
    public bool CanTransferTo(
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext,
        out InventoryFailure? failure)
    {
        if (!TryBuildOneWayTransferTransaction(target, item, amount, targetContext, out var transaction, out failure)
            || transaction == null)
            return false;

        return transaction.Validate(out failure);
    }

    /// <summary>
    /// Attempts to transfer an amount from one of this inventory's item instances to a target inventory.
    /// </summary>
    public bool TryTransferTo(
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext,
        out InventoryFailure? failure)
    {
        if (!TryBuildOneWayTransferTransaction(target, item, amount, targetContext, out var transaction, out failure)
            || transaction == null)
            return false;

        return transaction.TryCommit(out failure);
    }

    /// <summary>
    /// Transfers an amount from one of this inventory's item instances to a target inventory, or throws when rejected.
    /// </summary>
    public void TransferTo(
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext = null)
    {
        if (!TryTransferTo(target, item, amount, targetContext, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
    }

    private bool TryBuildOneWayTransferTransaction(
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext,
        out InventoryTransaction<TKey>? transaction,
        out InventoryFailure? failure)
    {
        transaction = null;
        if (!TryValidateOutgoingTransfer(target, item, amount, out failure))
            return false;

        try
        {
            var builder = InventoryTransaction<TKey>
                .From(this)
                .To(target);
            if (!builder.FromSide.TryRemove(item, out failure, amount))
                return false;

            var metadata = item.Metadata.IsEmpty ? null : item.Metadata;
            if (!builder.ToSide.TryAdd(item.Definition, amount, targetContext, metadata, out failure))
                return false;

            transaction = builder;
            failure = null;
            return true;
        }
        catch (InventorySystemException ex)
        {
            failure = InventoryFailure.Wrap(
                InventoryFailureKind.Transfer,
                InventoryFailureCodes.TransferRejected,
                "Transfer was rejected.",
                ex.Failure);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            failure = InventoryFailures.Transfer(ex.Message);
            return false;
        }
    }

    private bool TryValidateOutgoingTransfer(Inventory<TKey> target, ItemInstance<TKey> item, int amount, out InventoryFailure? failure)
    {
        if (target == null)
        {
            failure = InventoryFailures.Transfer("Target inventory cannot be null.");
            return false;
        }
        if (item == null)
        {
            failure = InventoryFailures.Validation("Item cannot be null.");
            return false;
        }
        if (ReferenceEquals(this, target))
        {
            failure = InventoryFailures.Transfer("Cannot transfer between the same inventory.");
            return false;
        }
        if (!ReferenceEquals(Manager, target.Manager) && !ReferenceEquals(Catalog, target.Catalog))
        {
            failure = InventoryFailures.Definition("Inventories must share the same item catalog.");
            return false;
        }
        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }

        bool sourceContainsItem = false;
        foreach (var sourceItem in Items)
        {
            if (ReferenceEquals(sourceItem, item))
            {
                sourceContainsItem = true;
                break;
            }
        }

        if (!sourceContainsItem)
        {
            failure = InventoryFailures.Validation("Item not found in source inventory.");
            return false;
        }
        if (item.Amount < amount)
        {
            failure = InventoryFailures.Transfer("Not enough quantity to transfer.");
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>
    /// Attempts to move every item from this source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public bool TryMoveAllTo(Inventory<TKey> target, ILayoutContext<TKey>? targetContext, out InventoryFailure? failure)
    {
        return TryMoveWhereTo(target, _ => true, targetContext, out failure);
    }

    /// <summary>
    /// Attempts to move every matching item from this source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public bool TryMoveWhereTo(
        Inventory<TKey> target,
        Func<ItemInstance<TKey>, bool> predicate,
        ILayoutContext<TKey>? targetContext,
        out InventoryFailure? failure)
    {
        if (predicate == null)
        {
            failure = InventoryFailures.Validation("Predicate cannot be null.");
            return false;
        }

        if (target == null)
        {
            failure = InventoryFailures.Transfer("Target inventory cannot be null.");
            return false;
        }
        if (ReferenceEquals(this, target))
        {
            failure = InventoryFailures.Transfer("Cannot transfer between the same inventory.");
            return false;
        }
        if (!ReferenceEquals(Manager, target.Manager) && !ReferenceEquals(Catalog, target.Catalog))
        {
            failure = InventoryFailures.Definition("Inventories must share the same item catalog.");
            return false;
        }

        InventoryTransaction<TKey> transaction;
        try
        {
            transaction = InventoryTransaction<TKey>
                .From(this)
                .To(target);
        }
        catch (InvalidOperationException ex)
        {
            failure = InventoryFailures.Transfer(ex.Message);
            return false;
        }

        var any = false;
        foreach (var item in new List<ItemInstance<TKey>>(Items))
        {
            if (!predicate(item))
                continue;

            any = true;
            if (!transaction.FromSide.TryRemove(item, out failure, item.Amount))
                return false;

            var metadata = item.Metadata.IsEmpty ? null : item.Metadata;
            if (!transaction.ToSide.TryAdd(item.Definition, item.Amount, targetContext, metadata, out failure))
                return false;
        }

        if (!any)
        {
            failure = InventoryFailures.TransferEmpty("Transfer contains no items.");
            return false;
        }

        return transaction.TryCommit(out failure);
    }

    /// <summary>
    /// Attempts to move every item with a catalog-resolved tag from this source inventory to a target inventory.
    /// </summary>
    public bool TryMoveByTagTo(Inventory<TKey> target, string tagId, ILayoutContext<TKey>? targetContext, out InventoryFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(tagId))
        {
            failure = InventoryFailures.Definition("Tag cannot be null.");
            return false;
        }

        return TryMoveWhereTo(target, item => Catalog.Satisfies(item.Definition, tagId), targetContext, out failure);
    }

    /// <summary>
    /// Attempts to move every item satisfying all catalog-resolved tags from this source inventory to a target inventory.
    /// </summary>
    public bool TryMoveAllTagsTo(Inventory<TKey> target, string[] tagIds, ILayoutContext<TKey>? targetContext, out InventoryFailure? failure)
    {
        if (tagIds == null || tagIds.Length == 0)
        {
            failure = InventoryFailures.Definition("At least one tag is required.");
            return false;
        }
        foreach (var tagId in tagIds)
        {
            if (string.IsNullOrWhiteSpace(tagId))
            {
                failure = InventoryFailures.Definition("Tags cannot contain null.");
                return false;
            }

            Catalog.Tags.GetKey(tagId);
        }

        return TryMoveWhereTo(
            target,
            item =>
            {
                foreach (var tagId in tagIds)
                {
                    if (!Catalog.Satisfies(item.Definition, tagId))
                        return false;
                }
                return true;
            },
            targetContext,
            out failure);
    }

    /// <summary>
    /// Attempts to transfer the largest valid amount up to a requested amount.
    /// </summary>
    public bool TryTransferMaximumTo(
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int requestedAmount,
        ILayoutContext<TKey>? targetContext,
        out int transferredAmount,
        out InventoryFailure? failure)
    {
        transferredAmount = 0;
        if (requestedAmount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }
        if (item == null)
        {
            failure = InventoryFailures.Validation("Item cannot be null.");
            return false;
        }

        int low = 1;
        int high = Math.Min(requestedAmount, item.Amount);
        int best = 0;
        InventoryFailure? lastError = null;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (CanTransferTo(target, item, mid, targetContext, out lastError))
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (best <= 0)
        {
            failure = lastError;
            return false;
        }

        if (!TryTransferTo(target, item, best, targetContext, out failure))
        {
            transferredAmount = 0;
            return false;
        }

        transferredAmount = best;
        return true;
    }

    /// <summary>
    /// Attempts to move as much matching item amount as possible in source storage order.
    /// </summary>
    public bool TryMoveMaximumWhereTo(
        Inventory<TKey> target,
        Func<ItemInstance<TKey>, bool> predicate,
        ILayoutContext<TKey>? targetContext,
        out int transferredAmount,
        out InventoryFailure? failure)
    {
        transferredAmount = 0;
        if (predicate == null)
        {
            failure = InventoryFailures.Validation("Predicate cannot be null.");
            return false;
        }

        InventoryFailure? lastError = null;
        foreach (var item in new List<ItemInstance<TKey>>(Items))
        {
            if (!predicate(item))
                continue;

            if (TryTransferMaximumTo(target, item, item.Amount, targetContext, out var moved, out lastError))
                transferredAmount += moved;
        }

        failure = transferredAmount > 0 ? null : lastError ?? InventoryFailures.TransferEmpty("Transfer contains no items.");
        return transferredAmount > 0;
    }

    /// <summary>
    /// Attempts to move as much item amount with a catalog-resolved tag as possible in source storage order.
    /// </summary>
    public bool TryMoveMaximumByTagTo(
        Inventory<TKey> target,
        string tagId,
        ILayoutContext<TKey>? targetContext,
        out int transferredAmount,
        out InventoryFailure? failure)
    {
        transferredAmount = 0;
        if (string.IsNullOrWhiteSpace(tagId))
        {
            failure = InventoryFailures.Definition("Tag cannot be null.");
            return false;
        }

        return TryMoveMaximumWhereTo(target, item => Catalog.Satisfies(item.Definition, tagId), targetContext, out transferredAmount, out failure);
    }

    /// <summary>
    /// Attempts to swap complete item stacks between this inventory and another compatible inventory.
    /// </summary>
    public bool TrySwapItemsWithInventory(
        Inventory<TKey> other,
        ItemInstance<TKey> sourceItem,
        ItemInstance<TKey> otherItem,
        ILayoutContext<TKey>? sourceTargetContext,
        ILayoutContext<TKey>? otherTargetContext,
        out InventoryFailure? failure)
    {
        if (sourceItem == null)
        {
            failure = InventoryFailures.Validation("First item cannot be null.");
            return false;
        }
        if (otherItem == null)
        {
            failure = InventoryFailures.Validation("Second item cannot be null.");
            return false;
        }

        return TrySwapItemsWithInventory(
            other,
            sourceItem,
            sourceItem.Amount,
            otherItem,
            otherItem.Amount,
            sourceTargetContext,
            otherTargetContext,
            out failure);
    }

    /// <summary>
    /// Attempts to swap item amounts between this inventory and another compatible inventory.
    /// </summary>
    public bool TrySwapItemsWithInventory(
        Inventory<TKey> other,
        ItemInstance<TKey> sourceItem,
        int sourceAmount,
        ItemInstance<TKey> otherItem,
        int otherAmount,
        ILayoutContext<TKey>? sourceTargetContext,
        ILayoutContext<TKey>? otherTargetContext,
        out InventoryFailure? failure)
    {
#pragma warning disable CS0618 // Swap currently reuses deprecated transfer internals for compatibility.
        return InventoryTransfer.TrySwap(this, other, sourceItem, sourceAmount, otherItem, otherAmount, sourceTargetContext, otherTargetContext, out failure);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Attempts to swap all contents between this inventory and another compatible inventory.
    /// </summary>
    public bool TrySwapWithInventory(
        Inventory<TKey> other,
        ILayoutContext<TKey>? sourceTargetContext,
        ILayoutContext<TKey>? otherTargetContext,
        out InventoryFailure? failure)
    {
#pragma warning disable CS0618 // Swap currently reuses deprecated transfer internals for compatibility.
        return InventoryTransfer.TrySwapInventories(this, other, sourceTargetContext, otherTargetContext, out failure);
#pragma warning restore CS0618
    }

    private void ApplyPreparedTransaction(InventoryTransaction<TKey> transaction, bool cleared = false)
    {
        var args = ApplyPreparedTransactionDeferred(transaction, cleared);
        PublishChange(args);
    }

    internal InventoryChangedEventArgs<TKey>? ApplyPreparedTransactionDeferred(
        InventoryTransaction<TKey> transaction,
        bool cleared = false) =>
        ApplyPreparedTransactionCore(transaction, cleared);

    internal void PublishChange(InventoryChangedEventArgs<TKey>? args)
    {
        if (args != null)
            Changed?.Invoke(this, args);
    }

    private InventoryChangedEventArgs<TKey>? ApplyPreparedTransactionCore(
        InventoryTransaction<TKey> transaction,
        bool cleared = false,
        IEnumerable<InventoryConfigurationChanged<TKey>>? configurationChanged = null,
        bool requiresFullRefresh = false)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (transaction.Inventory != this)
            throw new InvalidOperationException("Transaction does not belong to this inventory.");
        if (transaction.IsApplied)
            throw new InvalidOperationException("Transaction has already been applied.");

        var addedEvents = new List<ItemAdded<TKey>>();
        var removedEvents = new List<ItemRemoved<TKey>>();
        var modifiedCaptures = new List<ModifiedEventCapture>();
        var layoutContextsBefore = CaptureLayoutContextsForReconciliation();

        foreach (var (index, delta) in transaction.AmountDeltas)
        {
            var instance = _items[index];
            int beforeAmount = instance.Amount;
            var beforeContexts = GetCapturedOrCurrentLayoutContexts(layoutContextsBefore, instance, index);
            _items[index].AddAmount(delta);
            modifiedCaptures.Add(new ModifiedEventCapture(instance, index, beforeAmount, instance.Amount, beforeContexts));
        }

        var removed = new List<(int index, ItemInstance<TKey> instance)>(transaction.Removed);
        removed.Sort((a, b) => b.index.CompareTo(a.index));
        foreach (var (index, instance) in removed)
        {
            var layoutContexts = GetCapturedOrCurrentLayoutContexts(layoutContextsBefore, instance, index);
            removedEvents.Add(new ItemRemoved<TKey>(instance, index, layoutContexts));
            RemoveAt(index);
        }

        foreach (var (instance, context) in transaction.Added)
        {
            AddItem(instance, context);
        }

        var reconciliation = ReconcileLayoutAfterMutation();
        foreach (var (instance, _) in transaction.Added)
        {
            int index = _items.IndexOf(instance);
            var layoutContexts = index >= 0
                ? _layout.GetContextsForStorageIndex(this, index)
                : Array.Empty<ILayoutContext<TKey>>();
            addedEvents.Add(new ItemAdded<TKey>(instance, index, layoutContexts));
        }

        var modifiedEvents = new List<ItemModified<TKey>>();
        foreach (var capture in modifiedCaptures)
        {
            IReadOnlyList<ILayoutContext<TKey>> afterContexts = Array.Empty<ILayoutContext<TKey>>();
            int currentIndex = _items.IndexOf(capture.Instance);
            if (currentIndex >= 0)
                afterContexts = _layout.GetContextsForStorageIndex(this, currentIndex);

            modifiedEvents.Add(new ItemModified<TKey>(
                capture.Instance,
                capture.OriginalIndex,
                capture.BeforeAmount,
                capture.AfterAmount,
                capture.BeforeLayoutContexts,
                afterContexts));
        }

        var movedEvents = BuildReflowMovements(layoutContextsBefore);
        transaction.MarkApplied();

        bool hasChanges = transaction.AmountDeltas.Count > 0 || transaction.Removed.Count > 0 || transaction.Added.Count > 0;
        bool hasConfigurationChanges = configurationChanged != null && configurationChanged.Any();
        if (hasChanges || cleared || hasConfigurationChanges)
            _version++;
        if (!hasChanges && !cleared && !hasConfigurationChanges)
            return null;

        return new InventoryChangedEventArgs<TKey>(
            addedEvents,
            removedEvents,
            modifiedEvents,
            moved: movedEvents,
            cleared: cleared,
            configurationChanged: configurationChanged,
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
            requiresFullRefresh: requiresFullRefresh || reconciliation.RequiresFullRefresh);
    }

    private readonly struct ModifiedEventCapture
    {
        public ModifiedEventCapture(
            ItemInstance<TKey> instance,
            int originalIndex,
            int beforeAmount,
            int afterAmount,
            IReadOnlyList<ILayoutContext<TKey>> beforeLayoutContexts)
        {
            Instance = instance;
            OriginalIndex = originalIndex;
            BeforeAmount = beforeAmount;
            AfterAmount = afterAmount;
            BeforeLayoutContexts = beforeLayoutContexts;
        }

        public ItemInstance<TKey> Instance { get; }

        public int OriginalIndex { get; }

        public int BeforeAmount { get; }

        public int AfterAmount { get; }

        public IReadOnlyList<ILayoutContext<TKey>> BeforeLayoutContexts { get; }
    }

    /// <summary>
    /// Attempts to add an amount of an item definition to the inventory.
    /// </summary>
    /// <param name="definition">The item definition to add.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns><see langword="true"/> when the item is added and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(ItemDefinition<TKey> definition, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!TryFormulateAdd(definition, amount, context, out var tx, out failure) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to add an amount of the item definition resolved from a current or migrated definition id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="failure">A consumer-facing reason when the add is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <returns><see langword="true"/> when the item is added and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TKey definitionId, out InventoryFailure? failure, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryAdd(definition, out failure, amount, context);
    }

    /// <summary>
    /// Adds an amount of an item definition to the inventory or throws when the add is rejected.
    /// </summary>
    /// <param name="definition">The item definition to add.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <exception cref="InventoryOperationException">The add operation is rejected by validation, rules, capacity, or layout.</exception>
    public void Add(ItemDefinition<TKey> definition, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!TryAdd(definition, out var failure, amount, context))
            ThrowMutationFailure(failure, "Add operation failed.");
    }

    /// <summary>
    /// Adds an amount of the item definition resolved from a current or migrated definition id, or throws when rejected.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="context">Optional layout-specific placement context.</param>
    /// <exception cref="InventoryOperationException">The id cannot be resolved, or the add is rejected by validation, rules, capacity, or layout.</exception>
    public void Add(TKey definitionId, int amount = 1, ILayoutContext<TKey>? context = null)
    {
        if (!TryAdd(definitionId, out var failure, amount, context))
            ThrowMutationFailure(failure, "Add operation failed.");
    }

    /// <summary>
    /// Attempts to remove an amount from a specific item instance.
    /// </summary>
    /// <param name="instance">The item instance to remove from.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(ItemInstance<TKey> instance, out InventoryFailure? failure, int amount = 1)
    {
        if (!TryFormulateRemove(instance, amount, out var tx, out failure) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Removes an amount from a specific item instance or throws when the removal is rejected.
    /// </summary>
    /// <param name="instance">The item instance to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <exception cref="InventoryOperationException">The removal is rejected by validation, rules, capacity, or layout.</exception>
    public void Remove(ItemInstance<TKey> instance, int amount = 1)
    {
        if (!TryRemove(instance, out var failure, amount))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Attempts to remove an amount from the item at a storage index.
    /// </summary>
    /// <param name="index">The storage index to remove from.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveAtStorageIndex(int index, out InventoryFailure? failure, int amount = 1)
    {
        if (!TryFormulateRemoveAt(index, amount, out var tx, out failure) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Removes an amount from the item at a storage index or throws when the removal is rejected.
    /// </summary>
    /// <param name="index">The storage index to remove from.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <exception cref="InventoryOperationException">The removal is rejected by validation, rules, capacity, or layout.</exception>
    public void RemoveAtStorageIndex(int index, int amount = 1)
    {
        if (!TryRemoveAtStorageIndex(index, out var failure, amount))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Attempts to remove an amount by item definition using an item metadata selector.
    /// </summary>
    /// <param name="definition">The item definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="metadataMatch">How item metadata should be matched when selecting stacks.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch, out InventoryFailure? failure)
    {
        if (!TryFormulateRemoveByDefinition(definition, amount, metadataMatch, out var tx, out failure) || tx == null)
            return false;
        CommitTransaction(tx);
        return true;
    }

    /// <summary>
    /// Attempts to remove an amount by item definition, matching only structurally empty item metadata.
    /// </summary>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, out InventoryFailure? failure) =>
        TryRemoveByDefinition(definition, amount, ItemMetadataMatch.Empty, out failure);

    /// <summary>
    /// Attempts to remove an amount by item definition, matching exact item metadata.
    /// </summary>
    public bool TryRemoveByDefinition(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata, out InventoryFailure? failure) =>
        TryRemoveByDefinition(definition, amount, ItemMetadataMatch.Exact(metadata), out failure);

    /// <summary>
    /// Attempts to remove an amount of the item definition resolved from a current or migrated definition id.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="metadataMatch">How item metadata should be matched when selecting stacks.</param>
    /// <param name="failure">A consumer-facing reason when the removal is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when removal succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, ItemMetadataMatch metadataMatch, out InventoryFailure? failure)
    {
        if (!TryResolveRegisteredDefinitionId(definitionId, out var definition, out failure) || definition == null)
            return false;

        return TryRemoveByDefinition(definition, amount, metadataMatch, out failure);
    }

    /// <summary>
    /// Attempts to remove an amount of the resolved definition, matching only structurally empty item metadata.
    /// </summary>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, out InventoryFailure? failure) =>
        TryRemoveByDefinition(definitionId, amount, ItemMetadataMatch.Empty, out failure);

    /// <summary>
    /// Attempts to remove an amount of the resolved definition, matching exact item metadata.
    /// </summary>
    public bool TryRemoveByDefinition(TKey definitionId, int amount, InstanceMetadata? metadata, out InventoryFailure? failure) =>
        TryRemoveByDefinition(definitionId, amount, ItemMetadataMatch.Exact(metadata), out failure);

    /// <summary>
    /// Removes an amount by item definition or throws when the removal is rejected.
    /// </summary>
    /// <param name="definition">The item definition to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="metadataMatch">How item metadata should be matched when selecting stacks.</param>
    /// <exception cref="InventoryOperationException">The removal is rejected by validation, rules, capacity, or layout.</exception>
    public void RemoveByDefinition(ItemDefinition<TKey> definition, int amount, ItemMetadataMatch metadataMatch)
    {
        if (!TryRemoveByDefinition(definition, amount, metadataMatch, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Removes an amount by item definition, matching only structurally empty item metadata, or throws when rejected.
    /// </summary>
    public void RemoveByDefinition(ItemDefinition<TKey> definition, int amount)
    {
        if (!TryRemoveByDefinition(definition, amount, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Removes an amount by item definition, matching exact item metadata, or throws when rejected.
    /// </summary>
    public void RemoveByDefinition(ItemDefinition<TKey> definition, int amount, InstanceMetadata? metadata)
    {
        if (!TryRemoveByDefinition(definition, amount, metadata, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Removes an amount of the item definition resolved from a current or migrated definition id, or throws when rejected.
    /// </summary>
    /// <param name="definitionId">The current or migrated definition id to resolve through this inventory's catalog registry.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="metadataMatch">How item metadata should be matched when selecting stacks.</param>
    /// <exception cref="InventoryOperationException">The id cannot be resolved, or the removal is rejected by validation, rules, capacity, or layout.</exception>
    public void RemoveByDefinition(TKey definitionId, int amount, ItemMetadataMatch metadataMatch)
    {
        if (!TryRemoveByDefinition(definitionId, amount, metadataMatch, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Removes an amount of the resolved definition, matching only structurally empty item metadata, or throws when rejected.
    /// </summary>
    public void RemoveByDefinition(TKey definitionId, int amount)
    {
        if (!TryRemoveByDefinition(definitionId, amount, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Removes an amount of the resolved definition, matching exact item metadata, or throws when rejected.
    /// </summary>
    public void RemoveByDefinition(TKey definitionId, int amount, InstanceMetadata? metadata)
    {
        if (!TryRemoveByDefinition(definitionId, amount, metadata, out var failure))
            ThrowMutationFailure(failure, "Remove operation failed.");
    }

    /// <summary>
    /// Attempts to move an item between two layout contexts.
    /// </summary>
    /// <param name="contextFrom">The source layout context.</param>
    /// <param name="contextTo">The destination layout context.</param>
    /// <param name="failure">A consumer-facing reason when the move is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the move succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryMove(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? failure)
    {
        failure = null;

        var item = _layout.GetItemAt(this, contextFrom);

        if (item == null)
        {
            failure = InventoryFailures.Validation("Item not found in inventory.");
            return false;
        }

        int itemIndex = _items.IndexOf(item);
        var beforeContexts = itemIndex >= 0
            ? _layout.GetContextsForStorageIndex(this, itemIndex)
            : new List<ILayoutContext<TKey>> { contextFrom };
        var layoutContextsBefore = CaptureLayoutContextsForReconciliation();

        if (!_layout.TryMove(this, contextFrom, contextTo, out failure))
            return false;

        var reconciliation = ReconcileLayoutAfterMutation();
        var afterContexts = itemIndex >= 0
            ? _layout.GetContextsForStorageIndex(this, itemIndex)
            : new List<ILayoutContext<TKey>> { contextTo };

        var moved = new List<ItemMoved<TKey>>();
        if (!ContextListsEqual(beforeContexts, afterContexts))
        {
            var reportedBeforeContexts =
                beforeContexts.Count == 1 && LayoutContextEquals(beforeContexts[0], contextFrom)
                    ? new[] { contextFrom }
                    : beforeContexts;
            var reportedAfterContexts =
                afterContexts.Count == 1 && LayoutContextEquals(afterContexts[0], contextTo)
                    ? new[] { contextTo }
                    : afterContexts;
            moved.Add(new ItemMoved<TKey>(
                item,
                reportedBeforeContexts,
                reportedAfterContexts,
                ItemMovementCause.ExplicitMove));
        }
        moved.AddRange(BuildReflowMovements(
            layoutContextsBefore,
            new HashSet<ItemInstance<TKey>>(ItemInstanceReferenceComparer.Instance) { item }));

        if (moved.Count > 0 || reconciliation.AffectedLayoutContexts.Count > 0 || reconciliation.RequiresFullRefresh)
        {
            Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
                moved: moved,
                affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
                requiresFullRefresh: reconciliation.RequiresFullRefresh));
        }

        return true;
    }

    /// <summary>
    /// Moves an item between two layout contexts or throws when the move is rejected.
    /// </summary>
    /// <param name="contextFrom">The source layout context.</param>
    /// <param name="contextTo">The destination layout context.</param>
    /// <exception cref="InventoryOperationException">The move is rejected by validation or layout.</exception>
    public void Move(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo)
    {
        if (!TryMove(contextFrom, contextTo, out var failure))
            ThrowMutationFailure(failure, "Move operation failed.");
    }

    /// <summary>
    /// Attempts to swap two items between layout contexts.
    /// </summary>
    /// <param name="contextFrom">The first layout context.</param>
    /// <param name="contextTo">The second layout context.</param>
    /// <param name="failure">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the swap succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TrySwap(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? failure)
    {
        failure = null;

        var itemFrom = _layout.GetItemAt(this, contextFrom);
        var itemTo = _layout.GetItemAt(this, contextTo);

        if (itemFrom == null || itemTo == null)
        {
            failure = InventoryFailures.Validation("One or both of the items not found in inventory.");
            return false;
        }

        int fromIndex = _items.IndexOf(itemFrom);
        int toIndex = _items.IndexOf(itemTo);
        var firstBeforeContexts = fromIndex >= 0
            ? _layout.GetContextsForStorageIndex(this, fromIndex)
            : new List<ILayoutContext<TKey>> { contextFrom };
        var secondBeforeContexts = toIndex >= 0
            ? _layout.GetContextsForStorageIndex(this, toIndex)
            : new List<ILayoutContext<TKey>> { contextTo };
        var layoutContextsBefore = CaptureLayoutContextsForReconciliation();

        if (!_layout.TrySwap(this, contextFrom, contextTo, out failure))
        {
            return false;
        }

        var reconciliation = ReconcileLayoutAfterMutation();
        var moved = BuildReflowMovements(
            layoutContextsBefore,
            new HashSet<ItemInstance<TKey>>(ItemInstanceReferenceComparer.Instance) { itemFrom, itemTo });
        var changedEventArgs = new InventoryChangedEventArgs<TKey>(
            moved: moved,
            swapped: new List<ItemSwapped<TKey>>
            {
                new ItemSwapped<TKey>(
                    firstBeforeContexts.Count == 1 ? new[] { contextFrom } : firstBeforeContexts,
                    secondBeforeContexts.Count == 1 ? new[] { contextTo } : secondBeforeContexts,
                    itemTo,
                    itemFrom)
            },
            affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
            requiresFullRefresh: reconciliation.RequiresFullRefresh);

        Changed?.Invoke(this, changedEventArgs);

        return true;
    }

    /// <summary>
    /// Swaps two items between layout contexts or throws when the swap is rejected.
    /// </summary>
    /// <param name="contextFrom">The first layout context.</param>
    /// <param name="contextTo">The second layout context.</param>
    /// <exception cref="InventoryOperationException">The swap is rejected by validation or layout.</exception>
    public void Swap(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo)
    {
        if (!TrySwap(contextFrom, contextTo, out var failure))
            ThrowMutationFailure(failure, "Swap operation failed.");
    }

    /// <summary>
    /// Attempts to move an amount from one compatible stack into another.
    /// </summary>
    /// <param name="contextFrom">The source stack layout context.</param>
    /// <param name="contextTo">The destination stack layout context.</param>
    /// <param name="failure">A consumer-facing reason when the merge move is rejected; otherwise, <see langword="null"/>.</param>
    /// <param name="amount">Optional exact amount to move; when omitted, as much as possible is moved.</param>
    /// <returns><see langword="true"/> when the merge move succeeds and a change event is fired; otherwise, <see langword="false"/>.</returns>
    public bool TryMergeMove(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, out InventoryFailure? failure, int? amount = null)
    {
        failure = null;

        var itemFrom = _layout.GetItemAt(this, contextFrom);
        var itemTo = _layout.GetItemAt(this, contextTo);

        if (itemFrom == null || itemTo == null)
        {
            failure = InventoryFailures.Validation("One or both of the items not found in inventory.");
            return false;
        }

        if (!itemFrom.IsStackCompatible(itemTo))
        {
            failure = InventoryFailures.Stacking("Items are not stack compatible.");
            return false;
        }

        if (amount.HasValue && amount.Value <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }

        if (amount.HasValue && amount.Value > itemFrom.Amount)
        {
            failure = InventoryFailures.Validation("Not enough quantity to move.");
            return false;
        }

        int targetStack = itemTo.Amount;
        if (!TryResolveMaxStackSize(itemTo, out int targetMaxStack, out failure))
            return false;

        if (targetStack == targetMaxStack)
        {
            failure = InventoryFailures.Stacking("Target stack is already at max size, no items can be moved.");
            return false;
        }

        int room = targetMaxStack - targetStack;

        if (amount.HasValue && room < amount.Value)
        {
            failure = InventoryFailures.Stacking("Not enough room in target stack to move the requested amount.");
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
        if (!TryPrepareTransaction(tx, null, out var mappedTx, out failure) || mappedTx == null)
            return false;
        ApplyPreparedTransaction(mappedTx);
        return true;
    }

    /// <summary>
    /// Moves an amount from one compatible stack into another or throws when the merge move is rejected.
    /// </summary>
    /// <param name="contextFrom">The source stack layout context.</param>
    /// <param name="contextTo">The destination stack layout context.</param>
    /// <param name="amount">Optional exact amount to move; when omitted, as much as possible is moved.</param>
    /// <exception cref="InventoryOperationException">The merge move is rejected by validation, rules, capacity, or layout.</exception>
    public void MergeMove(ILayoutContext<TKey> contextFrom, ILayoutContext<TKey> contextTo, int? amount = null)
    {
        if (!TryMergeMove(contextFrom, contextTo, out var failure, amount))
            ThrowMutationFailure(failure, "Merge move operation failed.");
    }

    /// <summary>
    /// Attempts to rebuild current layout placement in current layout order using normal auto-placement.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when repack is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when layout repack succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Repacking preserves inventory item instances and storage order. It changes layout placement only.
    /// Repack is not sorting and does not use item comparer ordering.
    /// The active layout must implement <see cref="IRepackableInventoryLayout{TKey}"/>.
    /// </remarks>
    public bool TryRepackLayout(out InventoryFailure? failure)
    {
        var before = CaptureLayoutContextsByInstance();
        var orderedStorageIndices = GetStorageIndicesInCurrentLayoutOrder();
        var proposedContents = CreateCurrentContentsSnapshotInCurrentLayoutOrder();

        if (!TryCreateEmptyLayoutLike(_layout, out var proposedLayout, out failure) || proposedLayout == null)
            return false;

        if (!TryValidateProposedContents(proposedContents, _stackResolver, _capacityPolicy, proposedLayout, out failure))
            return false;

        ApplyLayoutRepack(proposedLayout, orderedStorageIndices, before);
        failure = null;
        return true;
    }

    /// <summary>
    /// Rebuilds current layout placement in current layout order using normal auto-placement, or throws when repack is rejected.
    /// </summary>
    /// <exception cref="InventoryOperationException">The repack operation is rejected.</exception>
    public void RepackLayout()
    {
        if (!TryRepackLayout(out var failure))
            ThrowMutationFailure(failure, "Layout repack failed.");
    }

    /// <summary>
    /// Attempts to sort the current layout without mutating inventory storage order.
    /// </summary>
    /// <param name="comparer">The item comparer used to order placed items.</param>
    /// <param name="failure">A consumer-facing reason when sorting is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when sorting succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Sorting changes only the current layout placement and reports moved layout contexts through <see cref="Changed"/>.</remarks>
    public bool TrySortLayout(IComparer<ItemInstance<TKey>> comparer, out InventoryFailure? failure)
    {
        if (comparer == null)
        {
            failure = InventoryFailures.Validation("Comparer cannot be null.");
            return false;
        }

        return TrySortLayout(new ItemSortContext<TKey>(comparer), out failure);
    }

    /// <summary>
    /// Sorts the current layout with an item comparer or throws when sorting is rejected.
    /// </summary>
    /// <param name="comparer">The item comparer used to order placed items.</param>
    /// <exception cref="InventoryOperationException">The sort is rejected by validation or layout.</exception>
    public void SortLayout(IComparer<ItemInstance<TKey>> comparer)
    {
        if (!TrySortLayout(comparer, out var failure))
            ThrowMutationFailure(failure, "Sort operation failed.");
    }

    /// <summary>
    /// Attempts to sort the current layout without mutating inventory storage order.
    /// </summary>
    /// <param name="comparison">The item comparison used to order placed items.</param>
    /// <param name="failure">A consumer-facing reason when sorting is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when sorting succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Sorting changes only the current layout placement and reports moved layout contexts through <see cref="Changed"/>.</remarks>
    public bool TrySortLayout(Comparison<ItemInstance<TKey>> comparison, out InventoryFailure? failure)
    {
        if (comparison == null)
        {
            failure = InventoryFailures.Validation("Comparer cannot be null.");
            return false;
        }

        return TrySortLayout(ItemSortContext<TKey>.FromComparison(comparison), out failure);
    }

    /// <summary>
    /// Sorts the current layout with an item comparison or throws when sorting is rejected.
    /// </summary>
    /// <param name="comparison">The item comparison used to order placed items.</param>
    /// <exception cref="InventoryOperationException">The sort is rejected by validation or layout.</exception>
    public void SortLayout(Comparison<ItemInstance<TKey>> comparison)
    {
        if (!TrySortLayout(comparison, out var failure))
            ThrowMutationFailure(failure, "Sort operation failed.");
    }

    /// <summary>
    /// Attempts to sort the current layout using layout-specific sorting instructions.
    /// </summary>
    /// <param name="sortContext">The sort context interpreted by the current layout.</param>
    /// <param name="failure">A consumer-facing reason when sorting is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when sorting succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>Layouts interpret sort contexts themselves; inventory storage order is not changed.</remarks>
    public bool TrySortLayout(IInventorySortContext<TKey> sortContext, out InventoryFailure? failure)
    {
        if (sortContext == null)
        {
            failure = InventoryFailures.Layout("Sort context cannot be null.");
            return false;
        }

        var before = CaptureLayoutContextsByInstance();
        if (!_layout.TrySort(this, sortContext, out failure))
            return false;

        var reconciliation = ReconcileLayoutAfterMutation();
        var moved = BuildReflowMovements(before, cause: ItemMovementCause.Sort);

        if (moved.Count > 0 || reconciliation.AffectedLayoutContexts.Count > 0 || reconciliation.RequiresFullRefresh)
        {
            Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
                moved: moved,
                affectedLayoutContexts: reconciliation.AffectedLayoutContexts,
                requiresFullRefresh: reconciliation.RequiresFullRefresh));
        }

        failure = null;
        return true;
    }

    /// <summary>
    /// Sorts the current layout with layout-specific sorting instructions or throws when sorting is rejected.
    /// </summary>
    /// <param name="sortContext">The sort context interpreted by the current layout.</param>
    /// <exception cref="InventoryOperationException">The sort is rejected by validation or layout.</exception>
    public void SortLayout(IInventorySortContext<TKey> sortContext)
    {
        if (!TrySortLayout(sortContext, out var failure))
            ThrowMutationFailure(failure, "Sort operation failed.");
    }

    private static void ThrowMutationFailure(InventoryFailure? failure, string fallbackMessage)
    {
        throw new InventoryOperationException(failure ?? InventoryFailures.Layout(fallbackMessage));
    }

    private Dictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>>? CaptureLayoutContextsForReconciliation()
    {
        return _layout is IInventoryLayoutReconciler<TKey>
            ? CaptureLayoutContextsByInstance()
            : null;
    }

    private Dictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>> CaptureLayoutContextsByInstance()
    {
        var contexts = new Dictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>>(
            _items.Count,
            ItemInstanceReferenceComparer.Instance);
        for (int i = 0; i < _items.Count; i++)
            contexts[_items[i]] = _layout.GetContextsForStorageIndex(this, i).ToList();
        return contexts;
    }

    private IReadOnlyList<ILayoutContext<TKey>> GetCapturedOrCurrentLayoutContexts(
        IReadOnlyDictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>>? captured,
        ItemInstance<TKey> instance,
        int currentIndex)
    {
        if (captured != null && captured.TryGetValue(instance, out var contexts))
            return contexts;

        return _layout.GetContextsForStorageIndex(this, currentIndex);
    }

    private InventoryLayoutReconciliationResult<TKey> ReconcileLayoutAfterMutation()
    {
        if (_layout is not IInventoryLayoutReconciler<TKey> reconciler)
            return InventoryLayoutReconciliationResult<TKey>.None;

        return reconciler.ReconcileAfterInventoryMutation(this)
            ?? InventoryLayoutReconciliationResult<TKey>.None;
    }

    private List<ItemMoved<TKey>> BuildReflowMovements(
        IReadOnlyDictionary<ItemInstance<TKey>, IReadOnlyList<ILayoutContext<TKey>>>? before,
        ISet<ItemInstance<TKey>>? excludedInstances = null,
        ItemMovementCause cause = ItemMovementCause.LayoutReflow)
    {
        var moved = new List<ItemMoved<TKey>>();
        if (before == null)
            return moved;

        for (int index = 0; index < _items.Count; index++)
        {
            var instance = _items[index];
            if ((excludedInstances != null && excludedInstances.Contains(instance)) ||
                !before.TryGetValue(instance, out var beforeContexts))
            {
                continue;
            }

            var afterContexts = _layout.GetContextsForStorageIndex(this, index);
            if (!ContextListsEqual(beforeContexts, afterContexts))
                moved.Add(new ItemMoved<TKey>(instance, beforeContexts, afterContexts, cause));
        }

        return moved;
    }

    private sealed class ItemInstanceReferenceComparer : IEqualityComparer<ItemInstance<TKey>>
    {
        public static ItemInstanceReferenceComparer Instance { get; } = new();

        public bool Equals(ItemInstance<TKey>? x, ItemInstance<TKey>? y) => ReferenceEquals(x, y);

        public int GetHashCode(ItemInstance<TKey> obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static bool ContextListsEqual(IReadOnlyList<ILayoutContext<TKey>> first, IReadOnlyList<ILayoutContext<TKey>> second)
    {
        if (first.Count != second.Count)
            return false;

        for (int i = 0; i < first.Count; i++)
        {
            if (!LayoutContextEquals(first[i], second[i]))
                return false;
        }

        return true;
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
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType != typeof(string))
                continue;

            var firstValue = property.GetValue(first);
            var secondValue = property.GetValue(second);
            if (!Equals(firstValue, secondValue))
                return false;
        }

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

        var removedEvents = new List<ItemRemoved<TKey>>();
        for (int index = 0; index < _items.Count; index++)
            removedEvents.Add(new ItemRemoved<TKey>(_items[index], index, _layout.GetContextsForStorageIndex(this, index)));

        foreach (var item in _items)
            item.DetachOwner(this);
        _items.Clear();
        _layout.OnInventoryCleared(this);
        Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(removed: removedEvents, cleared: true));
    }

    /// <summary>
    /// Replaces the entire inventory with the given entries after validating the replacement.
    /// </summary>
    /// <param name="entries">Definitions, amounts, and layout contexts to use as the replacement contents.</param>
    /// <remarks>
    /// Invalid entries with a <see langword="null"/> definition or non-positive amount are ignored.
    /// A successful replacement fires at most one <see cref="Changed"/> event. If validation fails,
    /// the original inventory remains unchanged and an <see cref="InventoryOperationException"/> is thrown.
    /// </remarks>
    public void ReplaceContents(IEnumerable<(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context)>? entries)
    {
        var validEntries = new List<(ItemDefinition<TKey> definition, int amount, ILayoutContext<TKey>? context)>();
        if (entries != null)
        {
            foreach (var (definition, amount, context) in entries)
            {
                if (definition == null || amount <= 0)
                    continue;
                validEntries.Add((definition, amount, context));
            }
        }

        if (_items.Count == 0 && validEntries.Count == 0)
            return;

        var builder = InventoryTransaction<TKey>.From(this);
        for (int index = _items.Count - 1; index >= 0; index--)
        {
            if (!builder.TryRemoveAtStorageIndex(index, out var removeError, _items[index].Amount))
                throw new InventoryOperationException(removeError ?? InventoryFailures.Unknown());
        }

        foreach (var (definition, amount, context) in validEntries)
        {
            if (!builder.TryAdd(definition, out var addError, amount, context))
                throw new InventoryOperationException(addError ?? InventoryFailures.Unknown());
        }

        if (!builder.TryBuild(null, out var transaction, out var failure) || transaction == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());

        ApplyPreparedTransaction(transaction, cleared: _items.Count > 0);
    }

    /// <summary>
    /// Captures a portable, deeply detached inventory snapshot.
    /// </summary>
    /// <returns>The captured non-generic snapshot.</returns>
    /// <exception cref="InventoryOperationException">
    /// A key, value, or custom layout cannot be represented by the registered snapshot codecs.
    /// </exception>
    public InventorySnapshot CaptureSnapshot()
    {
        if (!TryCaptureSnapshot(out var snapshot, out var failure) || snapshot == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Unknown());
        return snapshot;
    }

    /// <summary>
    /// Attempts to capture a portable, deeply detached inventory snapshot.
    /// </summary>
    /// <param name="snapshot">The complete snapshot when capture succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="failure">A consumer-facing reason when capture fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every persisted value and layout state was captured.</returns>
    public bool TryCaptureSnapshot(out InventorySnapshot? snapshot, out InventoryFailure? failure)
    {
        return InventorySnapshotCapture.TryCapture(this, out snapshot, out failure);
    }

    /// <summary>
    /// Serializes item instances and layout data through the legacy object-valued compatibility model.
    /// </summary>
    /// <returns>A legacy serialized inventory object graph.</returns>
    [Obsolete("Use CaptureSnapshot() for portable persistence. This legacy compatibility API is not round-trip safe with ordinary serializers.")]
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
                    .ToDictionary(pair => pair.Key, pair => pair.Value!)
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
    [Obsolete("This legacy compatibility API is retained until the portable snapshot restoration APIs replace it.")]
    public void Deserialize(SerializedInventory<TKey> data, bool strict = false)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        Clear();

        var builder = InventoryTransaction<TKey>.From(this);
        foreach (var serializedItem in data.Items)
        {
            var definition = Manager.Registry.Resolve(serializedItem.DefinitionId);

            InstanceMetadata? metadata = null;
            if (serializedItem.Metadata != null && serializedItem.Metadata.Count > 0)
            {
                metadata = new InstanceMetadata();
                metadata.RestoreMetadata(serializedItem.Metadata);
            }

            builder.TryAdd(definition, serializedItem.Amount, null, metadata, out var failure);
            if (strict && failure != null)
                throw new InvalidOperationException($"Failed to deserialize item {serializedItem.DefinitionId}: {failure}");
        }
        CommitTransaction(builder);

        _layout.RestorePersistentData(data.LayoutData as ILayoutPersistentData);
    }
}
