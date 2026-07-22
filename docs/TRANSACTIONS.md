# Transactions

Transactions combine several structural changes inside one inventory or across two inventories. Inventory-owned
transfer helpers remain available for simple source-owned movement. The external transfer-builder API is deprecated and
documented only for backwards compatibility.

The current transaction model follows this ownership rule:

```text
Builder stages intent against a simulation
  -> transaction validates the complete proposal
  -> transaction commits accepted changes atomically
  -> participating inventories emit committed-change events
```

Direct inventory methods remain the shortest route for simple changes. `InventoryTransaction` is the operation object for
larger local changes and cross-inventory transformations.

Read [Inventory Operations](INVENTORY_OPERATIONS.md) for individual mutations and
[Policies And Rules](POLICIES_AND_RULES.md) for the validation concerns applied during commit.

## Choose The Right Tool

| Goal | Tool |
|---|---|
| Perform one ordinary add or removal | `Inventory<TKey>.TryAdd(...)`, `TryRemove(...)`, or another direct operation |
| Combine several adds and removals atomically in one inventory | `InventoryTransaction<TKey>.For(inventory)` |
| Apply reusable semantic deltas locally | `InventoryTransaction<TKey>.For(inventory).Apply(delta)` |
| Apply two inventory-local deltas atomically | `InventoryTransaction<TKey>.From(first).To(second)` |
| Stage one-off two-inventory changes manually | `InventoryTransaction<TKey>.From(first).To(second).FromSide` / `.ToSide` |
| Move one amount between inventories | `TryTransferTo(...)` or `InventoryTransaction<TKey>.From(source).To(target)` |
| Plan several outgoing entries for another inventory | Cross-inventory transaction side builders or reusable per-side deltas |
| Move all matching contents or fail without moving any | `TryMoveWhereTo(...)` and related bulk helpers |
| Move as much as the target can currently accept | Maximum-transfer helpers |
| Exchange contents between inventories | Cross-inventory swap helpers |
| Change placement inside one inventory | `TryMove(...)` or `TrySwap(...)` |

Local layout moves and swaps are not transactions or transfers. They change presentation inside one inventory and emit
dedicated movement payloads. See [Layouts](LAYOUTS.md).

## Semantic Item Deltas

`InventoryItemDelta<TKey>` describes a reusable, context-free semantic net change for one inventory. It does not commit
anything and does not know about capacity, rules, stack limits, layout positions, or a specific inventory. It is useful
for describing recipes, shop offers, quest requirements, donations, and other changes that will later be applied through
transaction APIs.

Delta operations use inventory-local language:

- `Add(...)` means the item is added to the inventory the delta is applied to.
- `Remove(...)` means the item is removed from the inventory the delta is applied to.

For example, this delta describes the player-side result of selling one silver plate for four coins:

```csharp
var sellPlate = InventoryItemDelta<string>.Create()
    .Remove("silver_plate", amount: 1, label: "plate")
    .Add("coin", amount: 4, label: "coins");
```

The mirrored delta describes the opposite side of the same trade:

```csharp
InventoryItemDelta<string> npcSide =
    InventoryItemDelta<string>.Mirror(sellPlate);
```

Remove operations are metadata-aware:

```csharp
var delta = InventoryItemDelta<string>.Create()
    .Remove("apple", 5)                 // Exact empty metadata.
    .Remove("apple", 5, appleMetadata)  // Exact metadata.
    .Remove("apple", 5, ItemMetadataMatch.Any); // Wildcard metadata.
```

When inspecting `delta.Operations`, additions expose concrete payload metadata through `AddMetadata`, while removals
expose their selection rule through `RemoveMetadataMatch`. This mirrors direct inventory operations: creating an item
uses metadata, selecting an existing item uses `ItemMetadataMatch`.

Labels are optional and unique within one delta. They provide stable semantic handles for later planning and UI. Deltas
can also be combined semantically:

```csharp
var combined = InventoryItemDelta<string>.Combine(
    InventoryItemDeltaPart<string>.From(bookPurchase, prefix: "book", count: 2),
    InventoryItemDeltaPart<string>.From(inkPurchase, prefix: "ink"));
```

Combination uses one net semantic mode. Compatible operations merge, opposite operations can cancel to zero, and labels
from combined parts are addressable by original label, prefix, or exact combined label such as
`book.purchase-item`. If an operation nets to zero, its labels disappear with that operation.

## Inventory Transactions

`InventoryTransaction<TKey>` is the public transaction operation concept. It can represent:

- a local transaction built from one inventory.
- a cross-inventory transaction that applies one inventory-local delta per side.
- an inspected low-level structural transaction produced by a manual local builder.

Manual local builders can still materialize structural transaction details:

- amount deltas for existing storage entries.
- removed storage entries.
- newly added item instances and their optional placement contexts.

Manual local transactions are normally started with `InventoryTransaction<TKey>.For(inventory)`:

```csharp
var builder =
    InventoryTransaction<string>.For(backpack);
```

This creates a builder seeded with a simulation of the inventory's current state. `From(source).To(target)` is reserved
for the fluent cross-inventory form, so prefer `For(...)` when only one inventory participates.

### Structural Transaction Reference

| Member | Meaning |
|---|---|
| `Inventory` | The inventory the transaction belongs to |
| `AmountDeltas` | Amount changes indexed against existing storage entries |
| `Removed` | Existing storage entries removed by the transaction |
| `Added` | New item instances with optional layout contexts |
| `IsEmpty` | Whether the transaction contains no structural changes |
| `IsApplied` | Whether the transaction has already been committed |
| `ForInventory(target)` | Copies the structural data with another target inventory |

These members describe inspected local structural transactions. They are not the general cross-inventory transaction
model. `ForInventory(...)` is a low-level structural-copy operation and does not turn a local structural transaction
into a safe cross-inventory operation.

## Stage A Transaction

The builder exposes conditional staging methods:

| API | Staged operation |
|---|---|
| `Add(definition, amount, context)` / `Add(definitionId, amount, context)` | Throwing fluent add wrapper |
| `TryAdd(definition, out failure, amount, context)` | Add an amount, optionally with direct placement |
| `TryAdd(definitionId, out failure, amount, context)` | Resolve a current or migrated ID, then add |
| `TryAdd(definition, amount, context, metadata, out failure)` | Add with instance metadata |
| `TryAdd(definitionId, amount, context, metadata, out failure)` | Resolve a current or migrated ID, then add with metadata |
| `Remove(instance, amount)` | Throwing fluent remove wrapper for a known item instance |
| `TryRemove(instance, out failure, amount)` | Remove from a known item instance |
| `RemoveAtStorageIndex(index, amount)` | Throwing fluent remove wrapper for a storage index |
| `TryRemoveAtStorageIndex(index, out failure, amount)` | Remove by storage index |
| `RemoveAtContext(context, amount)` / `TryRemoveAtContext(context, out failure, amount)` | Remove from the item occupying a layout context |
| `Remove(definitionId, amount, context)` / `TryRemove(definitionId, amount, context, out failure)` | Remove exact-empty-metadata items, optionally constrained to a context |
| `Remove(definitionId, amount, metadata, context)` / `TryRemove(definitionId, amount, metadata, context, out failure)` | Remove exact-metadata items, optionally constrained to a context |
| `Remove(definitionId, amount, ItemMetadataMatch.Any, context)` / `TryRemove(definitionId, amount, ItemMetadataMatch.Any, context, out failure)` | Remove matching items while ignoring metadata, optionally constrained to a context |
| `TryRemoveByDefinition(definition, amount, metadataMatch, out failure)` | Remove across matching stacks using an explicit metadata selector |
| `TryRemoveByDefinition(definitionId, amount, metadataMatch, out failure)` | Resolve a current or migrated ID, then remove across matching stacks |
| `IsEmpty` | Inspect whether staging currently produces any structural change |

Each successful call updates only the builder's simulation. Later calls see earlier staged work:

```csharp
var builder =
    InventoryTransaction<string>.For(backpack);

if (!builder.TryAdd(
        "apple",
        out var addError,
        amount: 5))
{
    // Nothing was added to either the builder or backpack.
}

if (!builder.TryRemoveByDefinition(
        "coin",
        amount: 10,
        metadataMatch: ItemMetadataMatch.Any,
        out var removeError))
{
    // The previously staged apple addition remains in the builder.
    // The live backpack is still unchanged.
}
```

A failed staging call rejects that call; it does not automatically discard earlier successful staging. Application code
decides whether to continue, commit the accepted subset, or abandon the builder.

Manual builders also support expected-success fluent staging:

```csharp
InventoryTransaction<string>
    .For(backpack)
    .Remove("coin", amount: 10, metadataMatch: ItemMetadataMatch.Any, context: SlotLayoutContext<string>.Single(0))
    .Add("apple", amount: 5, context: SlotLayoutContext<string>.Single(2))
    .Commit();
```

Context-constrained removals are strict. If the requested slot, cell, or section does not contain enough matching items,
the staging call fails even when matching items exist elsewhere:

```csharp
if (!builder.TryRemove(
        "coin",
        amount: 10,
        metadataMatch: ItemMetadataMatch.Any,
        context: SlotLayoutContext<string>.Single(0),
        out var failure))
{
    // Coins in other slots do not satisfy this request.
}
```

Use `RemoveAtContext(...)` when UI code is driven by “the player clicked this position” and the definition is not
important. Use definition/key removal with a context when both the item identity and the position matter.

Manual builder removals follow the same metadata language as deltas:

```csharp
builder.Remove("apple", amount: 5);                       // Exact empty metadata.
builder.Remove("apple", amount: 5, metadata: appleMeta);   // Exact metadata.
builder.Remove("apple", amount: 5, metadataMatch: ItemMetadataMatch.Any); // Wildcard metadata.
```

Manual builders are best for one-off, procedural local changes. Deltas are better when the operation should be reusable,
stored, mirrored, combined, or applied with label-based plans.

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

Use `TryBuild(placementContext, out transaction, out failure)` when the transaction-level placement context should be
applied and validated before inspection.

Most application code can commit the builder directly:

```csharp
var committed =
    builder.TryCommit(out var failure);
```

Commit APIs:

| API | Use |
|---|---|
| `builder.Validate(out failure)` | Validate the staged local transaction |
| `builder.TryCommit(out failure)` | Build and conditionally commit |
| `builder.Commit()` | Throwing wrapper when success is expected |
| `transaction.Validate(out failure)` | Validate an inspected structural transaction |
| `transaction.TryCommit(out failure)` | Commit an inspected structural transaction |
| `transaction.Commit()` | Throwing wrapper when success is expected |

In 3.0, transaction commits are transaction-owned. Inventory-owned transaction commit APIs were removed from the public
surface so local and cross-inventory transactions use the same ownership model.

At commit, the transaction validates the complete proposal against the current definitions, stack resolver, capacity
policy, rules, and layout of each participating inventory. A rejected commit leaves live inventories unchanged and emits
no event.

An `InventoryTransaction<TKey>`:

- belongs to the inventory it was built for.
- can be applied only once.
- captures the inventory version it was built against.
- is rejected if the inventory changed before commit; rebuild the transaction against the current state.

The `Try...` commit methods report ownership, repeated-use, placement, or validation failures through `failure`.
Throwing wrappers raise `InventoryOperationException` when commit is rejected. See [Failure Handling](FAILURES.md) for
structured failure categories, stable codes, and project exception behavior.

## Transaction Placement Contexts

Prefer giving each staged add its own direct context when the code adding the item also knows where it should go:

```csharp
var builder =
    InventoryTransaction<string>.For(backpack);

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

This makes the per-add context the ergonomic choice for intent such as "place three apples in this slot." The metadata
overload accepts the context in the same way:

```csharp
builder.TryAdd(
    apple,
    amount: 3,
    context: SlotLayoutContext<string>.Single(2),
    metadata: appleMetadata,
    out var failure);
```

### Legacy Deferred Mapped Placement

A builder may also produce a transaction with one transaction-level mapped context. This API is retained for
compatibility with older deferred-placement code. It is not the preferred way to author new transaction placement.

For new code:

- manual builder transactions should put contexts directly on the staged `Add(...)` or context-constrained
  `Remove(...)` operation.
- delta-created transactions should use `InventoryDeltaApplicationPlan<TKey>` with labels, prefixes, or combined labels.
- mapped contexts should be reserved for compatibility code or rare workflows where one layer intentionally stages
  structural changes first and another layer maps the resulting added entries afterward.

A mapped context assigns positions by `InventoryTransaction<TKey>.Added` index:

```csharp
var builder =
    InventoryTransaction<string>.For(backpack);

builder.TryAdd(apple, out _, amount: 3);
builder.TryAdd(sword, out _);

var preview = builder.Build();

// After inspecting preview.Added and confirming that it has
// the two new instances the placement layer expects:
var placement =
    SlotLayoutContext<string>.Map()
        .Add(0, 1) // First transaction.Added entry.
        .Add(1, 3) // Second transaction.Added entry.
        .Build();

var committed =
    builder.TryBuild(
        placement,
        out var transaction,
        out var failure);

if (committed)
    committed = transaction!.TryCommit(out failure);
```

Transaction-level mapped contexts are part of the older deferred-placement API. They remain available so existing code
can continue to work, but they are no longer the designed path for ordinary staged placement.

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

Inspect `Build().Added` before constructing a dynamic mapping. Prefer direct operation contexts when placement is part
of a manual builder transaction, and prefer delta application plans when placement belongs to a reusable semantic delta.
Use `.Map()` only when deferred placement of the actual resulting additions is specifically required for compatibility
or advanced layering.

## Delta Application Plans

`InventoryDeltaApplicationPlan<TKey>` provides optional label-based guidance when applying an
`InventoryItemDelta<TKey>`. Plans are inventory-unbound; real validation happens when a transaction applies the delta to
an inventory.

Plan rules are evaluated in insertion order. The first matching rule handles the request. If no rule matches, the
transaction uses default behavior:

- removals use deterministic candidate selection.
- additions use normal auto-placement.

Addition rules return `InventoryPlacementDecision<TKey>`:

- `Auto()` uses normal auto-placement.
- `Place(context)` requires the added operation to fit through that strict context.
- `Reject(failure)` rejects the operation.

Removal rules return `InventoryRemovalDecision`:

- `Allow()` lets the candidate stack satisfy the removal.
- `Skip()` ignores that candidate and keeps looking.
- `Reject(failure)` rejects the operation.

For example, this places a purchased item in a specific slot:

```csharp
var plan = InventoryDeltaApplicationPlan<string>.Create()
    .ForAdditionLabel("purchase-item", request =>
        InventoryPlacementDecision<string>.Place(
            SlotLayoutContext<string>.Single(2)));
```

And this only allows an auto-crafting removal to draw from a specific slot:

```csharp
var plan = InventoryDeltaApplicationPlan<string>.Create()
    .ForRemovalLabel("input", candidate =>
        candidate.Contexts.OfType<SlotLayoutContext<string>>()
            .Any(context => context.SlotIndex == 1)
            ? InventoryRemovalDecision.Allow()
            : InventoryRemovalDecision.Skip());
```

Removal selection is a constraint, not a preference. If the selected candidates cannot satisfy the remove amount, the
transaction is rejected even if matching items exist elsewhere. Addition requests expose the operation amount, and
removal candidates expose both the full candidate stack and the planned amount that would be removed from it.

## Cross-Inventory Transactions

Cross-inventory transactions apply one inventory-local delta to each participating inventory and commit both sides
atomically. This makes trade/shop/exchange workflows explicit without introducing a separate exchange concept.

```csharp
var playerDelta = InventoryItemDelta<string>.Create()
    .Remove("silver_plate", amount: 1, label: "plate")
    .Add("coin", amount: 4, label: "coins");

var transaction = InventoryTransaction<string>
    .From(playerInventory)
    .To(npcInventory)
    .ApplyMirrored(playerDelta);

transaction.TryCommit(out var failure);
```

`ApplyMirrored(...)` applies the supplied delta to the first inventory and `InventoryItemDelta.Mirror(delta)` to the
second. This requires exact metadata semantics: removals using `ItemMetadataMatch.Any` cannot be mirrored because the opposite-side
add would not know which metadata to recreate. Use exact-metadata removals, explicit per-side deltas, manual side
staging, or transfer helpers when runtime-selected metadata must be preserved.

More complex workflows can provide both sides explicitly:

```csharp
var transaction = InventoryTransaction<string>
    .From(new InventoryTransactionEntry<string>(
        playerInventory,
        playerDelta,
        playerPlan))
    .To(new InventoryTransactionEntry<string>(
        npcInventory,
        npcDelta));
```

No plan means default deterministic removals and normal auto-placement for that inventory.

For one-off exchanges where a reusable delta would be overkill, a completed cross-inventory transaction exposes explicit
manual side builders:

```csharp
var transaction = InventoryTransaction<string>
    .From(playerInventory)
    .To(npcInventory);

transaction.FromSide
    .Remove("coin", amount: 10, metadataMatch: ItemMetadataMatch.Any, context: SlotLayoutContext<string>.Single(0));

transaction.ToSide
    .Add("coin", amount: 10, context: SlotLayoutContext<string>.Single(2));

transaction.Commit();
```

`FromSide` stages changes against the first inventory's simulation. `ToSide` stages changes against the second
inventory's simulation. Each successful side operation validates immediately and only changes that side's simulation;
the live inventories remain unchanged until the transaction commits. A failed side operation leaves both live
inventories unchanged and does not alter that side's staged simulation.

Manual cross-inventory staging uses the same add, remove, remove-at-context, context-constrained removal, and metadata
semantics as local manual builders. It is best for procedural one-off trades or exchanges. Prefer deltas when the
operation should be reusable, mirrored, stored, combined, or controlled through label-based application plans.

Do not mix the two workflows on the same cross-inventory transaction. Once a cross transaction has applied deltas, manual
side staging is rejected; once manual side staging succeeds, `Apply(...)` and `ApplyMirrored(...)` are rejected.

## Transaction Atomicity And Events

A local transaction commit is one inventory-local operation:

- validation happens before live mutation.
- rejection preserves live contents and placement.
- success applies the complete transaction.
- a non-empty success emits one `Inventory<TKey>.Changed` event.

The event groups additions, removals, modified amounts, and any final layout reflow from the committed transaction.
Layouts that support reconciliation compare surviving instances before the complete transaction with the final state,
so one event reports final movement without exposing temporary shifts between staged operations. Empty transactions
produce no structural change event.

For cross-inventory transactions, both sides are prepared before either inventory mutates. On success, both inventories
are mutated before either side publishes its event, so event handlers observe the final two-inventory state.

## Cross-Inventory Transfer Helpers

Inventory-owned transfer helpers remain first-class for simple source-owned movement. When movement needs custom
multi-step staging, use a cross-inventory transaction:

```csharp
var transaction =
    InventoryTransaction<string>
        .From(backpack)
        .To(chest);

transaction.FromSide
    .Remove(herbStack, amount: 3);

transaction.ToSide
    .Add(herbStack.Definition, amount: 3, context: null, metadata: herbStack.Metadata);

transaction.TryCommit(out var failure);
```

Use exact metadata when preserving source item metadata manually. For simple movement, the inventory-owned transfer
helpers below preserve source metadata automatically and stay first-class. For reusable one-way movement recipes, model
the source side as a removal delta and the target side as an add delta, then apply both through the cross-inventory
transaction.

A transfer helper moves item amounts and metadata between two distinct inventories.

Compatible inventories must share either:

- the same `InventoryManager<TKey>` instance; or
- the same `ItemCatalog<TKey>` instance.

Sharing only equivalent definitions or using separate catalogs with equal IDs is not sufficient. Reference identity of
the manager or catalog preserves canonical definition identity across both inventories.

The source and target may still use different stack resolvers, capacity policies, rules, or layouts. Both inventories
validate their side of the proposed transfer.

## One-Shot Transfers

Source-owned helpers can move a single item amount:

```csharp
var herbStack =
    backpack.Find(herb).Single();

var moved =
    backpack.TryTransferTo(
        craftingInput,
        herbStack,
        amount: 3,
        targetContext: null,
        out var failure);
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

## Deprecated Transfer Builders

External transfer builders stage several source entries together, but are deprecated in 3.0. Prefer cross-inventory
transactions when work needs staging beyond the inventory-owned helpers:

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
        out var failure);
```

`InventoryTransfer.From(source)` creates an outgoing-only builder. Staging removal never adds to a target and never
mutates the source. This is useful when the target is chosen later or when one shared target context should be supplied
at commit time.

| Builder member | Meaning |
|---|---|
| `Source` | Inventory whose items are planned to leave |
| `Target` | Target inventory when the builder was created with `.To(target)`; otherwise `null` |
| `IsTargetBound` | Whether staging validates target additions immediately |
| `Entries` | Snapshot of planned outgoing entries |
| `IsEmpty` | Whether no outgoing entries are staged |
| `To(target)` | Create an empty target-bound builder before staging removals |
| `TryRemove(item, amount, out failure)` | Stage removal from one source instance |
| `TryRemove(item, amount, targetContext, out failure)` | Stage removal and direct target placement; target-bound builders only |
| `TryRemoveAtStorageIndex(index, amount, out failure)` | Stage removal by source storage index |
| `TryRemoveAtStorageIndex(index, amount, targetContext, out failure)` | Stage indexed removal and direct target placement; target-bound builders only |
| `TryRemoveByDefinition(definition, amount, metadataMatch, out failure)` | Stage removal across matching source stacks |
| `TryRemoveByDefinition(definitionId, amount, metadataMatch, out failure)` | Resolve a current or migrated source ID, then stage removal across matching source stacks |

Each `InventoryTransferEntry<TKey>` exposes the canonical definition, amount, cloned metadata snapshot, and original
source instance for inspection.

The builder must be committed through the same source inventory:

| Source API | Behavior |
|---|---|
| `CanCommitTransfer(builder, target, out failure)` | Validate without mutation |
| `CanCommitTransfer(builder, target, targetContext, out failure)` | Validate with target placement |
| `TryCommitTransfer(builder, target, out failure)` | Conditionally commit |
| `TryCommitTransfer(builder, target, targetContext, out failure)` | Commit with target placement |
| `CommitTransfer(...)` | Throwing wrappers |

An empty transfer is rejected. A builder created from another source is also rejected.

### Deprecated Target-Bound Transfer Builders

When the target is already known, bind the builder before staging removals:

```csharp
var transfer =
    InventoryTransfer
        .From(backpack)
        .To(chest);

transfer.TryRemove(
    backpack.Find(herb).Single(),
    amount: 3,
    targetContext:
        SlotLayoutContext<string>.Single(2),
    out var herbError);

var committed =
    transfer.TryCommit(out var failure);
```

`.To(target)` returns the same `InventoryTransferBuilder<TKey>` type in target-bound mode. It must be called before any
removals are staged. Binding validates that the source and target can participate in transfers; same-inventory or
different-catalog targets are setup errors.

This path was the transfer API's ergonomic choice for "move this source amount to this target position." In new code,
prefer a cross-inventory transaction with explicit `FromSide` removal and `ToSide` addition.

Target-bound staging validates against simulated source and target transactions. If target placement, capacity, rules,
or stacking reject a staged operation, that operation is not added to the builder and neither live inventory changes.
`CanCommit(...)` and `TryCommit(...)` revalidate both live inventories before committing, so target changes made after
staging can still reject the transfer without removing source items.

This stricter validation applies even without a direct context:

```csharp
var transfer =
    InventoryTransfer.From(backpack).To(chest);

var accepted =
    transfer.TryRemove(
        backpack.Find(stone).Single(),
        amount: 1,
        out var failure);
```

Here `accepted` is `false` immediately if `chest` has rules, capacity, stacking, or automatic-placement behavior that
rejects the incoming stone. The outgoing-only builder would not discover the same target rejection until commit because
it does not know the target yet.

Direct contexts are exact. If an amount cannot fit through the supplied context, the staged removal is rejected instead
of silently placing overflow elsewhere. Split large source stacks into several explicit removals when target stack
limits or layout shape require several target positions.

When source and target stack limits differ, ask the target inventory for the current limit before planning those
explicit removals:

```csharp
var sourceStack =
    backpack.Find("arrow").Single();

int targetStackSize =
    quiver.GetMaxStackSize(sourceStack);

for (int remaining = sourceStack.Amount; remaining > 0;)
{
    int move =
        Math.Min(remaining, targetStackSize);

    transfer.TryRemove(
        sourceStack,
        move,
        nextTargetSlot(),
        out var failure);

    remaining -= move;
}
```

Use the metadata-aware `GetMaxStackSize(...)` overloads when the target's stack resolver depends on item metadata. This
keeps target-bound transfer placement explicit: helpers can tell you the current stack size, while the builder still
requires you to provide the exact contexts you care about.

Definition-based target-bound removals use automatic target placement:

```csharp
transfer.TryRemoveByDefinition(
    herb,
    amount: 12,
    metadataMatch: ItemMetadataMatch.Any,
    out var failure);
```

A single definition-based removal can produce several incoming entries from several source stacks, so it does not accept
one direct target context. Use item or storage-index removals when each incoming entry needs an exact target position.

Target-bound builders can also be passed to the existing source-owned commit APIs with the same target and a `null`
commit-time context. Supplying a separate non-null commit context is rejected because target placement is already part of
the staged plan.

## How A Deprecated Planned Transfer Commits

The source inventory coordinates the operation:

```text
Build source-removal transaction
  -> derive outgoing transfer entries
  -> simulate target additions with cloned metadata
  -> validate source and target transactions
  -> commit source removal
  -> commit target addition
```

If target capacity, rules, stacking, or placement reject the plan, neither inventory changes. Successful legacy planned
transfers normally emit one `Changed` event from the source and one from the target.

Either event can also contain `Moved` survivors when its layout reflows around the transferred entries. Entry-layout
source removals, for example, report every later entry that shifted.

`CanCommitTransfer(...)` runs the deprecated builder validation path without committing or emitting events. Current
source-owned helper validation uses `CanTransferTo(...)`, documented in one-shot transfers above.

## Placement Contexts In Deprecated Transfer Builders

For deferred placement of several incoming entries, deprecated transfer builders use a mapped context keyed by
`InventoryTransferBuilder<TKey>.Entries` order:

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
        out var failure);
```

The context belongs to the target layout. Its mapped indices describe incoming transfer-entry order, not source storage
indices.

Prefer inventory-owned transfer helpers or cross-inventory transaction side staging when each removal already knows its
corresponding target addition. Mapped transfer contexts remain available for existing builder-based code, but new
placement-sensitive staged code should use transaction side builders or delta application plans.

Best-effort maximum helpers use simpler repeated-transfer placement. Existing code that needs precise multi-entry
placement should use one of the deprecated builder workflows; new staged code should use cross-inventory transactions.

## All-Or-Nothing Bulk Moves

Bulk move helpers create and commit one all-or-nothing transfer-shaped operation:

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
        out var failure);
```

These operations are all-or-nothing. If any selected amount cannot leave the source or enter the target, no selected
contents move.

A selection containing no items produces a failed result with an empty-transfer failure rather than a successful no-op.

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
        out var failure);
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
its own source and target events. Use the all-or-nothing helpers or a cross-inventory transaction when partial progress
is not acceptable.

## Cross-Inventory Swaps

Swap helpers validate incoming and outgoing contents for both inventories:

| API | Exchange |
|---|---|
| `TrySwapItemsWithInventory(other, sourceItem, otherItem, ...)` | Complete stacks |
| `TrySwapItemsWithInventory(other, sourceItem, sourceAmount, otherItem, otherAmount, ...)` | Selected amounts |
| `TrySwapWithInventory(other, sourceTargetContext, otherTargetContext, out failure)` | All contents |

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
        out var failure);
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

Conditional APIs return `false` with a consumer-facing failure for expected rejection. Common causes include:

- a transaction or builder belongs to another inventory.
- an inspected transaction was already applied.
- a transfer source and target are the same inventory.
- inventories do not share the same manager or catalog object.
- an item instance does not belong to the source.
- an amount is non-positive or exceeds the available amount.
- the target rejects canonical definitions, capacity, rules, stacking, or placement.
- a mapped context uses the wrong layout type, key, or position.
- a deprecated transfer builder contains no entries.

For transactions, inventory-owned all-or-nothing transfers, deprecated planned transfer builders, bulk moves, and swaps,
rejection during validation preserves involved inventory state and emits no committed-change event.

Maximum helpers are deliberately incremental. A later failure does not undo amounts already moved by earlier successful
per-stack transfers.

## Common Mistakes

- Treating a builder as though it already changed the inventory.
- Ignoring a failed staging call and assuming the entire builder was discarded.
- Committing a transaction through an inventory it does not belong to.
- Reusing an already applied transaction.
- Using `ForInventory(...)` instead of cross-inventory transactions.
- Mapping transaction storage indices instead of `Added` indices.
- In deprecated transfer-builder code, mapping transfer source indices instead of transfer-entry order.
- In deprecated transfer-builder code, calling transfer commit on the target instead of the builder's source.
- Assuming separate but equivalent catalogs are cross-inventory compatible.
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
