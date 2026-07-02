# Events And UI Integration

`Inventory<TKey>.Changed` reports committed inventory changes. UI code can use it at two levels:

- `RequiresFullRefresh` and `AffectedLayoutContexts` provide a simple refresh strategy.
- Typed payload collections describe what changed for animations, badges, gameplay reactions, and audit logs.

The inventory has already committed the change when the event is raised. Rejected operations do not emit a committed
change event.

Read [Inventory Operations](INVENTORY_OPERATIONS.md) for ordinary mutations,
[Layouts](LAYOUTS.md) for layout contexts, and
[Policies And Rules](POLICIES_AND_RULES.md) for runtime configuration changes.

## Subscribe To Changes

```csharp
inventory.Changed += OnInventoryChanged;

void OnInventoryChanged(
    object? sender,
    InventoryChangedEventArgs<string> args)
{
    if (args.RequiresFullRefresh)
    {
        RebuildInventoryView();
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
        RefreshPosition(context);
}
```

The package raises a normal synchronous .NET event on the thread performing the mutation. It does not select a UI
dispatcher. Desktop, mobile, and game UI integrations should marshal to their required UI thread when necessary.

Because state is already committed before handlers run, event handlers should not throw. An escaping handler exception
can make the mutation call throw even though the inventory change has already happened.

Unsubscribe handlers when a long-lived inventory should no longer retain a view or controller:

```csharp
inventory.Changed -= OnInventoryChanged;
```

## Event Overview

One `InventoryChangedEventArgs<TKey>` can contain several categories from the same committed operation:

| Member | Meaning |
|---|---|
| `Origin` | High-level workflow such as a normal operation, exact restore, reconciliation, or salvage. |
| `Added` | New item instances were added. |
| `Removed` | Existing item instances were removed. |
| `Modified` | Existing item amounts changed. |
| `Moved` | A surviving item changed placement directly, through sorting/repacking, or as collateral layout reflow. |
| `Swapped` | Two layout placements were exchanged. |
| `MetadataChanged` | Metadata changed directly on an existing item instance. |
| `ConfigurationChanged` | A runtime stack resolver, capacity policy, or layout parameter changed. |
| `Cleared` | Existing contents were cleared as part of the operation. |
| `AffectedLayoutContexts` | Positions gathered from relevant payloads and layout reconciliation. |
| `RequiresFullRefresh` | The event does not completely describe the observable change at context level. |

Do not assume an event belongs to exactly one category. A transaction can add, remove, and modify stacks together.
A stack-shape parameter change can include configuration, additions, removals, and amount modifications in one event.

## The Recommended Refresh Decision

Process each event in this order:

```text
RequiresFullRefresh?
  yes -> rebuild the complete inventory view
  no  -> refresh AffectedLayoutContexts

Then, when useful:
  inspect semantic payloads for animation, metadata UI,
  configuration UI, gameplay, or logging
```

Example:

```csharp
void OnInventoryChanged(
    object? sender,
    InventoryChangedEventArgs<string> args)
{
    if (args.RequiresFullRefresh)
    {
        RebuildInventoryView();
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
        RefreshPosition(context);

    foreach (var change in args.ConfigurationChanged)
        RefreshConfigurationDisplay(change);

    foreach (var metadata in args.MetadataChanged)
        RefreshMetadataBadge(
            metadata.LayoutContexts,
            metadata.AfterMetadata);
}
```

`AffectedLayoutContexts` is for visual positions. A preserve-only configuration change can have no affected contexts, so
configuration panels should also inspect `ConfigurationChanged`.

## Added Items

Each `ItemAdded<TKey>` contains:

| Member | Meaning |
|---|---|
| `Instance` | The new inventory-owned item instance. |
| `Index` | Its assigned `Inventory.Items` storage index. |
| `LayoutContexts` | Every occupied layout position after addition. |
| `LayoutContext` | The position when exactly one context exists; otherwise `null`. |

```csharp
foreach (var added in args.Added)
{
    AddOrRefreshItem(
        added.Instance,
        added.LayoutContexts);
}
```

An add can instead appear in `Modified` when its amount merges entirely into an existing compatible stack. Consumers
that display amounts should handle both groups.

Multi-cell layouts return every occupied cell in `LayoutContexts`; do not assume one item has one position.

## Removed Items

Each `ItemRemoved<TKey>` contains:

| Member | Meaning |
|---|---|
| `Instance` | The removed item instance. |
| `Index` | Its storage index before removal. |
| `LayoutContexts` | Positions occupied before removal. |
| `LayoutContext` | The previous position when exactly one existed. |

The instance is no longer inventory-owned when the handler runs, but the payload remains useful for identity, visual
cleanup, logging, and animation.

Storage indices can shift after removals. Use the payload's contexts or stable instance identity for UI updates rather
than treating the old index as a current position.

## Modified Amounts

`ItemModified<TKey>` describes an existing instance whose amount changed:

| Member | Meaning |
|---|---|
| `Instance` | The item instance after commit. |
| `Index` | Its storage index before the transaction was applied. |
| `BeforeAmount` | Amount before commit. |
| `AfterAmount` | Amount after commit. |
| `BeforeLayoutContexts` | Positions before the complete transaction. |
| `AfterLayoutContexts` | Positions after the complete transaction. |
| `BeforeLayoutContext` / `AfterLayoutContext` | Single-position conveniences. |

Before and after contexts may differ even when the operation primarily changed an amount. For example, another removal
in the same entry-layout transaction can shift the modified instance's visible position.

```csharp
foreach (var modified in args.Modified)
{
    UpdateAmount(
        modified.Instance,
        modified.AfterAmount);
}
```

## Movement

`ItemMoved<TKey>` reports an instance that stayed in the inventory but changed layout placement:

| Member | Meaning |
|---|---|
| `Instance` | The moved item instance. |
| `FromLayoutContexts` | Positions before movement. |
| `ToLayoutContexts` | Positions after movement. |
| `FromPosition` / `ToPosition` | Single-position conveniences. |
| `Cause` | Why this particular instance moved. |
| `IsAutomatic` | Whether movement was automatic rather than directly targeted. |
| `IsSortResult` | Obsolete compatibility convenience for `Cause == ItemMovementCause.Sort`. |

`ItemMovementCause` distinguishes:

| Cause | Meaning |
|---|---|
| `ExplicitMove` | The instance was the direct target of `Move(...)` or `TryMove(...)`. |
| `Sort` | Sorting repositioned the instance. |
| `Repack` | Repacking repositioned the instance. |
| `LayoutReflow` | The layout repositioned a surviving instance as a consequence of another mutation. |

Use the cause when different automatic operations need different presentation. Use `IsAutomatic` when the UI only
needs to distinguish deliberate movement from automatic repositioning:

```csharp
foreach (var moved in args.Moved)
{
    if (moved.IsAutomatic)
    {
        RefreshMovedPositions(moved);
        continue;
    }

    AnimateDeliberateMove(
        moved.Instance,
        moved.FromLayoutContexts,
        moved.ToLayoutContexts);
}
```

Structural mutations can also move otherwise unchanged items. For example, inserting at Entry index `1` shifts the
previous entry at `1` to `2`; removing Entry index `0` shifts every later entry down. Those surviving instances appear
in `Moved` in the same event as the triggering `Added` or `Removed` payload.

A single event can contain different causes. Moving one Entry directly marks that instance as `ExplicitMove`, while
neighbors displaced by the same operation are marked as `LayoutReflow`. Swaps remain represented by `Swapped`, and
cross-inventory ownership changes remain represented by `Added` and `Removed`.

Sorting does not request a full refresh by itself. It emits moved payloads only for instances whose contexts actually
changed. Sorting an already sorted layout emits no event.

## Swaps

`ItemSwapped<TKey>` describes two exchanged placements:

| Member | Meaning |
|---|---|
| `FirstLayoutContexts` | First group of positions involved. |
| `SecondLayoutContexts` | Second group of positions involved. |
| `AffectedLayoutContexts` | Distinct union of both groups. |
| `FromPosition` / `ToPosition` | Single-position conveniences. |
| `AfterSwapFromPositionInstance` | Instance at the first position after the swap. |
| `AfterSwapToPositionInstance` | Instance at the second position after the swap. |

The instance properties describe the result, not the pre-swap occupants. Refresh the affected contexts from current
inventory state when the UI does not need a special swap animation.

Cross-inventory swaps are structural transactions on each inventory, so they report additions, removals, or amount
modifications rather than local-layout `Swapped` payloads.

## Metadata Changes

Direct metadata mutation on an inventory-owned instance produces `ItemMetadataChanged<TKey>`:

| Member | Meaning |
|---|---|
| `Instance` | Item instance after mutation. |
| `Index` | Current storage index. |
| `BeforeMetadata` | Dictionary snapshot before mutation. |
| `AfterMetadata` | Dictionary snapshot after mutation. |
| `LayoutContexts` | Current occupied positions. |
| `LayoutContext` | Single-position convenience. |

```csharp
foreach (var metadata in args.MetadataChanged)
{
    foreach (var context in metadata.LayoutContexts)
    {
        RefreshMetadataBadge(
            context,
            metadata.AfterMetadata);
    }
}
```

The dictionaries are copied snapshots, but their stored object values are not recursively deep-cloned.

A layout that orders items from metadata can reconcile after this mutation. The same event then contains
`MetadataChanged` for the edited item and `Moved` for every surviving item whose context changed.

### Partial-stack metadata

`TrySplitAndSetMetadata(...)` changes metadata by creating a new stack. Its event therefore contains:

- one `Modified` payload for the reduced original stack.
- one `Added` payload for the new metadata-bearing stack.
- no `MetadataChanged` payload.

UI code displaying metadata should inspect metadata on added instances as well as direct metadata-change payloads:

```csharp
foreach (var added in args.Added)
{
    RefreshItemWithMetadata(
        added.LayoutContexts,
        added.Instance.Metadata.AsReadOnly());
}
```

## Runtime Configuration Changes

Successful inventory-owned parameter mutation produces `InventoryConfigurationChanged<TKey>`.

| Member | Meaning |
|---|---|
| `Kind` | `StackResolver`, `CapacityPolicy`, or `Layout`. |
| `ParameterId` | Stable parameter ID that changed. |
| `Value` | Committed parameter value. |
| `PreviousComponent` | Component before replacement. |
| `CurrentComponent` | Replacement component. |
| `RequiresFullRefresh` | Whether this change cannot be represented safely as targeted updates. |

```csharp
foreach (var change in args.ConfigurationChanged)
{
    switch (change.Kind)
    {
        case InventoryConfigurationChangeKind.StackResolver:
            RefreshStackLimitDisplay();
            break;

        case InventoryConfigurationChangeKind.CapacityPolicy:
            RefreshCapacityDisplay();
            break;

        case InventoryConfigurationChangeKind.Layout:
            RebuildInventoryView();
            break;
    }
}
```

`InventoryChangedEventArgs<TKey>.RequiresFullRefresh` is true when the event was explicitly marked as incomplete,
when `Cleared` is true, or when any configuration entry requires it.

Inventory-owned rule changes are different: `TrySetRule(...)`, `TryRemoveRule(...)`, enable changes, and priority changes
do not currently emit `Inventory<TKey>.Changed`. A rule editor should update its own configuration view from the
mutation result.

## Full-Refresh Cases

When `RequiresFullRefresh` is false, the event's semantic payloads and `AffectedLayoutContexts` are a complete delta
for synchronizing item presentation at the layout's existing addressable contexts. Consumers can update those contexts
without rediscovering the whole view.

When it is true, topology, layout-owned presentation state, or another intentionally unrepresented change may also
have changed. Rebuild the complete view instead of assuming that the listed contexts are exhaustive. This is an escape
hatch, not an indication that an operation was merely large or structurally complex.

Current built-in operations request a full refresh in these cases:

| Operation | Why |
|---|---|
| `Clear()` on a non-empty inventory | Every previous position is invalidated. |
| `ReplaceContents(...)` when replacing existing contents | `Cleared` is true and the view is replaced as a whole. |
| Successful layout parameter mutation | Addressable positions or placement behavior may have changed. |

Additional details:

- A direct repack that changes no placement emits no event.
- A direct repack that changes placement reports complete `Moved` and affected-context payloads and does not request a
  full refresh, matching sorting.
- A custom repack capability only creates a candidate layout. Capability rejection or candidate validation failure
  preserves the active layout and emits no event; the inventory emits the normal single event only after commit.
- A custom layout can still request a full refresh from reconciliation when movement and affected contexts cannot
  completely represent its layout-owned presentation change.
- Preserve-only stack-resolver and capacity-policy changes do not require full refresh.
- Stack splitting or compression without repack uses normal added, removed, modified, and affected-context payloads.
- Stack-resolver rebuilding with repack reports complete removed, added, and affected-context payloads for built-in
  layouts. A custom reconciler can still mark its layout-owned delta incomplete.
- Sorting uses moved payloads and does not request full refresh.
- Direct metadata mutation is incremental.

`Cleared` continues to imply a full refresh for compatibility, even though removed payloads retain their former
contexts. `ReplaceContents(...)` uses the same established clear-and-replace event contract.

Do not recreate this matrix in UI code. Treat the event's `RequiresFullRefresh` value as authoritative so future
operations and custom layouts can request a rebuild without consumer changes.

## Affected Layout Contexts

`AffectedLayoutContexts` combines:

- added and removed contexts.
- modified before and after contexts.
- moved source and destination contexts.
- swap contexts.
- metadata-change contexts.
- any explicit contexts supplied by the operation.

It also includes supplemental contexts returned by an `IInventoryLayoutReconciler<TKey>`.

The collection may contain several context types only when a custom integration creates such payloads. Normal inventory
events use contexts from the inventory's active layout.

Multi-cell items contribute every occupied cell. This lets a grid UI refresh all cells covered or uncovered by one
change:

```csharp
var cells =
    args.AffectedLayoutContexts
        .OfType<MultiCellGridLayoutContext<string>>();

foreach (var cell in cells)
    RefreshGridCell(cell.X, cell.Y);
```

The collection avoids duplicate object references. Equivalent positions represented by separate context objects can
still appear more than once when the context type uses reference equality. Treat the collection as positions to refresh;
refreshing the same position twice is safe, and consumers that require uniqueness can de-duplicate by their context's
value members.

## Transactions And Transfers

A successful non-empty transaction emits one grouped event from its inventory, even when several staging operations
contributed to it. Reflow compares the state before the complete transaction with the final state, so temporary
intermediate shifts are not reported. Surviving instances report only their final before/after movement.

A successful planned transfer normally emits:

- one event from the source for removed or reduced stacks.
- one event from the target for added or increased stacks.

Best-effort multi-stack maximum transfers commit per source stack. They can therefore emit several source and target
event pairs.

See [Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md) for their atomicity and partial-progress differences.

## No-Event Cases

No committed change event is emitted when:

- validation rejects an operation.
- `Can...` APIs only simulate or validate.
- a builder stages work without commit.
- `Clear()` is called on an empty inventory.
- empty contents replace an already empty inventory.
- sorting or direct repack succeeds without changing placement.
- a rule-container mutation succeeds.
- detached metadata changes outside an inventory.

An operation returning `true` can therefore still be a no-op for event purposes, as with sorting an already sorted
layout.

## Persistence And View Rebuilds

Portable snapshot application is one atomic replacement event. The handler observes final contents and layout state;
old instances appear in `Removed` and replacements in `Added`. `Origin` distinguishes exact restoration, lossless
reconciliation, and salvage. Failed application emits no event.

The obsolete `Deserialize(...)` API retains its legacy multi-step behavior and should not be used for new UI
integration.

The persistence guide covers restore validation and compatibility in detail.

## Practical UI Patterns

### Simple position-based view

Use only full refresh and affected contexts:

```csharp
inventory.Changed += (_, args) =>
{
    if (args.RequiresFullRefresh)
    {
        RebuildInventoryView();
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
        RefreshPosition(context);
};
```

### Animated view

Refresh state first, then use semantic payloads for optional animation:

```csharp
inventory.Changed += (_, args) =>
{
    if (args.RequiresFullRefresh)
    {
        RebuildInventoryView();
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
        RefreshPosition(context);

    foreach (var move in args.Moved)
    {
        if (!move.IsAutomatic)
            QueueMoveAnimation(move);
    }
};
```

### State-store integration

Treat the inventory as authoritative. Event payloads say which projections need recomputing; they are not a second
mutable inventory model.

```csharp
inventory.Changed += (_, args) =>
{
    if (args.RequiresFullRefresh)
    {
        ReplaceInventoryProjection(
            ReadCompleteProjection(inventory));
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
    {
        UpdateProjectionAt(
            context,
            inventory.Layout.GetItemAt(
                inventory,
                context));
    }
};
```

## Common Mistakes

- Refreshing incrementally even when `RequiresFullRefresh` is true.
- Assuming every successful call emits an event.
- Assuming one event contains exactly one payload category.
- Treating `Inventory.Items` storage indices as visual positions.
- Using only `Added` and missing adds that merge into `Modified`.
- Assuming one item always has one layout context.
- Animating sort-generated moves as deliberate drag-and-drop.
- Reading swap instance properties as the pre-swap occupants.
- Listening only for `MetadataChanged` when split-and-set workflows are possible.
- Ignoring configuration changes because they have no affected contexts.
- Expecting rule mutation to emit `Changed`.
- Updating UI controls from a background mutation thread without dispatching.
- Letting an event-handler exception escape after the inventory has committed.
- Relying only on content events to redraw restored layout data after `Deserialize(...)`.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies And Rules](POLICIES_AND_RULES.md)
- [Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Persistence](PERSISTENCE.md)
- [Extending the system](EXTENDING.md)
