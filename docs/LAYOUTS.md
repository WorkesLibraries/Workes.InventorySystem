# Layouts

Layouts own inventory placement and presentation. They answer where an inventory-owned item instance appears without changing the inventory’s underlying ownership or storage order.

Read [Inventory Operations](INVENTORY_OPERATIONS.md) first for item instances and inventory-owned mutation.

## Ownership And Placement

`Inventory<TKey>.Items` is a read-only list in storage order. A layout maintains a separate mapping from that storage to positions such as:

- ordered entries.
- numbered slots.
- grid cells.
- named equipment positions.
- slots within named sections.
- rectangular multi-cell footprints.

Moving, swapping, sorting, or repacking layout positions does not reorder `Inventory.Items`.

This separation allows one inventory model to support very different user interfaces and placement constraints.

## Built-In Layouts

| Layout | Context | Placement model | Empty positions |
|---|---|---|---|
| `EntryLayout<TKey>` | `EntryLayoutContext<TKey>` | Ordered entry sequence | No fixed gaps |
| `SlotLayout<TKey>` | `SlotLayoutContext<TKey>` | Fixed numbered slots | Yes |
| `GridLayout<TKey>` | `GridLayoutContext<TKey>` | Fixed grid, one stack per cell | Yes |
| `MultiCellGridLayout<TKey>` | `MultiCellGridLayoutContext<TKey>` | Fixed grid with rectangular footprints | Yes |
| `EquipmentLayout<TKey>` | `EquipmentLayoutContext<TKey>` | Named compatible slots | Yes |
| `SectionedLayout<TKey>` | `SectionedLayoutContext<TKey>` | Named sections containing slots | Yes |

Choose a layout based on the application’s presentation and placement model, not merely its item count.

## Create An Inventory With A Layout

An inventory manager has a default layout:

```csharp
var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(99),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);
```

Each inventory receives a clone of the selected layout state:

```csharp
var listInventory = manager.CreateInventory();

var slottedInventory = manager.CreateInventory(
    layout: new SlotLayout<string>(20));
```

Placement changes in one inventory do not affect another inventory created from the same layout template.

## Layout Contexts

An `ILayoutContext<TKey>` carries layout-specific placement instructions.

Examples:

- entry index.
- slot index.
- grid coordinate.
- equipment slot ID.
- section ID and section slot.
- multi-cell anchor coordinate.

Context meaning belongs to the selected layout. A slot context cannot be used with a grid layout.

## Direct Contexts

A direct context addresses one position for one operation.

```csharp
inventory.Add(
    potion,
    amount: 1,
    context: SlotLayoutContext<string>.Single(3));
```

Direct contexts are also used for movement and lookup:

```csharp
var slot0 = SlotLayoutContext<string>.Single(0);
var slot3 = SlotLayoutContext<string>.Single(3);

var item = inventory.Layout.GetItemAt(inventory, slot3);
inventory.Move(slot3, slot0);
```

`ILayoutContext<TKey>.IsMapped` is `false` for direct contexts.

Transaction builders also accept a direct context on each `TryAdd(...)`. This is the preferred transaction workflow when
the add operation already knows its intended position, because the layout can use that context while deciding whether to
merge or create a new item instance. See
[Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md#transaction-placement-contexts).

## Automatic Placement

A `null` context (same as not including context at all) requests automatic placement where the layout supports it:

```csharp
inventory.Add(apple, amount: 5, context: null);
```

Automatic placement is layout-specific:

- entry layout appends a new entry.
- slot layout uses the first available slot.
- grid layouts scan using their configured placement order.
- equipment layout uses the first empty compatible slot.
- sectioned layout scans for the first compatible section slot.
- multi-cell grid layout scans for the first non-overlapping footprint placement.

Automatic placement can fail when no valid position exists.

## Mapped Contexts

A mapped context assigns positions to multiple entries added by one transaction or transfer.

This section is easiest to understand after reading
[Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md), because added-entry indices belong to the transaction being
committed rather than to permanent inventory storage.

`IsMapped` is `true`, and the mapping keys refer to:

- `InventoryTransaction<TKey>.Added` indices for transactions.
- transfer-builder entry order for incoming transfer placement.

Use a mapped context as a deferred-placement tool when the final resulting additions are known and another layer needs
to assign each one a target:

```csharp
var placement = SlotLayoutContext<string>.Map()
    .Add(0, 2)
    .Add(1, 3)
    .Build();
```

The first number passed to each `.Add(...)` is an added-entry index, not a storage index or item ID. The second number is
the layout destination position. Other layout context builders map the same added-entry indices to their own position types.

Mapped contexts describe additions only. They are applied after stacking decisions, so an add that merged completely
into an existing stack has no added-entry index to map. Existing amount deltas and removals are simulated by the layout
during validation but are not assigned mapping entries. The transactions guide explains when deferred mapping is useful,
why per-add direct contexts are normally more ergonomic, and how to inspect the resulting additions safely.

## Context Builder Reference

| Layout | Direct context | Mapped context |
|---|---|---|
| Entry | `EntryLayoutContext<TKey>.Single(index)` | `.Map().Insert(addedIndex, targetIndex).Build()` |
| Slot | `SlotLayoutContext<TKey>.Single(slot)` | `.Map().Add(addedIndex, slot).Build()` |
| Grid | `GridLayoutContext<TKey>.Single(x, y)` | `.Map().Add(addedIndex, x, y).Build()` |
| Multi-cell grid | `MultiCellGridLayoutContext<TKey>.Single(x, y[, anchor])` | `.Map().Add(addedIndex, x, y[, anchor]).Build()` |
| Equipment | `EquipmentLayoutContext<TKey>.Single(slotId)` | `.Map().Add(addedIndex, slotId).Build()` |
| Sectioned | `SectionedLayoutContext<TKey>.Single(sectionId, slot)` | `.Map().Add(addedIndex, sectionId, slot).Build()` |

Each added-entry index may be mapped only once.

## Query Layout Positions

Layouts expose position-oriented reads:

```csharp
var contexts = inventory.Layout.GetAddressableContexts(inventory);
var positionCount = inventory.Layout.GetPositionCount(inventory);

var item = inventory.Layout.GetItemAt(
    inventory,
    SlotLayoutContext<string>.Single(4));
```

Map storage back to presentation with:

```csharp
var itemIndex = 0;

var occupiedContexts =
    inventory.Layout.GetContextsForStorageIndex(inventory, itemIndex);

var foundOne =
    inventory.Layout.TryGetContextForStorageIndex(
        inventory,
        itemIndex,
        out var primaryContext);
```

Most built-in layouts return one context for one storage entry. A multi-cell item can return every occupied cell.

## Entry Layout

`EntryLayout<TKey>` presents inventory stacks as an ordered sequence without fixed empty positions.

```csharp
var layout = new EntryLayout<string>();
```

Behavior:

- automatic placement appends.
- direct placement inserts at an entry index.
- later entries shift as necessary.
- movement and sorting change entry presentation order.
- storage order remains unchanged.

Use entry layout for list, bag, or collection UIs that do not need stable gaps.

```csharp
inventory.Add(
    apple,
    context: EntryLayoutContext<string>.Single(0));
```

The context target may address an existing entry or the insertion position immediately after the final entry when adding.

## Slot Layout

`SlotLayout<TKey>` provides a fixed number of numbered slots.

```csharp
var layout = new SlotLayout<string>(slotCount: 20);
```

Behavior:

- slots are indexed from zero.
- empty slots remain addressable.
- automatic placement chooses the first available slot.
- direct placement targets one slot.
- moving and swapping preserve fixed slot addresses.

```csharp
inventory.Add(
    potion,
    context: SlotLayoutContext<string>.Single(5));

var potionStack = inventory.Layout.GetItemAt(
    inventory,
    SlotLayoutContext<string>.Single(5));
```

Slot layout is useful for hotbars, fixed-size bags, and other interfaces where empty positions matter but positions have no names or compatibility rules.

## Grid Layout

`GridLayout<TKey>` provides a fixed rectangular grid where each item instance occupies one cell.

```csharp
var grid = new GridLayout<string>(
    width: 8,
    height: 5,
    placementOrder: GridPlacementOrder.RowMajor);
```

Coordinates are zero-based:

```csharp
inventory.Add(
    apple,
    context: GridLayoutContext<string>.Single(x: 2, y: 1));
```

`GridPlacementOrder` controls automatic placement and comparer-based sorting scan order:

| Order | Scan |
|---|---|
| `RowMajor` | Left-to-right across each row, then the next row |
| `ColumnMajor` | Top-to-bottom down each column, then the next column |

Use grid layout when every stack occupies exactly one cell. Use multi-cell grid layout when definitions need different footprint sizes.

## Equipment Layout

`EquipmentLayout<TKey>` provides named slots with optional compatibility restrictions.

```csharp
var equipment = new EquipmentLayout<string>(
    new EquipmentSlot<string>("main-hand", "gear:weapon"),
    new EquipmentSlot<string>("head", "gear:armor"));
```

Address positions by stable slot ID:

```csharp
inventory.Add(
    sword,
    context: EquipmentLayoutContext<string>.Single("main-hand"));
```

Automatic placement chooses the first compatible empty slot.

Equipment layout does not support sorting because its slot identities carry semantic meaning.

## Equipment Slot Restrictions

Equipment slots may restrict placement by:

- required catalog-resolved tags.
- allowed definition IDs.
- canonical definition objects converted to their IDs.
- a combination of tags and definitions.

```csharp
var equipment = new EquipmentLayout<string>(
    new EquipmentSlot<string>(
        "main-hand",
        new EquipmentSlotOptions<string>
        {
            RequiredTags = new[] { "gear:weapon" },
            AllowedDefinitions = new[] { familyHeirloom }
        }),
    new EquipmentSlot<string>("head", "gear:armor"));
```

Compatibility rules:

| Configuration | Accepted item |
|---|---|
| Tags only | Satisfies every required tag |
| Definitions only | Definition ID is explicitly allowed |
| Tags and definitions | Satisfies required tags **or** has an allowed definition ID |
| Neither | Any item that otherwise passes inventory validation |

`AllowedDefinitions` is convenient when canonical objects are available. `AllowedDefinitionIds` is useful when configuration already stores IDs.

Definition restrictions compare IDs using `EqualityComparer<TKey>.Default`. Inventory operations still enforce canonical catalog registration.

Layout compatibility answers whether an item can be represented in a position. It does not replace stack resolution, capacity policies, inventory rules, or catalog validation.

## Sectioned Layout

`SectionedLayout<TKey>` groups fixed slots into named sections.

```csharp
var layout = new SectionedLayout<string>(
    new SectionDefinition<string>(
        "tools",
        slotCount: 2,
        options: new SectionDefinitionOptions<string>
        {
            RequiredTags = new[] { "gear:tool" },
            AllowedDefinitions = new[] { lockpick }
        }),
    new SectionDefinition<string>(
        "bag",
        slotCount: 8));
```

Address a position with section ID and section-local slot index:

```csharp
inventory.Add(
    axe,
    context: SectionedLayoutContext<string>.Single(
        sectionId: "tools",
        slotIndex: 0));
```

Sections use the same compatibility rules as equipment slots:

- all required tags form one tag-based path.
- any allowed definition ID forms an alternative definition-based path.
- tag match or definition match is sufficient when both are configured.
- no restrictions means unrestricted by the layout.

Automatic placement scans for the first compatible empty section slot. In the example:

- a tagged axe fits `tools`.
- the explicitly allowed lockpick fits `tools`.
- an apple skips `tools` and uses the unrestricted `bag`.

Sectioned layout is useful for inventories such as:

- hotbar plus bag.
- tool area plus general storage.
- quest storage plus unrestricted storage.

## Multi-Cell Grid Layout

`MultiCellGridLayout<TKey>` places rectangular item footprints into a fixed grid.

```csharp
var grid = new MultiCellGridLayout<string>(
    width: 8,
    height: 5,
    footprintProvider: footprintProvider,
    placementOrder: GridPlacementOrder.RowMajor,
    defaultAnchor: GridAnchor.TopLeft);
```

One item instance may occupy several cells. Placement rejects:

- footprints outside grid bounds.
- overlap with another item.
- invalid or non-positive footprint dimensions.

Every occupied cell resolves to the same item instance:

```csharp
var item = inventory.Layout.GetItemAt(
    inventory,
    MultiCellGridLayoutContext<string>.Single(3, 2));
```

`GetContextsForStorageIndex(...)` returns every cell occupied by that item.

## Footprints

`GridFootprint` stores positive width and height values.

`IGridFootprintProvider<TKey>` resolves a footprint from an item definition. The built-in `AttributeGridFootprintProvider<TKey>` reads two integer definition attributes:

```csharp
private const string FootprintWidth = "footprint-width";
private const string FootprintHeight = "footprint-height";

var provider = new AttributeGridFootprintProvider<string>(
    FootprintWidth,
    FootprintHeight,
    defaultFootprint: new GridFootprint(1, 1));
```

If either attribute is absent, the provider uses its default footprint.

Projects choose the attribute IDs and definition-authoring model. The package does not impose a footprint-specific item-definition base class.

One possible definition class is:

```csharp
sealed class FootprintDefinition : ItemDefinition<string>
{
    public const string WidthAttribute = "footprint-width";
    public const string HeightAttribute = "footprint-height";

    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<FootprintDefinition>("footprint-item")
            .RequireAttribute<int>(WidthAttribute)
            .RequireAttribute<int>(HeightAttribute);

    public FootprintDefinition(string id, int width, int height)
        : base(id, Schema)
    {
        DefineAttribute(WidthAttribute, width);
        DefineAttribute(HeightAttribute, height);
    }
}
```

Catalog setup declares those attributes:

```csharp
catalog.Attributes.Define<int>(FootprintDefinition.WidthAttribute);
catalog.Attributes.Define<int>(FootprintDefinition.HeightAttribute);

var table = new FootprintDefinition("table", width: 2, height: 1);
var chest = new FootprintDefinition("chest", width: 2, height: 2);

catalog.Registry.Register(table);
catalog.Registry.Register(chest);
catalog.Freeze();
```

## Multi-Cell Anchors

A multi-cell context coordinate refers to one corner of the footprint.

`GridAnchor` supports:

- `TopLeft`.
- `TopRight`.
- `BottomLeft`.
- `BottomRight`.

When a context omits its anchor, the layout uses `DefaultAnchor`:

```csharp
inventory.Add(
    table,
    context: MultiCellGridLayoutContext<string>.Single(
        x: 2,
        y: 0,
        anchor: GridAnchor.TopRight));
```

For a 2-by-1 footprint, top-right anchoring at `(2, 0)` occupies `(1, 0)` and `(2, 0)`.

A mapped context may choose an anchor per addition:

```csharp
var placement = MultiCellGridLayoutContext<string>.Map()
    .Add(0, 2, 0, GridAnchor.TopRight)
    .Add(1, 4, 2, GridAnchor.BottomRight)
    .Build();
```

## Move And Swap

Normal application code moves and swaps through the inventory:

```csharp
inventory.Move(fromContext, toContext);
inventory.Swap(firstContext, secondContext);
```

Use `TryMove(...)` and `TrySwap(...)` for expected placement rejection.

The layout validates:

- source and destination context types.
- bounds and occupancy.
- equipment or section compatibility.
- footprint overlap and bounds for multi-cell grids.

Move and swap change placement only. They preserve item instances, amounts, metadata, and storage order.

## Repack

`TryRepackLayout(...)` compacts placement using current visible layout order and normal automatic placement:

```csharp
var repacked = inventory.TryRepackLayout(out var error);
```

Throwing form:

```csharp
inventory.RepackLayout();
```

Repack:

- preserves the same item instances.
- preserves `Inventory.Items` storage order.
- reads items in current layout order.
- places them again through normal auto-placement.
- does not use an item comparer.

For a slot layout with items at slots `1`, `3`, and `4`, repack moves them to `0`, `1`, and `2` while preserving their previous visible order.

For grid layouts, auto-placement follows `GridPlacementOrder`.

Repack can fail if the contents cannot be represented through the layout's normal auto-placement.

Built-in support is deliberate rather than universal:

| Layout | Repack support | Reason |
|---|---|---|
| `EntryLayout<TKey>` | No | Entry positions have no gaps, so repack would always be a guaranteed no-op. |
| `SlotLayout<TKey>` | Yes | Interchangeable numbered slots can be compacted safely. |
| `GridLayout<TKey>` | Yes | Interchangeable cells can be compacted in placement order. |
| `MultiCellGridLayout<TKey>` | Yes | Footprints can be placed again through the configured scan strategy. |
| `SectionedLayout<TKey>` | Yes | Slots can be compacted while preserving section compatibility. |
| `EquipmentLayout<TKey>` | No | Named equipment positions are semantically meaningful and must not be reassigned merely to remove gaps. |

A custom layout opts in through `IRepackableInventoryLayout<TKey>`.

### Custom Layout Repack Support

`IRepackableInventoryLayout<TKey>` asks a layout to create an empty replacement with equivalent configuration. It does
not ask the layout to move live items itself. The inventory:

- reads storage indices in current visible layout order.
- asks the capability for an empty configured target.
- simulates normal context-less placement into that target.
- validates layout, stack, capacity, and rule constraints.
- commits the new layout atomically only when the complete candidate succeeds.
- reports the committed placement change through `Inventory.Changed`.

This separation keeps repack layout-agnostic while preserving inventory ownership of validation, mutation, and events.
A capability rejection or candidate-placement failure leaves the original layout and item instances unchanged and emits
no event.

Parameterized custom layouts can additionally implement `IParameterizedRepackableInventoryLayout<TKey>`. Its
`TryCreateEmptyRepackLayoutWithParameter(...)` method creates an empty target with the proposed configuration.
`IParameterizedInventoryLayout<TKey>.TryCreateWithParameter(...)` remains the separate preserve-placement path. A
custom layout may therefore support parameter changes without supporting repack, direct repack without parameterized
repack, or both capabilities.

## Sorting

Sorting changes layout placement according to an item comparer or layout-specific sort context:

```csharp
var sorted = inventory.TrySortLayout(
    (left, right) => string.CompareOrdinal(
        left.Definition.Id,
        right.Definition.Id),
    out var error);
```

Throwing form:

```csharp
inventory.SortLayout(
    (left, right) => string.CompareOrdinal(
        left.Definition.Id,
        right.Definition.Id));
```

Available input forms:

- `IComparer<ItemInstance<TKey>>`.
- `Comparison<ItemInstance<TKey>>`.
- `IInventorySortContext<TKey>`.

Sorting never changes `Inventory.Items` storage order.

## Built-In Sorting Support

| Layout | Behavior |
|---|---|
| `EntryLayout<TKey>` | Reorders visible entries by comparer. |
| `SlotLayout<TKey>` | Places items into sorted slot order. |
| `GridLayout<TKey>` | Places items by comparer using grid scan order. |
| `SectionedLayout<TKey>` | Sorts within section compatibility constraints. |
| `MultiCellGridLayout<TKey>` | Supports item-order and compact-footprint modes. |
| `EquipmentLayout<TKey>` | Sorting is unsupported. |

## Multi-Cell Sorting

Item-order mode prioritizes the comparer and repacks in that order:

```csharp
inventory.TrySortLayout(
    MultiCellGridSortContext<string>.ByItems(
        (left, right) => string.CompareOrdinal(
            left.Definition.Id,
            right.Definition.Id)),
    out var itemOrderError);
```

Compact mode prioritizes deterministic space-efficient footprint packing:

```csharp
inventory.TrySortLayout(
    MultiCellGridSortContext<string>.Compact(
        (left, right) => string.CompareOrdinal(
            left.Definition.Id,
            right.Definition.Id)),
    out var compactError);
```

The optional comparer is a tie-breaker in compact mode.

Compact sorting uses a deterministic heuristic. It is not guaranteed to find the mathematically optimal bin-packing result.

## Repack Versus Sort

| Goal | Operation |
|---|---|
| Remove gaps while keeping current visible order | `TryRepackLayout(...)` |
| Order items by definition, amount, metadata, or another comparer | `TrySortLayout(...)` |
| Use multi-cell footprint size as the primary objective | `MultiCellGridSortContext<TKey>.Compact(...)` |

Both operations preserve storage order and item instances.

## Layout Change Notifications

Successful move, swap, sort, and visible repack operations notify through `Inventory.Changed`.

Event data can identify:

- moved item instances.
- source and destination layout contexts.
- all affected layout contexts.

Each moved instance carries an `ItemMovementCause`. Direct movement uses `ExplicitMove`, sorting uses `Sort`, repacking
uses `Repack`, and surviving instances displaced as a consequence of another mutation use `LayoutReflow`.

A visible direct repack emits complete before/after `Moved` payloads and affected contexts, so it does not request a
full refresh. Repacking an already compact layout produces no change event.

Entry layout also reports collateral reflow after ordinary structural mutations. Indexed insertion, removal, merging
away a stack, transfer, or a multi-operation transaction can shift surviving entries. Every survivor whose final entry
context differs appears in `Moved` with the `LayoutReflow` cause, and its source and destination are included in
`AffectedLayoutContexts`. When a direct Entry move also displaces neighbors, the targeted instance uses `ExplicitMove`
while its displaced neighbors use `LayoutReflow`.

### Custom Layout Reconciliation

A custom layout whose observable state can reflow after inventory mutations implements
`IInventoryLayoutReconciler<TKey>`.

After an accepted mutation, the inventory:

1. lets the layout reconcile its layout-owned state from the final inventory state.
2. compares every surviving item context with the context captured before the complete operation.
3. adds all changed survivors to the same event's `Moved` collection.
4. merges the layout's supplemental affected contexts and full-refresh request.

`ReconcileAfterInventoryMutation(...)` is a post-validation reconciliation callback, not another acceptance phase. It
must not mutate inventory contents or reject an operation that already passed layout, capacity, stack, and rule
validation. Use normal layout validation methods to reject invalid proposals.

Implement the capability when additions/removals can shift survivors or when ordering depends on mutable state such as
amount or metadata. Fixed-position layouts do not need it and avoid the surviving-item diff cost. The built-in
`EntryLayout<TKey>` implements it for shifting entry contexts.

`InventoryLayoutReconciliationResult<TKey>` can add layout-owned affected contexts or request
`RequiresFullRefresh` when contexts and moved items cannot completely describe a presentation change, such as
addressable-context topology or other layout-owned presentation state. Do not request it merely because reconciliation
moves many items.

The [events and UI guide](EVENTS_AND_UI.md) covers these payloads in detail.

## Layout Persistence

`Inventory.Serialize()` stores:

- serialized item instances in storage order.
- layout-specific data in `SerializedInventory<TKey>.LayoutData`.

Each built-in layout has a corresponding persistent-data type:

| Layout | Persistent data |
|---|---|
| Entry | `EntryLayoutPersistentData` |
| Slot | `SlotLayoutPersistentData` |
| Grid | `GridLayoutPersistentData` |
| Multi-cell grid | `MultiCellGridLayoutPersistentData` |
| Equipment | `EquipmentLayoutPersistentData` |
| Sectioned | `SectionedLayoutPersistentData` |

Persistent layout data records the mapping from presentation positions to inventory storage indices. Grid-style data also records dimensions and placement settings where needed; equipment and sectioned data record their stable slot or section identities.

`Inventory.Deserialize(...)` restores item contents and then asks the current layout to restore the saved layout data.

The target inventory must use a compatible layout configuration. Restoration rejects mismatched:

- persistent-data types.
- grid dimensions or placement settings.
- equipment slot identities.
- section identities or sizes.
- other layout-specific structural data.

The persistence guide covers serialization boundaries and compatibility in more detail.

## Runtime Layout Parameters

Some built-in layouts expose inventory-owned runtime parameters:

| Layout | Parameter IDs |
|---|---|
| Slot | `slotCount` |
| Grid | `width`, `height`, `placementOrder` |
| Multi-cell grid | `width`, `height`, `placementOrder`, `defaultAnchor` |
| Sectioned | `section:{sectionId}.slotCount` |

Use `Inventory<TKey>.TrySetLayoutParameter(...)` or its throwing wrapper rather than mutating layout state directly.

There are two placement behaviors:

| Call | Placement behavior |
|---|---|
| `TrySetLayoutParameter(parameterId, value, out error)` | Preserves current placements and rejects the change if an occupied context would become invalid. |
| The overload with `InventoryParameterMutationActions.RepackLayout` | Creates the proposed layout empty and places entries again in current visible layout order using normal automatic placement. |

For example, consider an eight-slot layout with three occupied positions:

```text
Before:                 [A - - - B - - C]
Shrink to four,
preserving placement:   rejected
Shrink to four
with RepackLayout:      [A B C -]
```

```csharp
var changed = inventory.TrySetLayoutParameter(
    "slotCount",
    4,
    InventoryParameterMutationActions.RepackLayout,
    out var error);
```

Repack preserves the item instances and `Inventory.Items` storage order; it changes only their layout placement. The
change can still be rejected when the proposed layout cannot automatically place every entry or footprint. Both modes
validate the complete proposed result and commit atomically, so rejection leaves the current layout unchanged.

For custom layouts, the preserve-placement overload requires `IParameterizedInventoryLayout<TKey>`. The repack overload
requires `IParameterizedRepackableInventoryLayout<TKey>` as well. Parameter parsing and creation of the empty proposed
configuration belong to the layout rather than `Inventory<TKey>`.

See [Policies And Rules](POLICIES_AND_RULES.md#mutation-actions) for supported action combinations, stack splitting,
compression, and the deeper rebuild behavior.

## Choosing A Layout

| Requirement | Recommended layout |
|---|---|
| Simple ordered list with no fixed gaps | `EntryLayout<TKey>` |
| Fixed number of interchangeable positions | `SlotLayout<TKey>` |
| Fixed grid with one stack per cell | `GridLayout<TKey>` |
| Named semantic positions | `EquipmentLayout<TKey>` |
| Several named slot groups | `SectionedLayout<TKey>` |
| Items with different rectangular sizes | `MultiCellGridLayout<TKey>` |

## Common Mistakes

- Treating `Inventory.Items` order as UI order.
- Passing a context type that belongs to another layout.
- Using a mapped context for a single direct operation.
- Mapping storage indices instead of transaction added-entry indices.
- Assuming a `null` context means the same position for every layout.
- Expecting equipment layout to sort.
- Using grid layout for variable-size footprints.
- Treating tag/definition compatibility as a replacement for inventory rules.
- Changing placement directly through low-level layout methods instead of inventory operations.
- Restoring saved layout data into an incompatible layout configuration.
- Assuming compact multi-cell sorting is optimal bin packing.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Policies and rules](POLICIES_AND_RULES.md)
- [Transactions and transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Events and UI integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
