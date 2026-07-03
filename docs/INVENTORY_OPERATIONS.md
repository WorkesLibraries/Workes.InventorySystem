# Inventory Operations

`Inventory<TKey>` is the normal application-facing owner of runtime item stacks. Applications create inventories through an `InventoryManager<TKey>`, inspect their item instances, and route changes through inventory-owned APIs.

Before continuing, read:

- [Core Concepts](CONCEPTS.md) for identity and ownership.
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md) for constructing the item universe.

## Create An Inventory Manager

An `InventoryManager<TKey>` combines a frozen catalog with default inventory components:

| Constructor argument | Responsibility |
|---|---|
| `defaultStackResolver` | Determines stack limits and compatibility behavior used during add and merge operations. |
| `defaultCapacityPolicy` | Accepts or rejects proposed contents based on inventory capacity. |
| `defaultLayout` | Defines placement and presentation. |
| `catalog` | Supplies the canonical registered item definitions. |
| `defaultRules` | Optionally supplies semantic inventory rules. |

```csharp
var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(99),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);
```

The catalog is required and must be frozen before `CreateInventory()` succeeds.

```csharp
catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Freeze();

var inventory = manager.CreateInventory();
```

The manager exposes:

- `Catalog`, shared by all inventories it creates.
- `Registry`, a shortcut to `Catalog.Registry`.
- default stack resolver, capacity policy, layout, and rules.
- inventory factory methods.

## Create Inventories From Defaults Or Overrides

Use `CreateInventory()` to use every manager default:

```csharp
var backpack = manager.CreateInventory();
var chest = manager.CreateInventory();
```

Use the optional override parameters when one inventory needs different behavior:

```csharp
var hotbar = manager.CreateInventory(
    stackResolver: new FixedSizeStackResolver<string>(10),
    layout: new SlotLayout<string>(8),
    capacityPolicy: new UnlimitedCapacityPolicy<string>());
```

All inventories created by the manager still share its catalog and canonical definition objects.

## Inventory State

Important inventory members include:

| Member | Meaning |
|---|---|
| `Manager` | The manager that created the inventory. |
| `Catalog` | The shared item catalog. |
| `Items` | A read-only list of owned stacks in storage order. |
| `InstanceCount` | Number of item instances or stacks. |
| `TotalItemCount` | Sum of all stack amounts. |
| `Layout` | Current placement and presentation behavior. |
| `StackResolver` | Current stack-size behavior. |
| `CapacityPolicy` | Current capacity behavior. |
| `Rules` | Inventory-owned rules by ID. |
| `Attributes` | Inventory-level attributes. |
| `Changed` | Notification after committed content or placement changes. |

`Items` is storage order, not visual order. Use the layout when you need slots, cells, sections, equipment positions, or other presentation locations.

## Item Instances

An `ItemInstance<TKey>` represents one runtime stack owned by an inventory.

It exposes:

- `Definition`, the canonical registered item definition.
- `Amount`, controlled by inventory operations.
- `InstanceId`, a unique runtime identifier.
- `Metadata`, per-stack runtime data.

Application code does not construct item instances directly and cannot directly assign their amounts.

Instances are created by:

- inventory add operations.
- committed transactions and transfers.
- portable snapshot application.
- splits and rebuild operations.

Use the instance references returned through `Inventory.Items` or query methods when an operation targets a particular stack.

## Try Methods And Throwing Wrappers

Most expected-to-fail operations use a `Try...` method:

```csharp
if (!inventory.TryAdd(apple, out var error, amount: 5))
{
    // Show or log the consumer-facing rejection reason.
}
```

Use the throwing wrapper when the operation is expected to succeed:

```csharp
inventory.Add(apple, amount: 5);
```

The general pattern is:

| Conditional flow | Expected-success flow |
|---|---|
| `TryAdd(...)` | `Add(...)` |
| `TryRemove(...)` | `Remove(...)` |
| `TryMove(...)` | `Move(...)` |
| `TrySwap(...)` | `Swap(...)` |
| `TryMergeMove(...)` | `MergeMove(...)` |
| `TryRepackLayout(...)` | `RepackLayout()` |
| `TrySortLayout(...)` | `SortLayout(...)` |

`Try...` methods return `false` and an error when the proposed operation is rejected. Throwing wrappers raise `InvalidOperationException` with the same domain error.

## Add Items

Add a canonical registered definition through the inventory:

```csharp
inventory.Add(apple, amount: 5);
```

Conditional form:

```csharp
var added = inventory.TryAdd(
    apple,
    out var error,
    amount: 5);
```

Adding performs the complete inventory operation:

1. Confirms the exact definition object is registered in the inventory catalog.
2. Resolves the maximum stack size.
3. Fills compatible existing stacks when the layout exposes them as merge candidates.
4. Creates additional stacks for any remaining amount.
5. Validates the proposed result against layout, capacity, and rules.
6. Commits the complete result and emits one change notification.

The amount must be greater than zero.

### Place while adding

Layouts can accept an optional layout context:

```csharp
inventory.Add(
    potion,
    amount: 1,
    context: SlotLayoutContext<string>.Single(2));
```

A `null` context requests normal automatic placement where the layout supports it.

Context types are layout-specific. The layouts guide covers their detailed behavior.

## Remove Items

### Remove from a specific stack

```csharp
var stack = inventory.Find(apple).First();

inventory.Remove(stack, amount: 2);
```

Conditional form:

```csharp
var removed = inventory.TryRemove(
    stack,
    out var error,
    amount: 2);
```

The instance must belong to this inventory and contain at least the requested amount.

Removing the complete amount removes the instance. Partial removal preserves the instance with a smaller amount.

### Remove at a storage index

```csharp
inventory.RemoveAtStorageIndex(index: 0, amount: 1);
```

Storage indexes refer to `Inventory.Items`, not layout positions. Prefer instance references or layout contexts when that better matches the application workflow.

### Remove by definition

Remove an amount across matching stacks:

```csharp
inventory.RemoveByDefinition(
    apple,
    amount: 5,
    ignoreMetadata: true);
```

Conditional form:

```csharp
var removed = inventory.TryRemoveByDefinition(
    apple,
    amount: 5,
    ignoreMetadata: true,
    out var error);
```

`ignoreMetadata` controls which stacks may contribute:

- `true` permits removal across stacks regardless of metadata.
- `false` uses the first matching stack’s metadata as the reference. When that reference is non-empty, only structurally equal metadata contributes. An empty reference is treated as no metadata filter.

Pass the canonical registered definition even though removal searches matching definition IDs internally.

The complete amount must be available across eligible stacks or the operation is rejected without partial removal.

## Query Contents

Inventory queries return current counts or snapshot lists of matching item instances.

## Query By Definition

```csharp
var appleAmount = inventory.Count(apple);
var hasFiveApples = inventory.Contains(apple, amount: 5);
var appleStacks = inventory.Find(apple);
```

| API | Result |
|---|---|
| `Count(definition)` | Total amount across stacks using the exact definition object. |
| `Contains(definition, amount)` | Whether at least that total amount exists. |
| `Find(definition)` | Snapshot list of matching item instances. |

Definition queries use object identity, matching the catalog’s canonical-definition invariant. A detached same-ID definition does not match.

The list returned by `Find(...)` is a snapshot of the matches. Later inventory changes do not add or remove entries from that returned list, although its item-instance objects remain live readable handles.

## Query By Tag

Tag queries use catalog-resolved membership, including:

- schema tags.
- direct definition tags.
- generated parent tags.

```csharp
var tools = inventory.FindByTag("core:equipment.tools");
var toolAmount = inventory.CountByTag("core:equipment.tools");
```

`ContainsAllTags(...)` asks whether at least one item definition satisfies every requested tag:

```csharp
var hasFreshFood = inventory.ContainsAllTags(
    "core:food",
    "core:state.fresh");
```

It does not combine tags from unrelated items. One definition must satisfy the complete requested set.

All queried tag IDs must be declared in the inventory catalog.

## Query By Predicate

Use `FindWhere(...)` for runtime conditions involving amount, metadata, definition data, or other readable instance state:

```csharp
var largeStacks = inventory.FindWhere(item => item.Amount >= 10);

var polishedItems = inventory.FindWhere(
    item => item.Metadata.TryGet<string>("quality", out var quality) &&
            quality == "polished");
```

Like `Find(...)`, `FindWhere(...)` returns a snapshot list.

## Move Items Within A Layout

Move operations use layout contexts rather than storage indexes.

```csharp
var from = SlotLayoutContext<string>.Single(0);
var to = SlotLayoutContext<string>.Single(3);

inventory.Move(from, to);
```

Conditional form:

```csharp
if (!inventory.TryMove(from, to, out var error))
{
    // The source was empty or the layout rejected the destination.
}
```

Moving changes layout placement. It does not change the item’s amount or reorder `Inventory.Items`.

The current layout decides whether the destination is valid.

The changed event marks the directly targeted instance with `ItemMovementCause.ExplicitMove`. If the layout shifts
other surviving instances as a consequence, those movements use `ItemMovementCause.LayoutReflow` in the same event.
See [Events and UI integration](EVENTS_AND_UI.md#movement) for the complete cause model.

## Swap Layout Positions

Swap two occupied contexts:

```csharp
inventory.Swap(
    SlotLayoutContext<string>.Single(0),
    SlotLayoutContext<string>.Single(1));
```

Both contexts must contain items, and the layout must accept both resulting placements.

Use `TrySwap(...)` when an empty position or compatibility restriction is an expected branch.

## Merge Compatible Stacks

`MergeMove(...)` transfers an amount from one stack into another compatible stack within the same inventory.

```csharp
inventory.MergeMove(
    contextFrom: SlotLayoutContext<string>.Single(1),
    contextTo: SlotLayoutContext<string>.Single(0));
```

When `amount` is omitted, the operation moves as much as possible without exceeding the destination’s maximum stack size:

```csharp
inventory.MergeMove(from, to, amount: 2);
```

Stacks are compatible when they have:

- the same definition ID.
- structurally equal metadata.

The destination stack must have enough room for an explicitly requested amount. When the source becomes empty, its item instance is removed.

The entire merge is validated before commit.

## Clear Or Replace All Contents

`Clear()` removes every item instance:

```csharp
inventory.Clear();
```

Calling it on an already empty inventory is a no-op.

`ReplaceContents(...)` validates and replaces the complete inventory in one operation:

```csharp
inventory.ReplaceContents(
    new[]
    {
        (
            definition: apple,
            amount: 5,
            context: (ILayoutContext<string>?)SlotLayoutContext<string>.Single(0)
        ),
        (
            definition: potion,
            amount: 2,
            context: (ILayoutContext<string>?)SlotLayoutContext<string>.Single(1)
        )
    });
```

Replacement behavior:

- entries with a `null` definition or non-positive amount are ignored.
- valid entries are checked through normal definition, stack, capacity, rule, and layout validation.
- validation failure throws and preserves the original contents.
- a successful replacement emits at most one change event.

Passing `null` or no valid entries replaces a non-empty inventory with empty contents.

## Repack Layout Placement

Repacking rebuilds placement using the current visible layout order and normal automatic placement:

```csharp
if (!inventory.TryRepackLayout(out var error))
{
    // The current layout does not support inventory-owned repack,
    // or the proposed placement was rejected.
}
```

Repacking:

- preserves item instances.
- preserves `Inventory.Items` storage order.
- changes layout placement only.
- does not apply an item comparer.
- requires the active layout to implement `IRepackableInventoryLayout<TKey>`.

Use repack to remove placement gaps. Use sorting when you want comparer-driven order.

Slot, grid, multi-cell grid, and sectioned layouts support this capability. Entry layout does not because repack would
always be a no-op. Equipment layout does not because named slots are semantically meaningful. A custom capability
creates only an empty equivalently configured layout; the inventory performs placement, validation, atomic commit, and
event reporting. Rejection leaves current contents and placement unchanged.

## Sort Layout Placement

The simplest sorting form accepts a comparison:

```csharp
inventory.SortLayout(
    (left, right) => string.CompareOrdinal(
        left.Definition.Id,
        right.Definition.Id));
```

Conditional form:

```csharp
var sorted = inventory.TrySortLayout(
    (left, right) => string.CompareOrdinal(
        left.Definition.Id,
        right.Definition.Id),
    out var error);
```

Sorting changes layout placement, not `Inventory.Items` storage order.

Overloads accept:

- `IComparer<ItemInstance<TKey>>`.
- `Comparison<ItemInstance<TKey>>`.
- an `IInventorySortContext<TKey>` interpreted by the current layout.

Sorting support and behavior vary by layout. Some layouts use simple item ordering, multi-cell grids can prioritize compact packing, and equipment layouts do not support sorting.

The layouts guide owns detailed per-layout sorting behavior.

## Instance Metadata

`InstanceMetadata` stores loose per-stack key/value data.

Use metadata for runtime variation such as:

- quality rolls.
- durability state.
- ownership.
- quest flags.
- generated serial numbers.
- crafting annotations.

Use definition attributes instead when the value belongs to the item type and is shared by every instance.

There is no metadata catalog or metadata schema.

Metadata values must remain portable snapshot values: null, supported scalar values, one-dimensional arrays, and
`List<T>`, recursively. Dictionaries, enums, multidimensional arrays, arbitrary enumerable types, literal `object`
values, and custom domain objects are rejected. `Add`, `Set`, `Change`, `Replace`, and `Transform` validate the complete
proposed state before commit; their `Try` forms return `false` and leave existing metadata unchanged.

## Read Metadata

```csharp
var stack = inventory.Items.First();

if (stack.Metadata.TryGet<string>("quality", out var quality))
{
    // Use quality.
}
```

Other read APIs include:

| API | Meaning |
|---|---|
| `IsEmpty` | Whether the metadata container has no entries. |
| `AsReadOnly()` | Read-only dictionary view. |
| `ToDictionary()` | Mutable dictionary copy. |
| `StructuralEquals(other)` | Key/value equality used by stack compatibility. |

`AsReadOnly()` may reflect later mutations. `ToDictionary()` copies the dictionary container.

Stored values are not deep-cloned.
Treat stored arrays and lists as immutable after assignment. Mutating one through the original reference or a value
returned by `TryGet(...)`, `AsReadOnly()`, or `ToDictionary()` bypasses inventory-owned validation and change events.

## Mutate Metadata

Metadata mutation follows the same conditional/throwing pattern as inventory operations.

| Conditional | Throwing | Behavior |
|---|---|---|
| `TryAdd(...)` | `Add(...)` | Add only when the key is absent. |
| `TrySet(...)` | `Set(...)` | Add or replace a value. |
| `TryChange(...)` | `Change(...)` | Replace only when the key exists. |
| `TryRemove(...)` | `Remove(...)` | Remove an existing key. |
| `TryClear(...)` | `Clear()` | Remove all entries. |
| `TryReplace(...)` | `Replace(...)` | Replace the complete dictionary. |
| `TryTransform(...)` | `Transform(...)` | Mutate a proposed metadata clone. |

```csharp
stack.Metadata.Set("quality", "common");
stack.Metadata.Change("quality", "polished");

if (!stack.Metadata.TryRemove("quality", out var error))
{
    // The key was absent or the owning inventory rejected the result.
}
```

### Detached and inventory-owned metadata

Detached `InstanceMetadata` mutates after its own local checks.

Once metadata belongs to an inventory-owned item instance, changes route through that inventory. The proposed result is validated against:

- stack limits and compatibility.
- capacity.
- rules.
- layout constraints.

Rejected changes leave metadata and inventory state unchanged. Accepted direct metadata changes notify through
`Inventory.Changed`. Layouts that order from mutable item state can reconcile in the same operation, so the event may
also contain `Moved` survivors and additional affected contexts.

### Replace and transform

```csharp
stack.Metadata.TryReplace(
    new Dictionary<string, object>
    {
        ["quality"] = "polished",
        ["owner"] = "player"
    },
    out var replaceError);
```

```csharp
stack.Metadata.TryTransform(
    proposed =>
    {
        proposed.Set("inspected", true);
        proposed.Set("quality", "flawless");
    },
    out var transformError);
```

`TryTransform(...)` works on a proposed clone. For inventory-owned metadata, the complete transformed result is validated before commit.

Dictionary containers are copied, but nested stored objects are not deep-cloned.

## Metadata Applies To The Whole Stack

One metadata container belongs to one item instance. If that instance has amount `10`, changing its metadata changes the complete stack of ten.

To change only part of a stack, split it and assign metadata to the result:

```csharp
var stack = inventory.Find(gem).First();

var split = stack.TrySplitAndSetMetadata(
    amount: 2,
    key: "quest-item",
    value: true,
    out var questStack,
    out var error);
```

When successful:

- the original stack keeps the remaining amount.
- the new stack receives a copy of the original metadata.
- the requested key/value is applied to the new stack.
- normal stack, rule, capacity, and layout validation applies.

The instance must belong to an inventory. Splitting detached instances is rejected.

Expected-success form:

```csharp
var questStack = stack.SplitAndSetMetadata(
    amount: 2,
    key: "quest-item",
    value: true);
```

If `amount` equals the complete stack amount, the existing instance receives the metadata instead of creating a second stack.

## Validation And Atomicity

Inventory operations validate the proposed result before committing whenever rejection is possible.

Depending on the operation, validation can include:

- canonical definition membership.
- positive and available amounts.
- stack compatibility and maximum stack size.
- capacity policy.
- semantic and structural rules.
- layout placement.
- metadata-dependent behavior.

For rejected `Try...` operations:

- the method returns `false`.
- `error` describes the rejection.
- contents and placement remain unchanged.
- no committed-change event is fired.

Throwing wrappers preserve the same atomic behavior and then throw `InvalidOperationException`.

Successful compound operations commit as one inventory change rather than exposing partially updated intermediate state.

## Choosing The Right Operation

| Goal | API |
|---|---|
| Add a registered definition | `TryAdd(...)` / `Add(...)` |
| Remove from one known stack | `TryRemove(...)` / `Remove(...)` |
| Remove using storage order | `TryRemoveAtStorageIndex(...)` / `RemoveAtStorageIndex(...)` |
| Remove an amount across stacks | `TryRemoveByDefinition(...)` / `RemoveByDefinition(...)` |
| Move one placed item | `TryMove(...)` / `Move(...)` |
| Exchange two placed items | `TrySwap(...)` / `Swap(...)` |
| Combine compatible stacks | `TryMergeMove(...)` / `MergeMove(...)` |
| Remove all contents | `Clear()` |
| Atomically replace all contents | `ReplaceContents(...)` |
| Remove placement gaps | `TryRepackLayout(...)` / `RepackLayout()` |
| Sort visual placement | `TrySortLayout(...)` / `SortLayout(...)` |
| Find stacks | `Find(...)`, `FindByTag(...)`, `FindWhere(...)` |
| Count amounts | `Count(...)`, `CountByTag(...)`, `Contains(...)` |
| Mutate one stack’s runtime data | `ItemInstance.Metadata` |
| Mutate metadata on part of a stack | `TrySplitAndSetMetadata(...)` / `SplitAndSetMetadata(...)` |
| Assess a portable save | `AssessSnapshot(...)` |
| Restore a save exactly | `TryRestoreSnapshot(...)` / `RestoreSnapshot(...)` |
| Adapt a save without loss | `TryReconcileSnapshot(...)` / `ReconcileSnapshot(...)` |
| Salvage a best-effort subset | `TrySalvageSnapshot(...)` / `SalvageSnapshot(...)` |

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies and rules](POLICIES_AND_RULES.md)
- [Transactions and transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Events and UI integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
