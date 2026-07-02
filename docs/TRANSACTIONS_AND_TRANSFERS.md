# Transactions And Transfers

Transactions combine several structural changes inside one inventory. Transfers coordinate structural changes between
two inventories.

Both features follow the same ownership rule:

```text
Builder stages intent against a simulation
  -> inventory validates the complete proposal
  -> inventory commits accepted changes
  -> inventory emits committed-change events
```

Builders do not commit live state. Inventories own commit because they coordinate canonical definitions, stack
resolution, capacity, rules, and layout placement.

Read [Inventory Operations](INVENTORY_OPERATIONS.md) for individual mutations and
[Policies And Rules](POLICIES_AND_RULES.md) for the validation concerns applied during commit.

## Choose The Right Tool

| Goal | Tool |
|---|---|
| Perform one ordinary add or removal | `Inventory<TKey>.TryAdd(...)`, `TryRemove(...)`, or another direct operation |
| Combine several adds and removals atomically in one inventory | `InventoryTransactionBuilder<TKey>` |
| Move one amount between inventories | `TryTransferTo(...)` |
| Plan several outgoing entries and commit them together | `InventoryTransferBuilder<TKey>` |
| Move all matching contents or fail without moving any | `TryMoveWhereTo(...)` and related bulk helpers |
| Move as much as the target can currently accept | Maximum-transfer helpers |
| Exchange contents between inventories | Cross-inventory swap helpers |
| Change placement inside one inventory | `TryMove(...)` or `TrySwap(...)` |

Local layout moves and swaps are not transactions or transfers. They change presentation inside one inventory and emit
dedicated movement payloads. See [Layouts](LAYOUTS.md).

## Inventory Transactions

An `InventoryTransaction<TKey>` represents structural changes targeting exactly one inventory:

- amount deltas for existing storage entries.
- removed storage entries.
- newly added item instances and their optional placement contexts.

Transactions are normally produced by `InventoryTransaction<TKey>.From(inventory)`:

```csharp
var builder =
    InventoryTransaction<string>.From(backpack);
```

This creates a builder seeded with a simulation of the inventory's current state.

### Transaction Object Reference

| Member | Meaning |
|---|---|
| `Inventory` | The inventory the transaction belongs to |
| `AmountDeltas` | Amount changes indexed against existing storage entries |
| `Removed` | Existing storage entries removed by the transaction |
| `Added` | New item instances with optional layout contexts |
| `IsEmpty` | Whether the transaction contains no structural changes |
| `IsApplied` | Whether the transaction has already been committed |
| `ForInventory(target)` | Copies the structural data with another target inventory |

`ForInventory(...)` is a low-level structural-copy operation. It does not turn an inventory transaction into a safe
cross-inventory transfer. Use the transfer APIs when ownership moves between inventories.

## Stage A Transaction

The builder exposes conditional staging methods:

| API | Staged operation |
|---|---|
| `TryAdd(definition, out error, amount, context)` | Add an amount, optionally with direct placement |
| `TryAdd(definition, amount, context, metadata, out error)` | Add with instance metadata |
| `TryRemove(instance, out error, amount)` | Remove from a known item instance |
| `TryRemoveAtStorageIndex(index, out error, amount)` | Remove by storage index |
| `TryRemoveByDefinition(definition, amount, ignoreMetadata, out error)` | Remove across matching stacks |
| `IsEmpty` | Inspect whether staging currently produces any structural change |

Each successful call updates only the builder's simulation. Later calls see earlier staged work:

```csharp
var builder =
    InventoryTransaction<string>.From(backpack);

if (!builder.TryAdd(
        apple,
        out var addError,
        amount: 5))
{
    // Nothing was added to either the builder or backpack.
}

if (!builder.TryRemoveByDefinition(
        coin,
        amount: 10,
        ignoreMetadata: true,
        out var removeError))
{
    // The previously staged apple addition remains in the builder.
    // The live backpack is still unchanged.
}
```

A failed staging call rejects that call; it does not automatically discard earlier successful staging. Application code
decides whether to continue, commit the accepted subset, or abandon the builder.

## Build Versus Commit

`Build()` materializes the staged structural transaction for inspection:

```csharp
InventoryTransaction<string> transaction =
    builder.Build();

foreach (var addition in transaction.Added)
{
    Console.WriteLine(
        addition.instance.Definition.Id);
}
```

Use `TryBuild(placementContext, out transaction, out error)` when the transaction-level placement context should be
applied and validated before inspection.

Most application code can commit the builder directly:

```csharp
var committed =
    backpack.TryCommitTransaction(
        builder,
        out var error);
```

Commit APIs:

| API | Use |
|---|---|
| `TryCommitTransaction(builder, out error)` | Build and conditionally commit |
| `TryCommitTransaction(builder, placementContext, out error)` | Build and commit with transaction-level placement |
| `TryCommitTransaction(transaction, out error)` | Commit an inspected transaction |
| `TryCommitTransaction(transaction, placementContext, out error)` | Commit an inspected transaction with placement |
| `CommitTransaction(...)` | Throwing wrappers when success is expected |

At commit, the inventory validates the complete transaction against its current definitions, stack resolver, capacity
policy, rules, and layout. A rejected commit leaves the live inventory unchanged and emits no event.

An `InventoryTransaction<TKey>`:

- belongs to the inventory it was built for.
- can be applied only once.
- should be committed while the inventory still represents the state against which it was built.

The `Try...` commit methods report ownership, repeated-use, placement, or validation failures through `error`.
Throwing wrappers raise `InvalidOperationException` when commit is rejected.

## Transaction Placement Contexts

Prefer giving each staged add its own direct context when the code adding the item also knows where it should go:

```csharp
var builder =
    InventoryTransaction<string>.From(backpack);

var staged = builder.TryAdd(
    apple,
    out var addError,
    amount: 3,
    context: SlotLayoutContext<string>.Single(2));
```

The context participates in staging. The layout uses slot `2` when selecting merge candidates and deciding whether the
add needs a new item instance:

- if slot `2` contains a compatible apple stack with enough room, that stack receives an amount delta.
- if slot `2` is empty, the builder stages a new apple instance there.
- a compatible apple stack in another slot is not selected merely because it has room.

This makes the per-add context the ergonomic choice for intent such as “place three apples in this slot.” The metadata
overload accepts the context in the same way:

```csharp
builder.TryAdd(
    apple,
    amount: 3,
    context: SlotLayoutContext<string>.Single(2),
    metadata: appleMetadata,
    out var error);
```

### Deferred Mapped Placement

A commit may instead receive one transaction-level mapped context. This is an advanced option for workflows where one
layer stages structural changes and another layer chooses positions afterward.

A mapped context assigns positions by `InventoryTransaction<TKey>.Added` index:

```csharp
var builder =
    InventoryTransaction<string>.From(backpack);

builder.TryAdd(apple, out _, amount: 3);
builder.TryAdd(sword, out _);

var transaction = builder.Build();

// After inspecting transaction.Added and confirming that it has
// the two new instances the placement layer expects:
var placement =
    SlotLayoutContext<string>.Map()
        .Add(0, 1) // First transaction.Added entry.
        .Add(1, 3) // Second transaction.Added entry.
        .Build();

var committed =
    backpack.TryCommitTransaction(
        transaction,
        placement,
        out var error);
```

Mapped keys are added-entry indices, not:

- inventory storage indices.
- item-definition IDs.
- layout positions.
- the order of every builder method call.

Mapped placement happens after the builder has made its stacking decisions. It can assign positions to new instances,
but it cannot change merge-candidate selection or turn an amount delta into a new instance.

For example, if the staged apple addition merges completely into an existing stack, it appears in `AmountDeltas` and
does not occupy an `Added` index. Mapping the expected apple as entry `0` may then target a different addition, or be
rejected with `Mapped added entry index out of range.` when no such added entry exists.

Inspect `Build().Added` before constructing a dynamic mapping. Prefer per-add direct contexts when placement is part of
the original add intent; use `.Map()` when deferred placement of the actual resulting additions is specifically useful.

## Transaction Atomicity And Events

A transaction commit is one inventory-local operation:

- validation happens before live mutation.
- rejection preserves live contents and placement.
- success applies the complete transaction.
- a non-empty success emits one `Inventory<TKey>.Changed` event.

The event groups additions, removals, modified amounts, and any final layout reflow from the committed transaction.
Layouts that support reconciliation compare surviving instances before the complete transaction with the final state,
so one event reports final movement without exposing temporary shifts between staged operations. Empty transactions
produce no structural change event.

## Cross-Inventory Transfers

A transfer moves item amounts and metadata between two distinct inventories.

Compatible inventories must share either:

- the same `InventoryManager<TKey>` instance; or
- the same `ItemCatalog<TKey>` instance.

Sharing only equivalent definitions or using separate catalogs with equal IDs is not sufficient. Reference identity of
the manager or catalog preserves canonical definition identity across both inventories.

The source and target may still use different stack resolvers, capacity policies, rules, or layouts. Both inventories
validate their side of the proposed transfer.

## One-Shot Transfers

Use source-owned helpers for a single item amount:

```csharp
var herbStack =
    backpack.Find(herb).Single();

var moved =
    backpack.TryTransferTo(
        craftingInput,
        herbStack,
        amount: 3,
        targetContext: null,
        out var error);
```

| API | Behavior |
|---|---|
| `CanTransferTo(...)` | Validates without mutating either inventory |
| `TryTransferTo(...)` | Conditionally transfers one amount |
| `TransferTo(...)` | Throwing wrapper |

The call belongs to the source inventory because the supplied `ItemInstance<TKey>` must be owned by that source.
`targetContext` belongs to the target layout.

Transfers preserve structurally equal metadata in a separate metadata object on the target. Full-stack transfer removes
the source instance; the target still owns its own resulting item instance.

## Transfer Builders

Use a transfer builder when several source entries should move together:

```csharp
var transfer =
    InventoryTransfer.From(backpack);

transfer.TryRemove(
    backpack.Find(herb).Single(),
    amount: 3,
    out var herbError);

transfer.TryRemove(
    backpack.Find(bottle).Single(),
    amount: 1,
    out var bottleError);

var committed =
    backpack.TryCommitTransfer(
        transfer,
        craftingInput,
        targetContext: null,
        out var error);
```

`InventoryTransfer.From(source)` creates an outgoing-only builder. Staging removal never adds to a target and never
mutates the source.

| Builder member | Meaning |
|---|---|
| `Source` | Inventory whose items are planned to leave |
| `Entries` | Snapshot of planned outgoing entries |
| `IsEmpty` | Whether no outgoing entries are staged |
| `TryRemove(item, amount, out error)` | Stage removal from one source instance |
| `TryRemoveAtStorageIndex(index, amount, out error)` | Stage removal by source storage index |
| `TryRemoveByDefinition(definition, amount, ignoreMetadata, out error)` | Stage removal across matching source stacks |

Each `InventoryTransferEntry<TKey>` exposes the canonical definition, amount, cloned metadata snapshot, and original
source instance for inspection.

The builder must be committed through the same source inventory:

| Source API | Behavior |
|---|---|
| `CanCommitTransfer(builder, target, out error)` | Validate without mutation |
| `CanCommitTransfer(builder, target, targetContext, out error)` | Validate with target placement |
| `TryCommitTransfer(builder, target, out error)` | Conditionally commit |
| `TryCommitTransfer(builder, target, targetContext, out error)` | Commit with target placement |
| `CommitTransfer(...)` | Throwing wrappers |

An empty transfer is rejected. A builder created from another source is also rejected.

## How A Planned Transfer Commits

The source inventory coordinates the operation:

```text
Build source-removal transaction
  -> derive outgoing transfer entries
  -> simulate target additions with cloned metadata
  -> validate source and target transactions
  -> commit source removal
  -> commit target addition
```

If target capacity, rules, stacking, or placement reject the plan, neither inventory changes. Successful planned
transfers normally emit one `Changed` event from the source and one from the target.

Either event can also contain `Moved` survivors when its layout reflows around the transferred entries. Entry-layout
source removals, for example, report every later entry that shifted.

`CanTransferTo(...)` and `CanCommitTransfer(...)` run the planning and validation path without committing or emitting
events.

## Transfer Placement Contexts

For one incoming entry, a direct `targetContext` selects its target position:

```csharp
source.TryTransferTo(
    target,
    sourceItem,
    amount: 1,
    targetContext:
        SlotLayoutContext<string>.Single(4),
    out var error);
```

For several incoming entries, use a mapped context keyed by `InventoryTransferBuilder<TKey>.Entries` order:

```csharp
var transfer =
    InventoryTransfer.From(backpack);

transfer.TryRemove(backpack.Items[0], 5, out _);
transfer.TryRemove(backpack.Items[1], 1, out _);

var placement =
    SlotLayoutContext<string>.Map()
        .Add(0, 2)
        .Add(1, 3)
        .Build();

var moved =
    backpack.TryCommitTransfer(
        transfer,
        chest,
        placement,
        out var error);
```

The context belongs to the target layout. Its mapped indices describe incoming transfer-entry order, not source storage
indices.

Best-effort maximum helpers use simpler repeated-transfer placement. Use a transfer builder with a mapped context when
precise multi-entry placement matters.

## All-Or-Nothing Bulk Moves

Bulk move helpers create and commit one planned transfer:

| API | Selected source contents |
|---|---|
| `TryMoveAllTo(...)` | Every item |
| `TryMoveWhereTo(...)` | Every item matching a predicate |
| `TryMoveByTagTo(...)` | Every item satisfying one catalog-resolved tag |
| `TryMoveAllTagsTo(...)` | Every item satisfying all supplied catalog-resolved tags |

```csharp
var moved =
    chest.TryMoveByTagTo(
        backpack,
        "loot:treasure",
        targetContext: null,
        out var error);
```

These operations are all-or-nothing. If any selected amount cannot leave the source or enter the target, no selected
contents move.

A selection containing no items produces a failed result with an empty-transfer error rather than a successful no-op.

## Maximum And Best-Effort Transfers

Maximum helpers intentionally answer a different question: how much can move right now?

| API | Behavior |
|---|---|
| `TryTransferMaximumTo(...)` | Moves the largest valid amount from one stack, up to the requested amount |
| `TryMoveMaximumWhereTo(...)` | Visits matching stacks in source storage order and moves as much as possible |
| `TryMoveMaximumByTagTo(...)` | Best-effort move for a catalog-resolved tag |

```csharp
var moved =
    chest.TryMoveMaximumByTagTo(
        backpack,
        "loot:treasure",
        targetContext: null,
        out var transferredAmount,
        out var error);
```

`TryTransferMaximumTo(...)` searches for the largest accepted amount and commits that amount once. It returns `false`
with `transferredAmount == 0` when nothing can move.

The multi-stack maximum helpers:

- process matching stacks in source storage order.
- may fully move early stacks and partially move a later stack.
- continue past stacks that cannot move.
- return the total transferred amount.
- return `true` when at least one amount moved.

They are best-effort, not one atomic bulk transfer. Each successful per-stack transfer commits immediately and may emit
its own source and target events. Use the all-or-nothing helpers or a transfer builder when partial progress is not
acceptable.

## Cross-Inventory Swaps

Swap helpers validate incoming and outgoing contents for both inventories:

| API | Exchange |
|---|---|
| `TrySwapItemsWithInventory(other, sourceItem, otherItem, ...)` | Complete stacks |
| `TrySwapItemsWithInventory(other, sourceItem, sourceAmount, otherItem, otherAmount, ...)` | Selected amounts |
| `TrySwapWithInventory(other, sourceTargetContext, otherTargetContext, out error)` | All contents |

```csharp
var swapped =
    backpack.TrySwapItemsWithInventory(
        other: chest,
        sourceItem: backpack.Items[0],
        sourceAmount: 2,
        otherItem: chest.Items[0],
        otherAmount: 1,
        sourceTargetContext: null,
        otherTargetContext: null,
        out var error);
```

Context direction is named from the receiving side:

| Context | Incoming contents it places |
|---|---|
| `sourceTargetContext` | Contents from `other` entering the source inventory |
| `otherTargetContext` | Contents from the source entering `other` |

For a whole-inventory swap, mapped indices follow the incoming entries' order in the inventory they leave.

If either resulting inventory violates capacity, rules, stacking, or layout placement, the swap is rejected before
contents change. Swapping two empty inventories succeeds as a no-op and emits no events.

## Failure Semantics

Conditional APIs return `false` with a consumer-facing error for expected rejection. Common causes include:

- a transaction or builder belongs to another inventory.
- an inspected transaction was already applied.
- a transfer source and target are the same inventory.
- inventories do not share the same manager or catalog object.
- an item instance does not belong to the source.
- an amount is non-positive or exceeds the available amount.
- the target rejects canonical definitions, capacity, rules, stacking, or placement.
- a mapped context uses the wrong layout type, key, or position.
- a transfer contains no entries.

For transactions, planned transfers, bulk moves, and swaps, rejection during validation preserves involved inventory
state and emits no committed-change event.

Maximum helpers are deliberately incremental. A later failure does not undo amounts already moved by earlier successful
per-stack transfers.

## Common Mistakes

- Treating a builder as though it already changed the inventory.
- Ignoring a failed staging call and assuming the entire builder was discarded.
- Committing a transaction through an inventory it does not belong to.
- Reusing an already applied transaction.
- Using `ForInventory(...)` instead of the transfer system.
- Mapping transaction storage indices instead of `Added` indices.
- Mapping transfer source indices instead of transfer-entry order.
- Calling transfer commit on the target instead of the builder's source.
- Assuming separate but equivalent catalogs are transfer-compatible.
- Using a best-effort maximum helper when all-or-nothing behavior is required.
- Expecting a multi-stack maximum move to emit only one event.
- Confusing local `TryMove(...)` with cross-inventory `TryMoveAllTo(...)`.
- Reversing `sourceTargetContext` and `otherTargetContext` during a swap.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies And Rules](POLICIES_AND_RULES.md)
- [Metadata](INVENTORY_OPERATIONS.md#instance-metadata)
- [Events and UI integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
