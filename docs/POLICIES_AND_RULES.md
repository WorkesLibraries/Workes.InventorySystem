# Policies And Rules

Stack resolvers, capacity policies, rules, and layouts answer different questions about an inventory:

| Concern | Question |
|---|---|
| Stack resolver | How large may each compatible stack become? |
| Capacity policy | Does the inventory-wide resource limit allow the proposed result? |
| Rules | Are the items and resulting inventory state semantically allowed? |
| Layout | Can the stack entries be represented, and where are they placed? |

The inventory coordinates all four concerns. Application code chooses components during setup and routes later changes
through `Inventory<TKey>` so current contents can be validated before anything is committed.

Read [Core Concepts](CONCEPTS.md) for the overall object model and [Layouts](LAYOUTS.md) for placement behavior.

## Configuration Versus Runtime Mutation

Creation-time configuration supplies complete component objects and a rule container:

```csharp
var rules = new RuleContainer<string>();
rules.Add(
    "equipment-only",
    new RequireAnyTagRule<string>("core:equipment"));

var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(maxStack: 20),
    new MaxTotalItemAmountCapacityPolicy<string>(
        maxTotalItemAmount: 100),
    new SlotLayout<string>(slotCount: 24),
    catalog,
    rules);

var inventory = manager.CreateInventory();
```

The manager provides defaults. `CreateInventory(...)` can override the stack resolver, capacity policy, layout, or rules
for one inventory.

At runtime:

- change rules with inventory-owned rule methods such as `TrySetRule(...)`.
- change exposed component parameters with `TrySetStackResolverParameter(...)`,
  `TrySetCapacityPolicyParameter(...)`, or `TrySetLayoutParameter(...)`.
- do not mutate a shared component or the manager's default rule container as a way to reconfigure an existing
  inventory.

Layouts and rule containers are cloned when an inventory is created. Parameterized stack resolvers, capacity policies,
and layouts create replacement component instances when a parameter changes. The inventory validates the proposed
replacement and commits it only if the complete result is valid.

## Stack Resolution

A stack resolver returns the maximum stack size for an item definition. Adds first merge into compatible stacks selected
by the layout, then create new stacks for any remaining amount.

Stack compatibility is separate from maximum size. `ItemInstance<TKey>.IsStackCompatible(...)` requires:

- the same definition ID.
- structurally equal instance metadata.

Two instances with different metadata therefore remain separate even when their definitions use the same stack limit.

### Built-In Stack Resolvers

| Resolver | Constructor | Behavior |
|---|---|---|
| `FixedSizeStackResolver<TKey>` | `(maxStack)` | Uses one maximum for every definition. |
| `ConditionalMaxStackResolver<TKey>` | `(stackableAttributeId, maxStack, missingAttributeIsStackable = false)` | A Boolean definition attribute chooses `maxStack` or `1`. |
| `AttributeMaxStackResolver<TKey>` | `(maxStackAttributeId, missingAttributeMaxStack = null)` | An integer definition attribute supplies the maximum. |
| `MultipliedAttributeStackResolver<TKey>` | `(baseStackAttributeId, multiplier, missingAttributeBaseStack = null)` | Multiplies an integer definition attribute by an inventory-specific multiplier. |

Attribute-driven resolvers read definition attributes, not instance metadata. Declare their attribute IDs in the catalog
before freeze:

```csharp
catalog.Attributes.Define<bool>("stackable");
catalog.Attributes.Define<int>("base-stack");

var pouchResolver = new ConditionalMaxStackResolver<string>(
    stackableAttributeId: "stackable",
    maxStack: 20,
    missingAttributeIsStackable: false);

var warehouseResolver =
    new MultipliedAttributeStackResolver<string>(
        baseStackAttributeId: "base-stack",
        multiplier: 5);
```

`MultipliedAttributeStackResolver<TKey>` calculates `floor(baseStack * multiplier)` with a minimum result of `1`.

The nullable fallback values control missing-attribute behavior:

- a non-null fallback supplies a value when the definition lacks the attribute.
- `null` enables strict mode, which rejects resolution for a definition missing that attribute.

Missing resolver attributes do not by themselves fail catalog freeze unless the definition's schema independently
requires them.

## Capacity Policies

Capacity policies validate inventory-wide, non-spatial resources. They do not decide whether a layout has a free slot or
whether an item is semantically appropriate.

| Policy | Behavior |
| --- | --- |
| `UnlimitedCapacityPolicy<TKey>` | Always accepts the capacity check. |
| `MaxTotalItemAmountCapacityPolicy<TKey>` | Limits the sum of all item amounts. |
| `WeightCapacityPolicy<TKey>` | Limits summed per-unit definition weight. |

```csharp
catalog.Attributes.Define<int>("weight");

var capacity = new WeightCapacityPolicy<string>(
    weightAttributeId: "weight",
    maxWeight: 25,
    treatMissingWeightAsZero: true);
```

`WeightCapacityPolicy<TKey>` reads the weight from each registered definition and multiplies it by the corresponding
item amount. With `treatMissingWeightAsZero: true`, a definition without the weight attribute contributes zero. With
`false`, a missing weight rejects validation.

Capacity is checked for normal adds, transactions, transfers, metadata mutation, restore, rebuild validation, and
capacity-parameter changes. Reducing a capacity limit succeeds only when the current inventory already fits the proposed
limit.

## Rules

Rules express semantic, structural, and final-state constraints that do not belong to capacity or placement.

| Contract | View available to the rule |
|---|---|
| `IRulePolicy<TKey>` | Normalized semantic additions and removals |
| `IInventoryStructuralRulePolicy<TKey>` | Structural transaction details such as item-instance changes |
| `IInventorySnapshotRulePolicy<TKey>` | Projected final whole-inventory state |
| `InventorySnapshotRulePolicy<TKey>` | Base class for implementing snapshot-aware rules |

`RuleContainer<TKey>` stores rules under stable IDs. Enabled rules run in descending priority order; rules with equal
priority retain insertion order. Validation stops at the first rejection and includes the failing rule ID and type in
the error.

### Built-In Rules

Choose the narrowest rule that expresses the constraint:

| Area | Rules |
|---|---|
| Tags | `RequireAnyTagRule<TKey>`, `RequireAllTagsRule<TKey>` |
| Allowed definitions | `OnlyAllowItemsRule<TKey>` |
| Definition attributes | `RequireAttributeRule<TKey, TValue>`, `AttributeEqualsRule<TKey, TValue>`, `AttributeOneOfValuesRule<TKey, TValue>`, `AttributePredicateRule<TKey, TValue>` |
| Definition predicate | `ItemPredicateRule<TKey>` |
| Instance metadata | `RequireMetadataKeyRule<TKey>`, `RequireMetadataRule<TKey>`, `RequireMetadataOneOfValuesRule<TKey>`, `MetadataRangeRule<TKey, T>` |
| Final inventory shape | `UniqueItemRule<TKey>`, `MaxUniqueItemsRule<TKey>` |
| Composition | `OrRule<TKey>`, `NotRule<TKey>` |

`UniqueItemRule<TKey>` limits the number of item instances per definition, not the total item amount.
`MaxUniqueItemsRule<TKey>` limits the number of distinct definitions in the projected inventory.

Examples:

```csharp
var lightItemsOnly =
    new AttributePredicateRule<string, int>(
        "weight",
        weight => weight <= 6,
        "Expected weight to be 6 or less");

var usableQuestItem = new OrRule<string>(
    new RequireAnyTagRule<string>("core:usable"),
    new RequireAnyTagRule<string>("core:quest"));

var noBrokenItems = new NotRule<string>(
    new RequireMetadataRule<string>("condition", "broken"));
```

Tag rules use catalog-resolved tag membership, including schema tags and generated parent tags. Attribute rules read
definition attributes. Metadata rules read per-instance metadata. See
[Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md) for those data boundaries.

## Configure Rules During Setup

Build a `RuleContainer<TKey>` before inventory creation when a rule should be a default:

```csharp
var defaultRules = new RuleContainer<string>();

defaultRules.Add(
    "light-only",
    new AttributePredicateRule<string, int>(
        "weight",
        weight => weight <= 6),
    priority: 100);

defaultRules.Add(
    "quest-lock",
    new RequireAnyTagRule<string>("core:quest"),
    priority: 50,
    enabled: false);
```

The dictionary key passed to `Add` or `Set` is the stable management ID used for errors and later runtime operations.
The container wraps the supplied rule so that this explicit ID is authoritative.

Every created inventory receives its own cloned rule container. Runtime changes to one inventory do not affect another
inventory or the manager defaults.

## Mutate Rules At Runtime

Runtime rule changes go through the inventory:

| API | Behavior |
|---|---|
| `TrySetRule(id, rule, out error)` | Adds or replaces a rule, preserving an existing entry's priority and enabled state. |
| `TrySetRule(id, rule, priority, enabled, out error)` | Adds or replaces with explicit priority and enabled state. |
| `TryRemoveRule(id, out error)` | Removes a rule. |
| `TrySetRuleEnabled(id, enabled, out error)` | Enables or disables a rule. |
| `TrySetRulePriority(id, priority, out error)` | Changes evaluation priority. |
| `SetRule`, `RemoveRule`, `SetRuleEnabled`, `SetRulePriority` | Throwing wrappers for expected-success flows. |

Before committing, the inventory:

1. clones its current rule container.
2. applies the proposed change to the clone.
3. validates all current contents against the proposed rule set.
4. replaces the live rule set only when validation succeeds.

This makes a rejected rule change atomic:

```csharp
var questOnly =
    new OnlyAllowItemsRule<string>(questGem);

if (!inventory.TrySetRule(
        "quest-only",
        questOnly,
        out var error))
{
    // Existing contents violate the proposed rule.
    // The previous rule set remains active.
}
```

Enabling a disabled rule can fail when current contents violate it. Removing or disabling a rule normally relaxes
validation, but still uses the same proposed-rule-set path. Unknown IDs are rejected by remove, enable, and priority
operations.

Rule-container changes do not currently emit `Inventory<TKey>.Changed`; they change validation configuration without
changing contents or layout. Code that presents editable rule configuration should update that view from the result of
the rule-mutation call.

## Runtime Component Parameters

Parameterized components expose stable string IDs through `InventoryParameterDefinition`. A parameter definition
contains:

- `Id`.
- expected `ValueType`.
- a developer-facing `Description`.

The corresponding `TryCreateWithParameter(...)` contract creates a proposed replacement component. Normal application
code does not call that extension contract directly; it uses the inventory-owned APIs:

```csharp
inventory.TrySetStackResolverParameter(
    "maxStack",
    10,
    out var stackError);

inventory.TrySetCapacityPolicyParameter(
    "maxTotalItemAmount",
    100,
    out var capacityError);

inventory.TrySetLayoutParameter(
    "slotCount",
    30,
    out var layoutError);
```

The parameter-mutation flow is:

```text
Validate parameter ID and action flags
  -> create a proposed replacement component
  -> validate current contents and requested rebuild
  -> commit the replacement atomically
  -> emit one configuration-change event
```

Rejected changes preserve the component, stack shape, contents, and layout and emit no event. Throwing wrappers
`SetStackResolverParameter(...)`, `SetCapacityPolicyParameter(...)`, and `SetLayoutParameter(...)` preserve the same
atomic behavior and throw `InvalidOperationException` on rejection.

## Preserve-Only Is The Default

Overloads without action flags use `InventoryParameterMutationActions.None`. They preserve current stack shape and
layout placement.

Examples:

- increasing `maxStack` changes future stacking but leaves existing stacks separate.
- increasing `slotCount` succeeds if every current placement remains valid.
- lowering a capacity limit succeeds only if current contents already fit.
- lowering `maxStack` below an existing stack amount is rejected.

Use mutation actions only when the current representation must be rebuilt to satisfy the proposed parameter.

## Mutation Actions

`InventoryParameterMutationActions` is a flags enum:

| Action | Effect |
|---|---|
| `None` | Preserves stack shape and placement. |
| `SplitOversizedStacks` | Splits stacks that exceed the proposed stack maximum. |
| `CompressCompatibleStacks` | Merges compatible amounts into fuller earlier stacks. |
| `RepackLayout` | Re-places entries in current visible layout order using normal automatic placement. |

Combine actions with `|`.

### Splitting Oversized Stacks

Use splitting when a reduced maximum would otherwise invalidate existing stacks:

```csharp
var changed = inventory.TrySetStackResolverParameter(
    "maxStack",
    4,
    InventoryParameterMutationActions.SplitOversizedStacks,
    out var error);
```

A stack of `10` becomes `4, 4, 2`. Split chunks preserve metadata. Split-only keeps existing placements where possible
and must find valid placement for the additional stack entries. If the layout cannot represent those entries, the
entire change is rejected.

### Compressing Compatible Stacks

Use compression when increasing a maximum and existing compatible stacks should be consolidated:

```csharp
var changed = inventory.TrySetStackResolverParameter(
    "maxStack",
    25,
    InventoryParameterMutationActions.CompressCompatibleStacks,
    out var error);
```

Four stacks of `10` can become `25, 15`. Compression preserves the relative position of incompatible entries and never
merges different definition IDs or structurally different metadata.

Increasing the maximum without `CompressCompatibleStacks` leaves the four existing stacks unchanged and applies the
larger maximum only to future stacking.

### Repacking Layout

Use repack when the proposed layout or rebuilt stack entries should be placed again:

```csharp
var changed = inventory.TrySetLayoutParameter(
    "slotCount",
    3,
    InventoryParameterMutationActions.RepackLayout,
    out var error);
```

Repack reads entries in current visible layout order and uses the proposed layout's normal automatic-placement behavior.
It is not a sort and does not use `Inventory.Items` storage order or an item comparer.

Repack can still fail when automatic placement cannot represent all entries. For layout compaction without a parameter
change, use `TryRepackLayout(...)` or `RepackLayout()` instead.

Direct layout compaction preserves existing item instances. Parameter changes using rebuild actions currently recreate
equivalent instances from their definitions, amounts, and metadata and report removals and additions with a full
refresh. This preserves item contents, but application code must not retain the old instance references.

Slot, grid, multi-cell grid, and sectioned layouts expose the required repack capabilities. Entry layout deliberately
does not because repack would always be a no-op. Equipment layout deliberately does not because named positions must
not be automatically reassigned. Custom layouts use:

| Capability | Purpose |
|---|---|
| `IRepackableInventoryLayout<TKey>` | Creates an empty equivalent target for direct repack and repack requested by another component mutation. |
| `IParameterizedRepackableInventoryLayout<TKey>` | Creates an empty target with one layout parameter changed. |

The existing `IParameterizedInventoryLayout<TKey>` contract remains responsible for parameter changes that preserve
current placement. Implementing it alone does not opt a custom layout into parameterized repack. Layout capabilities
create proposed empty configurations; the inventory still owns ordering, automatic-placement simulation, complete
validation, atomic commit, and events.

### Supported Action Combinations

| Mutation target | Supported actions |
|---|---|
| Stack resolver | Any combination of `SplitOversizedStacks`, `CompressCompatibleStacks`, and `RepackLayout` |
| Capacity policy | `None` only |
| Layout | `None` or `RepackLayout` |

Common stack-resolver combinations:

```csharp
var splitAndRepack =
    InventoryParameterMutationActions.SplitOversizedStacks |
    InventoryParameterMutationActions.RepackLayout;

var compressAndRepack =
    InventoryParameterMutationActions.CompressCompatibleStacks |
    InventoryParameterMutationActions.RepackLayout;

var fullRebuild =
    InventoryParameterMutationActions.SplitOversizedStacks |
    InventoryParameterMutationActions.CompressCompatibleStacks |
    InventoryParameterMutationActions.RepackLayout;
```

Splitting and compression do not imply repack. Include `RepackLayout` only when placement should also be rebuilt.
Unsupported action bits and actions that do not belong to the mutation target are rejected before commit.

## Built-In Parameter IDs

Treat parameter IDs as part of application configuration. Centralize them when practical, especially generated IDs.

### Stack resolvers

| Component | Parameter ID | Value type | Meaning |
|---|---|---|---|
| `FixedSizeStackResolver<TKey>` | `maxStack` | `int` | Fixed maximum stack size |
| `ConditionalMaxStackResolver<TKey>` | `maxStack` | `int` | Maximum when the stackable attribute is true |
| `ConditionalMaxStackResolver<TKey>` | `missingAttributeIsStackable` | `bool` | Missing stackable-attribute fallback |
| `AttributeMaxStackResolver<TKey>` | `missingAttributeMaxStack` | `int` or `null` | Missing maximum fallback; `null` enables strict mode |
| `MultipliedAttributeStackResolver<TKey>` | `multiplier` | `double` | Multiplier for the base-stack attribute |
| `MultipliedAttributeStackResolver<TKey>` | `missingAttributeBaseStack` | `int` or `null` | Missing base fallback; `null` enables strict mode |

### Capacity policies

| Component | Parameter ID | Value type | Meaning |
|---|---|---|---|
| `MaxTotalItemAmountCapacityPolicy<TKey>` | `maxTotalItemAmount` | `int` | Maximum sum of item amounts |
| `WeightCapacityPolicy<TKey>` | `maxWeight` | `double` | Maximum total weight |
| `WeightCapacityPolicy<TKey>` | `treatMissingWeightAsZero` | `bool` | Missing weight handling |

`WeightCapacityPolicy<TKey>` also accepts `int` and `float` values for `maxWeight` and converts them to `double`.

### Layouts

| Component | Parameter ID | Value type |
|---|---|---|
| `SlotLayout<TKey>` | `slotCount` | `int` |
| `GridLayout<TKey>` | `width` | `int` |
| `GridLayout<TKey>` | `height` | `int` |
| `GridLayout<TKey>` | `placementOrder` | `GridPlacementOrder` |
| `MultiCellGridLayout<TKey>` | `width` | `int` |
| `MultiCellGridLayout<TKey>` | `height` | `int` |
| `MultiCellGridLayout<TKey>` | `placementOrder` | `GridPlacementOrder` |
| `MultiCellGridLayout<TKey>` | `defaultAnchor` | `GridAnchor` |
| `SectionedLayout<TKey>` | `section:{sectionId}.slotCount` | `int` |

Section parameter IDs are generated from the stable section ID. For example:

```csharp
inventory.TrySetLayoutParameter(
    "section:bag.slotCount",
    6,
    InventoryParameterMutationActions.RepackLayout,
    out var error);
```

## Events And Refresh

Every successful component-parameter mutation emits one `Inventory<TKey>.Changed` event whose
`ConfigurationChanged` collection contains the replacement:

| Result | Full refresh |
|---|---|
| Preserve-only stack resolver change | No |
| Preserve-only capacity policy change | No |
| Any successful layout parameter change | Yes |
| Stack split or compression without repack | No; normal item and affected-context payloads describe shape changes |
| Stack-resolver mutation containing `RepackLayout` | No for complete built-in layout deltas; custom reconciliation may request one |

Each configuration entry reports its `Kind`, `ParameterId`, proposed value, previous component, replacement component,
and `RequiresFullRefresh`.

```csharp
inventory.Changed += (_, args) =>
{
    if (args.RequiresFullRefresh)
    {
        RefreshWholeInventory();
        return;
    }

    RefreshContexts(args.AffectedLayoutContexts);
};
```

Rejected parameter changes emit no event. Direct `TryRepackLayout(...)` is not a parameter change, so it does not add a
configuration entry; a visible direct repack reports complete moved placements and affected contexts without requesting
a full refresh.

## Common Mistakes

- Using a capacity policy to model slot availability or item-category rules.
- Treating different metadata as stack-compatible.
- Reading instance metadata from an attribute-driven stack resolver or capacity policy.
- Mutating manager defaults and expecting existing inventories to change.
- Editing an inventory's rule container around inventory-owned validation.
- Expecting an increased stack maximum to compress existing stacks automatically.
- Lowering a stack maximum without requesting `SplitOversizedStacks`.
- Assuming split or compression automatically repacks the layout.
- Treating repack as sorting.
- Passing stack actions to a capacity or layout parameter change.
- Hard-coding a generated section parameter ID without keeping the section ID stable.
- Assuming rule mutation emits an inventory contents-change event.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Transactions and transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Metadata](../README.md#metadata)
- [Events and UI integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
- [Extending the system](EXTENDING.md)
