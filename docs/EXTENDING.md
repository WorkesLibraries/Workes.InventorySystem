# Extending The Inventory System

`Workes.InventorySystem` is designed so applications can replace domain behavior without taking ownership of inventory
mutation. Extension code supplies definitions, policies, rules, placement models, and portable codecs; application code
still mutates state through `Inventory<TKey>`.

This guide is for extension authors. For ordinary setup and usage, start with:

- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md).
- [Inventory Operations](INVENTORY_OPERATIONS.md).
- [Layouts](LAYOUTS.md).
- [Policies And Rules](POLICIES_AND_RULES.md).
- [Persistence](PERSISTENCE.md).

## Extension Ownership

Choose the narrowest extension point that owns the behavior:

| Concern | Extension point | Owns |
|---|---|---|
| Domain item authoring | `ItemDefinition<TKey>` subclass and `ItemSchema<TKey>` | Definition attributes, direct tags, and schema family |
| Maximum stack size | `IStackResolver<TKey>` | Positive maximum amount for one compatible stack |
| Shared non-spatial capacity | `ICapacityPolicy<TKey>` | Projected weight, bulk, total amount, or another inventory-wide resource |
| Gameplay constraints | `IRulePolicy<TKey>` and specialized rule contracts | Semantic, projected-state, or structural acceptance |
| Placement and presentation | `IInventoryLayout<TKey>` | Layout positions, contexts, movement, sorting, and placement state |
| Runtime tuning | Parameterized component contracts | Stable parameter descriptions and replacement component creation |
| Repacking | `IRepackableInventoryLayout<TKey>` capabilities | Empty compatible candidate layouts |
| Mutation-driven layout reflow | `IInventoryLayoutReconciler<TKey>` | Post-commit layout-owned reconciliation |
| Multi-cell dimensions | `IGridFootprintProvider<TKey>` | Stable rectangular footprint per definition |
| Layout-specific sorting intent | `IInventorySortContext<TKey>` | Instructions interpreted by one layout |
| Definition-ID persistence | `IInventorySnapshotKeyCodec<TKey>` | Portable encoding for a custom key type |
| Layout persistence | `IInventoryLayoutSnapshotCodec<TKey>` | Capture, decode, validation, and exact reconstruction |

Extensions do not own item lifetime, inventory storage order, atomic commit, or change-event publication. Those remain
inventory responsibilities.

## Shared Extension Invariants

All extension implementations should follow these rules:

- Keep validation side-effect free. A failed proposal must not mutate inventory, layout, policy, rule, or shared state.
- Keep results deterministic. Candidate ordering, placement scans, tie-breaking, and codec output must be stable.
- Treat definitions registered in the inventory catalog as canonical. Do not create detached same-ID replacements.
- Return useful `InventoryFailure` values from `Try...` and `Can...` contracts. Use stable, namespaced custom codes for
  extension-owned failures and keep `Message` human-readable. See [Failure Handling](FAILURES.md).
- Create isolated replacement objects for parameter changes and candidate layouts; do not mutate the active component.
- Deep-copy mutable layout state from `Clone()`.
- Let inventory-owned APIs commit and emit events. Layouts and policies do not normally invoke `Inventory.Changed`.
- Keep persisted IDs and versions stable once saves may exist.
- Make singleton codecs stateless and safe for concurrent calls.

Invalid constructor configuration and invalid definition data can throw. Conditional runtime validation should normally
return `false` with an `InventoryFailure`. The inventory catches the failure before commit. Custom extension behavior
should normally extend the failure system with stable namespaced failure codes, not custom exception types or new
failure categories; see [Extension-Authored Failures](FAILURES.md#extension-authored-failures).

## Validation And Simulation Lifecycle

An extension can participate at several stages:

```text
Caller intent
  -> formulation
     stack resolver
     layout merge candidates
     direct per-add placement
  -> transaction preparation
     layout context mapping
     canonical definition validation
     capacity policy
     semantic/projected rules
     structural rules
     final layout placement validation
  -> atomic commit
     storage changes
     layout add/remove callbacks
     optional layout reconciliation
     one final event
```

Transaction builders run the same behavior against a cloned simulation. A clone or validator that shares mutable state
with the live inventory can therefore cause mutations before commit. Validation methods must inspect proposals rather
than editing active state.

Amount deltas change existing stacks and do not create positions. Structural removals happen before structural
additions. Any layout simulation must evaluate that final ordering.

## Custom Definition Classes

Use a definition subclass when item creation has domain fields or a schema family. The class should own its schema and
hide schema selection from normal callers:

```csharp
public class EquipmentDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<EquipmentDefinition>("equipment")
            .RequireAttribute<int>("weight")
            .AddTag("core:equipment");

    public EquipmentDefinition(
        string id,
        int weight,
        params string[] tags)
        : this(id, Schema, weight, tags)
    {
    }

    protected EquipmentDefinition(
        string id,
        ItemSchema<string> schema,
        int weight,
        params string[] tags)
        : base(id, schema, tags)
    {
        DefineAttribute("weight", weight);
    }
}
```

The public constructor accepts domain data. The protected constructor exists only so derived definition classes can
pass their own child schema through shared authoring logic:

```csharp
public sealed class WeaponDefinition : EquipmentDefinition
{
    public new static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<WeaponDefinition>("weapon")
            .WithParent(EquipmentDefinition.Schema)
            .RequireAttribute<int>("damage")
            .AddTag("core:equipment.weapons");

    public WeaponDefinition(
        string id,
        int weight,
        int damage,
        params string[] tags)
        : base(id, Schema, weight, tags)
    {
        DefineAttribute("damage", damage);
    }
}
```

Use `DefineAttribute`, `DefineTag`, and `DefineTags` only during construction. Register all attribute and tag IDs in the
catalog before freeze. Schema inheritance should mirror the C# class hierarchy.

Do not expose a public `ItemSchema<TKey>` constructor parameter on a registered concrete definition class. Catalog
freeze rejects caller-selectable schemas because definitions of one concrete type should have one class-owned schema
contract.

## Custom Stack Resolvers

Implement `IStackResolver<TKey>` when built-in resolvers cannot express the maximum stack size. The resolver does not
decide compatibility; compatible stacks still require the same definition ID and structurally equal metadata.

```csharp
public sealed class DefinitionMaxStackResolver<TKey>
    : IStackResolver<TKey>
{
    private readonly string _attributeId;
    private readonly int _fallback;

    public DefinitionMaxStackResolver(
        string attributeId,
        int fallback)
    {
        _attributeId = attributeId;
        _fallback = fallback > 0
            ? fallback
            : throw new ArgumentOutOfRangeException(nameof(fallback));
    }

    public int ResolveMaxStackSize(
        Inventory<TKey> inventory,
        ItemInstance<TKey> instance)
    {
        if (!instance.Definition.Attributes.TryGet<int>(
                _attributeId,
                out var maximum))
        {
            return _fallback;
        }

        return maximum > 0
            ? maximum
            : throw new InventoryOperationException(
                $"Definition '{instance.Definition.Id}' has invalid '{_attributeId}'.");
    }
}
```

Return a positive value for every supported item. Resolver behavior affects additions, merging, transactions,
transfers, metadata changes, repacking, reconciliation, snapshot application, and runtime parameter validation.
During an `Inventory.Metadata` mutation, the supplied inventory is an isolated candidate containing the proposed root
metadata and the preserved contents. Capacity policies, rules, and layout validation follow the same rule: inspect
`inventory.Metadata` when application-convention state legitimately affects a decision, but never mutate it during
validation.

### Parameterized resolvers

Implement `IParameterizedStackResolver<TKey>` when a resolver supports inventory-owned runtime tuning:

```csharp
private static readonly IReadOnlyCollection<InventoryParameterDefinition>
    s_parameters =
        new[]
        {
            new InventoryParameterDefinition(
                "fallbackMaxStack",
                typeof(int),
                "Fallback maximum stack size.")
        };

public IReadOnlyCollection<InventoryParameterDefinition> Parameters =>
    s_parameters;

public bool TryCreateWithParameter(
    Inventory<TKey> inventory,
    string parameterId,
    object? value,
    out IStackResolver<TKey>? resolver,
    out InventoryFailure? failure)
{
    resolver = null;

    if (parameterId != "fallbackMaxStack" ||
        value is not int maximum ||
        maximum <= 0)
    {
        failure = InventoryFailure.Create(
            InventoryFailureKind.Configuration,
            InventoryFailureCodes.ConfigurationUnsupportedParameter,
            "Parameter 'fallbackMaxStack' expects a positive Int32.",
            component: nameof(DefinitionMaxStackResolver<TKey>),
            source: parameterId);
        return false;
    }

    resolver = new DefinitionMaxStackResolver<TKey>(
        _attributeId,
        maximum);
    failure = null;
    return true;
}
```

The method creates a replacement; it does not edit the active resolver. The inventory validates current contents and
requested rebuild actions before committing it.

The parameter must also participate in the component's normal decisions. In this example, the replacement stores
`maximum` in `_fallback`, and `ResolveMaxStackSize(...)` reads that field whenever the definition does not provide its
own maximum:

```csharp
public int ResolveMaxStackSize(
    Inventory<TKey> inventory,
    ItemInstance<TKey> instance)
{
    return instance.Definition.Attributes.TryGet<int>(
        _attributeId,
        out var definitionMaximum)
            ? definitionMaximum
            : _fallback;
}
```

The parameter contract does not apply the behavior automatically. It only describes the available setting and creates
a correctly configured replacement. The resolver, policy, or layout must use the stored value in its ordinary
resolution, validation, placement, or query methods.

## Parameterized Components

Parameterized resolvers, capacity policies, and layouts use the same pattern:

| Contract | Replacement result |
|---|---|
| `IParameterizedStackResolver<TKey>` | `IStackResolver<TKey>` |
| `IParameterizedCapacityPolicy<TKey>` | `ICapacityPolicy<TKey>` |
| `IParameterizedInventoryLayout<TKey>` | `IInventoryLayout<TKey>` preserving placement |
| `IParameterizedRepackableInventoryLayout<TKey>` | Empty layout with changed configuration |

`InventoryParameterDefinition` exposes a stable ID, expected value type, and description. Treat IDs as public
configuration: centralize constants, validate exact types, and do not repurpose an existing ID.

Normal code changes parameters through inventory APIs. The extension only creates a proposed component.

There are therefore two separate implementation responsibilities:

1. `TryCreateWithParameter(...)` validates the requested value and returns a replacement containing it.
2. The component's normal decision methods consult the stored value. For example, a capacity policy compares projected
   bulk with `_maximum`, while a layout uses its configured dimensions when validating or assigning contexts.

Creating a replacement without reading the new value during normal decisions produces a parameter that appears to
change successfully but has no behavioral effect.

## Custom Capacity Policies

Capacity policies model a shared non-spatial resource such as weight, bulk, total quantity, volume, or energy. A
definition-specific gameplay limit is usually a rule instead.

`CanApply(...)` receives current state plus a normalized semantic transaction. `NormalizedInventoryTransaction<TKey>`
is extension-facing validation data, not a user-authored operation builder. Its `Added` and `Removed` collections
contain `(definition, metadata, amount)` groups; amount deltas have already become semantic additions or removals.
Capacity logic must not depend on storage indices.

```csharp
public sealed class BulkCapacityPolicy<TKey>
    : ICapacityPolicy<TKey>
{
    private readonly string _bulkAttributeId;
    private readonly int _maximum;

    public BulkCapacityPolicy(
        string bulkAttributeId,
        int maximum)
    {
        _bulkAttributeId = bulkAttributeId;
        _maximum = maximum;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? failure)
    {
        var current = inventory.Items.Sum(
            item => Bulk(item.Definition) * item.Amount);
        var added = transaction.Added.Sum(
            entry => Bulk(entry.definition) * entry.amount);
        var removed = transaction.Removed.Sum(
            entry => Bulk(entry.definition) * entry.amount);

        var projected = current + added - removed;
        if (projected > _maximum)
        {
            failure = InventoryFailure.Create(
                InventoryFailureKind.Capacity,
                "com.example.inventory.capacity.bulk_exceeded",
                $"Bulk capacity exceeded: {projected}/{_maximum}.",
                component: nameof(BulkCapacityPolicy<TKey>));
            return false;
        }

        failure = null;
        return true;
    }

    public bool CanAdd(
        Inventory<TKey> inventory,
        ItemInstance<TKey> instance,
        out InventoryFailure? failure)
    {
        var projected =
            inventory.Items.Sum(item => Bulk(item.Definition) * item.Amount) +
            Bulk(instance.Definition) * instance.Amount;

        if (projected <= _maximum)
        {
            failure = null;
            return true;
        }

        failure = InventoryFailure.Create(
            InventoryFailureKind.Capacity,
            "com.example.inventory.capacity.bulk_exceeded",
            $"Bulk capacity exceeded: {projected}/{_maximum}.",
            component: nameof(BulkCapacityPolicy<TKey>));
        return false;
    }

    private int Bulk(ItemDefinition<TKey> definition) =>
        definition.Attributes.GetOrDefault<int>(_bulkAttributeId);
}
```

Inventory mutation uses `CanApply(...)`; `CanAdd(...)` remains useful for direct custom checks. A parameterized capacity
policy follows the same replacement-instance pattern and must not mutate current state.

## Custom Rules

Choose the narrowest rule representation that can express the constraint:

| Contract | Use when |
|---|---|
| `IRulePolicy<TKey>` | Semantic added/removed definition, metadata, and quantity groups are sufficient |
| `InventorySnapshotRulePolicy<TKey>` | The projected final quantities or unique definitions are needed |
| `IInventoryStructuralRulePolicy<TKey>` | Storage indices, stack instances, or structural additions/removals matter |

All registered rules implement `IRulePolicy<TKey>`. A rule can additionally implement snapshot or structural
capabilities. Enabled rules run by descending priority and then insertion order. Snapshot rules receive a lazy
`InventoryRuleSnapshot<TKey>`; structural rules run afterward when structural data is available.

For a final quantity limit:

```csharp
public sealed class MaxDefinitionAmountRule<TKey>
    : InventorySnapshotRulePolicy<TKey>
{
    private readonly ItemDefinition<TKey> _definition;
    private readonly int _maximum;

    public MaxDefinitionAmountRule(
        string id,
        ItemDefinition<TKey> definition,
        int maximum)
    {
        Id = id;
        _definition = definition;
        _maximum = maximum;
    }

    protected override bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out InventoryFailure? failure)
    {
        var projected = snapshot.GetQuantity(_definition);
        if (projected > _maximum)
        {
            failure = InventoryFailure.Create(
                InventoryFailureKind.Rules,
                "com.example.inventory.rules.max_definition_amount",
                $"Cannot carry more than {_maximum} of '{_definition.Id}'.",
                component: nameof(MaxDefinitionAmountRule<TKey>));
            return false;
        }

        failure = null;
        return true;
    }
}
```

Rules must evaluate the proposal they receive. They should not mutate inventory or rely on another rule having run
first. Keep IDs stable when rules are replaced or configured at runtime.

## Custom Layouts

A layout owns the reversible mapping between inventory storage indices and presentation positions. It does not own
items, amounts, stack limits, capacity, rules, or metadata.

The examples below use one fixed `ShelfLayout<TKey>` backed by:

```csharp
private readonly List<int?> _shelves;
```

`null` is an empty shelf. A value is an index into `inventory.Items`. Moving presentation changes `_shelves`, never
`Inventory.Items`.

### Contexts

Direct contexts identify one position. Mapped contexts represent transaction-level added-entry placement:

```csharp
public sealed class ShelfLayoutContext<TKey>
    : ILayoutContext<TKey>
{
    public int ShelfIndex { get; }
    public bool IsMapped { get; }

    public IReadOnlyDictionary<int, int>
        AddedEntryShelves { get; }

    private ShelfLayoutContext(int shelfIndex)
    {
        ShelfIndex = shelfIndex;
        AddedEntryShelves =
            new Dictionary<int, int>();
    }

    private ShelfLayoutContext(
        IReadOnlyDictionary<int, int> addedEntryShelves)
    {
        ShelfIndex = -1;
        IsMapped = true;
        AddedEntryShelves =
            new Dictionary<int, int>(addedEntryShelves);
    }

    public static ShelfLayoutContext<TKey> Single(int shelfIndex) =>
        new(shelfIndex);

    public static ShelfLayoutContext<TKey> Map(
        IReadOnlyDictionary<int, int> addedEntryShelves) =>
        new(addedEntryShelves);
}
```

Mapped dictionary keys refer to `InventoryTransaction<TKey>.Added` indices, not inventory storage indices. Validate
negative positions, out-of-range added-entry indices, duplicate targets, and context types.

After validation, copy the current added-entry contexts, apply the mapped positions, and create a structurally
equivalent transaction through the public placement-copy API:

```csharp
var contexts = transaction.Added
    .Select(entry => entry.context)
    .ToArray();

foreach (var (addedIndex, shelfIndex) in shelfContext.AddedEntryShelves)
    contexts[addedIndex] = ShelfLayoutContext<TKey>.Single(shelfIndex);

mappedTransaction = transaction.WithAddedEntryContexts(contexts);
failure = null;
return true;
```

`WithAddedEntryContexts(...)` requires exactly one context per `Added` entry. It preserves the target inventory, amount
deltas, removals, and added item instances, so a layout can express placement without gaining authority to rewrite the
transaction's structural changes. It also rejects already-applied transactions. Per-add builder contexts remain the
ergonomic placement route; transaction-level mapped contexts remain useful for deferred multi-entry placement.

### Layout method lifecycle

| Group | Methods | Responsibility |
|---|---|---|
| Query | `GetPositionCount`, `GetAddressableContexts`, `GetItemAt`, context reverse lookup | Describe current placement without mutation |
| Formulation | `GetMergeCandidates`, `TryApplyPlacementContext`, `CanAcceptNewItem` | Interpret add intent and direct placement |
| Validation | `CanSatisfyPlacement` | Prove the complete final structural state fits |
| Placement operations | `TryMove`, `TrySwap`, `TrySort` | Mutate layout state only |
| Storage notifications | `OnItemAdded`, `OnItemRemoved`, `OnInventoryCleared` | Keep stored indices aligned after commit |
| Infrastructure | `Clone`, legacy persistent-data methods, `SnapshotCodec` | Simulation and portable round trips |

Query methods must be deterministic and side-effect free. Multi-position layouts return every occupied context for a
storage index.

### Query and merge behavior

For the shelf layout:

- `GetAddressableContexts(...)` returns every shelf, including empty shelves.
- `GetItemAt(...)` validates a direct context and resolves `_shelves[index]` through `inventory.Items`.
- `GetContextsForStorageIndex(...)` returns the shelf containing that storage index.
- `TryGetContextForStorageIndex(...)` returns its first context.
- `GetMergeCandidates(...)` returns only the item at a direct shelf, or occupied storage indices in shelf order for
  context-less adds.

Merge candidate order affects which compatible stack receives quantity first. Never enumerate an unordered set when
order is observable.

### Final-state placement simulation

`CanSatisfyPlacement(...)` validates a local copy:

1. Validate amount-delta and removal indices.
2. Copy `_shelves`.
3. Apply removals before additions.
4. Clear removed indices and decrement every stored index greater than each removal.
5. Set the first future storage index to `inventory.Items.Count - removedCount`.
6. Place each `Added` entry at its direct context or first automatic shelf.
7. Store `firstFutureIndex + addedIndex`.
8. Reject invalid types, ranges, duplicate targets, occupied targets, or exhausted space.

Amount deltas do not allocate positions. Simulation must not invoke live callbacks or modify `_shelves`.

`CanAcceptNewItem(...)` is the single-entry form. It validates a direct context or confirms that automatic placement
has room.

### Storage callbacks

After commit:

- `OnItemAdded(...)` stores the new storage index at the selected or automatic shelf.
- `OnItemRemoved(...)` clears the removed index and decrements every stored index greater than it.
- `OnInventoryCleared(...)` sets every shelf to `null`.

Callbacks synchronize already accepted storage changes. They are not another rejection phase.

### Move, swap, and sort

`TryMove(...)` and `TrySwap(...)` alter `_shelves` only. Validate context type, range, empty sources, and occupied
destinations according to layout semantics.

Simple shelf sorting can accept `ItemSortContext<TKey>`:

```csharp
public bool TrySort(
    Inventory<TKey> inventory,
    IInventorySortContext<TKey> sortContext,
    out InventoryFailure? failure)
{
    if (sortContext is not ItemSortContext<TKey> itemSort)
    {
        failure = InventoryFailure.Create(
            InventoryFailureKind.Layout,
            InventoryFailureCodes.LayoutRejected,
            "Invalid sort context type.",
            component: nameof(ShelfLayout<TKey>));
        return false;
    }

    var occupied = _shelves
        .Select((storageIndex, shelfIndex) =>
            (storageIndex, shelfIndex))
        .Where(entry => entry.storageIndex.HasValue)
        .Select(entry =>
            (storageIndex: entry.storageIndex!.Value,
             entry.shelfIndex))
        .ToList();

    occupied.Sort((left, right) =>
    {
        var compared = itemSort.Comparer.Compare(
            inventory.Items[left.storageIndex],
            inventory.Items[right.storageIndex]);
        return compared != 0
            ? compared
            : left.shelfIndex.CompareTo(right.shelfIndex);
    });

    for (var shelf = 0; shelf < _shelves.Count; shelf++)
    {
        _shelves[shelf] =
            shelf < occupied.Count
                ? occupied[shelf].storageIndex
                : null;
    }

    failure = null;
    return true;
}
```

The former shelf index is a deterministic tie-breaker. A custom sort context can carry richer layout-specific
instructions, but the layout must reject unsupported context types clearly.

Layouts do not classify or publish movement. Inventory compares contexts and assigns `ItemMovementCause.ExplicitMove`,
`Sort`, `Repack`, or `LayoutReflow`.

### Cloning

`Clone()` must preserve the concrete layout type, configuration, placement, and codec association while sharing no
mutable placement collections:

```csharp
public IInventoryLayout<TKey> Clone()
{
    var clone = new ShelfLayout<TKey>(_shelves.Count);
    clone._shelves.Clear();
    clone._shelves.AddRange(_shelves);
    return clone;
}
```

Derived layouts must not inherit a base `Clone()` that returns the base type and silently drops derived state.
Transaction builders and inventory creation depend on cloning before simulation.

### Optional layout capabilities

Implement capabilities only when their semantics are meaningful:

| Capability | Contract |
|---|---|
| Context-less compaction | `IRepackableInventoryLayout<TKey>` |
| Parameter change preserving placement | `IParameterizedInventoryLayout<TKey>` |
| Parameter change rebuilding placement | `IParameterizedRepackableInventoryLayout<TKey>` |
| Post-mutation survivor reflow | `IInventoryLayoutReconciler<TKey>` |

A repack capability returns an empty **placement map** with equivalent configuration, not an inventory with its item
contents removed. Inventory retains the source contents separately, chooses their visible order, and simulates adding
every entry to that proposed map. If any entry cannot be placed or validation fails, the proposal is discarded and the
active inventory is unchanged.

A parameterized preserve path returns a replacement with existing placement translated exactly. A parameterized repack
path returns an empty placement map with the proposed configuration so that inventory can rebuild placement under the
new rules. Merely implementing the repackable contract does not cause ordinary parameter changes to use this path:
`TrySetLayoutParameter(...)` uses the placement-preserving method by default. The empty-map method is called only when
the caller explicitly includes `InventoryParameterMutationActions.RepackLayout`.

Both direct and parameterized layout repack preserve the existing item instances and `Inventory.Items` storage order.
They rebuild only layout-owned placement and report changed positions as `ItemMoved<TKey>` with the `Repack` cause. A
parameterized repack additionally reports the layout configuration change and requests a full refresh because the
addressable layout topology may have changed.

A reconciler runs after an accepted mutation. It may update layout-owned presentation state but cannot reject the
operation or mutate inventory contents:

```csharp
public InventoryLayoutReconciliationResult<TKey>
    ReconcileAfterInventoryMutation(Inventory<TKey> inventory)
{
    RebuildPresentationOrder(inventory);
    return InventoryLayoutReconciliationResult<TKey>.None;
}
```

Inventory reports surviving context changes as `LayoutReflow`. Return supplemental contexts only for other
layout-owned presentation changes. Set `RequiresFullRefresh` only when movement and affected contexts cannot completely
describe the observable change.

### Event accuracy

Layouts normally do not raise `Inventory.Changed`. Event accuracy depends on:

- returning complete before/after contexts;
- keeping storage-index maps synchronized in callbacks;
- returning stable value-like contexts where practical;
- reporting all occupied cells for multi-position items;
- using reconciliation only for post-commit presentation changes.

Complexity alone does not require a full refresh. Precise sorting and repacking can remain incremental.

## Portable Layout Persistence

Every `IInventoryLayout<TKey>` exposes one stateless `IInventoryLayoutSnapshotCodec<TKey>` singleton. This is a complete
round-trip contract:

```text
TryCapture
  -> portable SnapshotValue
TryDecode
  -> inert validated candidate
TryCreateExactLayout
  -> isolated exactly placed layout
```

If capture succeeds, the data must decode and reconstruct exactly against equivalent runtime configuration.
`CaptureSnapshot()` verifies that round trip before returning. Exact restoration may reject genuinely changed shape,
restrictions, footprints, or configuration.

```csharp
public IInventoryLayoutSnapshotCodec<TKey> SnapshotCodec =>
    ShelfLayoutSnapshotCodec<TKey>.Instance;
```

`ShelfLayoutSnapshotCodec<TKey>` implements the three methods described below and exposes a singleton `Instance`,
stable kind `com.example.inventory.layout.shelf`, and version `1`.

The shelf codec uses:

- global kind `com.example.inventory.layout.shelf`;
- data version `1`;
- `shelfCount`;
- `shelves`, an ordered list of stable snapshot entry IDs or null.

Never persist storage indices in portable snapshot data. Capture resolves item instances:

```csharp
var references = new List<object?>();

foreach (var storageIndex in layout.Shelves)
{
    if (!storageIndex.HasValue)
    {
        references.Add(null);
        continue;
    }

    var item = context.Inventory.Items[storageIndex.Value];
    if (!context.TryGetEntryId(item, out var entryId))
    {
        data = null;
        failure = InventoryFailure.Create(
            InventoryFailureKind.Snapshot,
            InventoryFailureCodes.SnapshotMalformed,
            "Shelf references an unknown inventory item.",
            component: nameof(ShelfLayoutSnapshotCodec<TKey>));
        return false;
    }

    references.Add(entryId);
}
```

Encode the count and references through package-supported values:

```csharp
data = SnapshotValue.Object(new[]
{
    new SnapshotNamedValue
    {
        Name = "shelfCount",
        Value = InventorySnapshotCodecs.Encode(layout.ShelfCount)
    },
    new SnapshotNamedValue
    {
        Name = "shelves",
        Value = InventorySnapshotCodecs.Encode(references)
    }
});
```

Decode must validate:

- exact layout kind and supported data version;
- object shape and required unique properties;
- positive shelf count matching reference count;
- null or string references only;
- every non-null ID exists in the decode context;
- each entry appears exactly once;
- every snapshot entry is placed.

Produce `InventoryLayoutSnapshotCandidate<TKey>` with one `ShelfLayoutContext<TKey>` per placed entry. Do not mutate a
live layout while decoding.

Exact reconstruction receives validated candidate data plus entry-ID mappings:

```csharp
public bool TryCreateExactLayout(
    InventoryLayoutSnapshotRestoreContext<TKey> context,
    out IInventoryLayout<TKey>? layout,
    out InventoryFailure? failure)
{
    layout = null;

    if (context.TargetLayout is not ShelfLayout<TKey> target ||
        !TryDecodeReferences(
            context.Candidate.Data,
            out var references,
            out failure) ||
        references.Count != target.ShelfCount)
    {
        failure ??= InventoryFailure.Create(
            InventoryFailureKind.Layout,
            InventoryFailureCodes.LayoutRejected,
            "Saved shelf shape is incompatible.",
            component: nameof(ShelfLayoutSnapshotCodec<TKey>));
        return false;
    }

    var storageMap = new List<int?>();
    foreach (var reference in references)
    {
        if (reference == null)
        {
            storageMap.Add(null);
        }
        else if (context.StorageIndices.TryGetValue(
                     reference,
                     out var storageIndex))
        {
            storageMap.Add(storageIndex);
        }
        else
        {
            failure = InventoryFailure.Create(
                InventoryFailureKind.Snapshot,
                InventoryFailureCodes.SnapshotMalformed,
                $"Unknown snapshot entry '{reference}'.",
                component: nameof(ShelfLayoutSnapshotCodec<TKey>));
            return false;
        }
    }

    layout = ShelfLayout<TKey>.FromSnapshot(
        target,
        storageMap);
    failure = null;
    return true;
}
```

`FromSnapshot(...)` represents an application-defined constructor or factory that copies immutable configuration and
the validated map into a new layout. It must not return or mutate `context.TargetLayout`.

Codecs must also:

- deep-copy and validate their `SnapshotValue` output;
- preserve historical decode support when the data version changes;
- use one global kind meaning across all `TKey` types;
- remain stateless and concurrency safe;
- preserve enough shape and placement data for exact reconstruction.

### Legacy layout persistence

`GetPersistentData()` and `RestorePersistentData(...)` remain required layout infrastructure used by legacy
serialization and often by cloning. They are not the portable snapshot wire contract. `SnapshotCodec` must use
serializer-friendly `SnapshotValue` data and stable entry IDs.

## Custom Inventory Key Codecs

Built-in scalar key types need no configuration. A custom `TKey` assigns one separate codec through its type:

```csharp
[InventorySnapshotKeyCodec(typeof(ItemKeyCodec))]
public sealed record ItemKey(string Value);

public sealed class ItemKeyCodec
    : IInventorySnapshotKeyCodec<ItemKey>
{
    public string FormatId =>
        "com.example.inventory.key.item";

    public int CurrentVersion => 1;

    public bool TryEncode(
        ItemKey value,
        out SnapshotValue? encoded,
        out InventoryFailure? failure)
    {
        encoded = SnapshotValue.String(value.Value);
        failure = null;
        return true;
    }

    public bool TryDecode(
        SnapshotValue encoded,
        int version,
        out ItemKey value,
        out InventoryFailure? failure)
    {
        if (version != 1 ||
            encoded.Kind != SnapshotValueKind.String ||
            encoded.StringValue == null)
        {
            value = default!;
            failure = InventoryFailure.Create(
                InventoryFailureKind.Snapshot,
                InventoryFailureCodes.SnapshotCodecRejected,
                "Unsupported item-key snapshot data.",
                component: nameof(ItemKeyCodec));
            return false;
        }

        value = new ItemKey(encoded.StringValue);
        failure = null;
        return true;
    }
}
```

The codec type needs a public parameterless constructor. Keep `FormatId` stable, increment `CurrentVersion` when the
wire shape changes, and decode every historical version the application still supports. The `workes.inventory.`
prefix is reserved.

Custom codecs extend definition IDs only. Item and inventory metadata support package-owned portable scalars,
one-dimensional arrays, and `List<T>` recursively; custom domain-object metadata codecs are intentionally unsupported.

## Custom Footprint Providers

Implement `IGridFootprintProvider<TKey>` when footprint cannot be expressed by
`AttributeGridFootprintProvider<TKey>`.

```csharp
public sealed class DefinitionFootprintProvider<TKey>
    : IGridFootprintProvider<TKey>
{
    private readonly IReadOnlyDictionary<TKey, GridFootprint>
        _footprints;

    public DefinitionFootprintProvider(
        IReadOnlyDictionary<TKey, GridFootprint> footprints)
    {
        _footprints = footprints;
    }

    public GridFootprint GetFootprint(
        ItemDefinition<TKey> definition) =>
        _footprints.TryGetValue(definition.Id, out var footprint)
            ? footprint
            : new GridFootprint(1, 1);
}
```

Return positive stable rectangles. Footprints participate in placement, movement, sorting, repack, reconciliation, and
exact snapshot compatibility. Changing a persisted definition's footprint can make exact restoration fail.

Compact multi-cell sorting is a deterministic heuristic, not globally optimal bin packing. Stable provider output and
stable sort tie-breakers are essential.

## Extension Checklist

### All extensions

- The extension owns only its documented concern.
- Validation is deterministic and side-effect free.
- Failures return useful errors and leave active state unchanged.
- Runtime changes flow through inventory-owned APIs.
- Registered definitions remain canonical.

### Parameterized component checklist

- Parameter IDs and value types are stable.
- `TryCreateWithParameter(...)` returns a replacement object.
- The active component and shared configuration are not mutated.

### Rules and policies

- Capacity uses normalized semantic quantities rather than storage indices.
- Rules use the narrowest sufficient semantic, projected, or structural contract.
- Rule correctness does not depend on another rule's side effects or order.

### Layouts

- Query methods are side-effect free and return complete contexts.
- Merge candidates and automatic placement are deterministic.
- Final-state validation applies removals before additions.
- Storage callbacks keep every stored index aligned.
- Move, swap, and sort mutate placement only.
- `Clone()` preserves the concrete type and deep-copies mutable state.
- Optional repack and reconciliation capabilities have meaningful semantics.
- Contexts and reconciliation make event deltas accurate.
- `SnapshotCodec` captures, decodes, and exactly reconstructs all layout-owned state.

### Persistence

- Kind and format IDs are stable and globally unique.
- Versions are positive and historical versions remain decodable.
- Layout references use stable snapshot entry IDs, never storage indices.
- Decode validates complete structure without live mutation.
- Exact reconstruction returns an isolated layout.
- Capture round-trips under equivalent configuration.
- Codec singletons are stateless and concurrency safe.

## Common Extension Pitfalls

- Mutating inventory or active layout state during `Can...` or `TryCreate...` validation.
- Returning zero or negative stack sizes or footprints.
- Treating capacity as per-definition gameplay rules.
- Reading storage order as presentation order.
- Failing to decrement stored layout indices after removals.
- Allocating positions for amount deltas.
- Sharing mutable placement collections from `Clone()`.
- Returning a base layout type from a derived layout clone.
- Using unstable candidate ordering or comparer results without tie-breakers.
- Firing inventory events from extension code.
- Requesting full refresh merely because a delta is large.
- Persisting layout storage indices instead of stable entry IDs.
- Capturing layout data that cannot be exactly reconstructed.
- Changing codec IDs instead of versioning their data.
- Assuming custom metadata objects can register snapshot codecs.
- Replacing transaction structure when a layout should only call `WithAddedEntryContexts(...)`.

## Continue Reading

- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Policies And Rules](POLICIES_AND_RULES.md)
- [Layouts](LAYOUTS.md)
- [Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Events And UI Integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
