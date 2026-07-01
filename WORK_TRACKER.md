# Work Tracker

This file is the durable planning tracker for the inventory system work. Keep it updated after each slice so planning state does not live only in chat history.

## To-do

No active implementation points remain in this tracker.

## Documentation To-do

No active documentation points remain in this tracker.

## Completed

These points are completed for the current feature iteration.

### 1. Inventory Mutation API Feels Noisy

- Added throwing convenience methods such as `Add`, `RemoveByDefinition`, `Move`, `Swap`, `MergeMove`, and `SortLayout`.
- Existing `Try... out error` APIs remain available for conditional workflows.

### 2. Registry Freezing Through Catalog

- `ItemRegistry<TKey>.Freeze()` is internal.
- Public freeze path is `ItemCatalog<TKey>.Freeze()`.

### 3. Class-Owned Schemas

- Public schema-taking `ItemDefinition<TKey>` constructors were removed.
- Schema-taking constructors remain protected for derived item definition classes.

### 4. Custom Identifier Type Support

- Non-string identifiers are tested with `Guid`.

### 5. Attribute Catalog

- `ItemCatalog<TKey>.Attributes` exists.
- Schema attributes are validated against the catalog on freeze.
- Attribute authoring now uses string IDs.
- `AttributeKey<T>` is internal.
- Definition attributes are externally read-only through string-based `IAttributeView`.

### 6. Non-Namespaced Tag Validity

- Flat `TagKey` values remain compatibility value objects.
- Catalog-declared tags require namespaced IDs.
- Catalog freeze rejects undeclared flat tags used by catalog-registered definitions/schemas.

### 7. Required Manager Catalog Behavior

- `InventoryManager<TKey>` requires an explicit `ItemCatalog<TKey>`.
- Every manager and inventory still has a catalog.
- Inventory creation requires the manager catalog to be frozen.

### 8. Inventory-Owned Rule Mutation

- New inventories clone default or override rule containers instead of sharing them by reference.
- `Inventory<TKey>` exposes safe rule mutation APIs that validate current contents before applying rule changes.
- Invalidating rule changes are rejected without mutating the inventory rule set.
- Throwing rule mutation wrappers are available for workflows where the change is expected to succeed.

### 9. Inventory Policies Are Updatable Safely Through Inventory-Owned APIs

- Rule mutation validation is implemented.
- Stack resolver parameters can be changed through inventory-owned APIs.
- Capacity policy parameters can be changed through inventory-owned APIs.
- Layout parameters can be changed through inventory-owned APIs.
- Current contents are validated before changes are committed.

### 10. Optional Repack/Compress Modes For Policy Changes

- Parameter changes remain preserve-only by default.
- Layout changes can opt into repack through `InventoryParameterMutationOptions`.
- Stack-size reductions can opt into compression/splitting.
- Repack/compress validates the whole proposed state before committing.
- Rejected changes remain atomic and fire no events.

### 11. Transaction Layout-Context API Shape

- Kept both per-entry and transaction-level context paths.
- Added clearer builder build aliases and inventory commit overloads.
- Preferred normal flow is builder builds, inventory commits.

### 12. Transaction Builder And Transfer Helper API Alignment

- Transfer-wide helpers remain on `InventoryTransfer`.
- Builders remain focused on staging/building.
- Added small transaction builder polish instead of broad helper duplication.

### 13. Transaction And Transfer Commit Semantics

- Transactions commit through `Inventory<TKey>`.
- Transfers commit through `InventoryTransfer`.
- Transfer internals consistently check commit results after validation.

### 14. Sort-Result Move Events

- `ItemMoved<TKey>.IsSortResult` exists.
- Manual `Inventory.TryMove(...)` moved events report `IsSortResult == false`.
- `Inventory.TrySortLayout(...)` moved events report `IsSortResult == true`.
- Event examples demonstrate skipping sort-result move animations.

### 15. Metadata Mutation Goes Through Inventory

- Metadata remains loose key/object data with no central catalog.
- Metadata mutation on inventory-owned item instances validates through the owning inventory.
- Metadata mutation emits change events.
- Stack metadata applies to the whole stack.
- Partial-stack metadata changes are supported through explicit split-and-set helpers.

### 16. Built-In Attribute-Driven Stacking Policies

- Added conditional stackability resolver driven by a boolean definition attribute.
- Added max-stack resolver driven by an integer definition attribute.
- Missing stackability attributes can default to stackable or unstackable.
- Missing max-stack attributes can use a nullable fallback.
- Strict max-stack resolver mode fails at runtime when used with definitions missing the attribute.

### 17. Reviewed Extension-Facing Layout/Rule/Policy APIs

- Extension contracts remain public.
- XML docs now distinguish normal inventory usage from extension-facing implementation hooks.
- Concrete low-level implementation methods are hidden from normal IntelliSense where practical with `EditorBrowsable(Never)`.
- UI examples cover metadata-change event handling.

### 18. Identity Type Ergonomics

- Added explicit test coverage for common non-string identity types.
- Covered integer-like, enum, custom, float, and double identity types.
- Integer-like ids remain supported through explicit definition ids.

### 19. String-Owned Tag API Ergonomics

- Normal tag authoring now uses string ids.
- `TagKey` is internal infrastructure.
- Schemas, definitions, rules, layouts, inventory helpers, and transfer helpers accept string tag ids.
- Tags still must be declared in `ItemCatalog<TKey>.Tags` before catalog freeze.
- Resolved tag output exposes public string descriptors.

### 20. Renamed Fixed-Size Stack Resolver

- `FixedSizeStackResolver<TKey>` is the public fixed-size resolver.
- Examples, tests, XML docs, and error messages use the fixed-size name consistently.

### 21. Reviewed Schema Integration And Class-Owned Ergonomics

- Added owned schema creation through `ItemSchema<TKey>.CreateFor<TDefinition>(...)`.
- Schemas can record the item definition type that owns them.
- Catalog validation rejects owned schemas used by unrelated definition types.
- Public schema-taking constructors on registered concrete definition types are rejected.
- Protected schema constructor chaining remains available for class-owned definition hierarchies.
- Simple default-schema definitions remain supported.

### 22. Added Multiplied Attribute Stack Resolver

- Added a stack resolver that reads an integer definition attribute as a base stack ratio/size.
- Resolver-owned multiplier state determines the final inventory-specific max stack size.
- Multiplier can be changed through runtime stack resolver parameter mutation.
- Missing base-stack attributes can use a nullable fallback or strict runtime failure.
- Computed max stack sizes are floored and clamped to a minimum of one.

### 23. Equipment And Sectioned Layouts Can Restrict By Definitions

- Equipment slots can restrict placement by explicit item definition ids.
- Sectioned layout sections can restrict placement by explicit item definition ids.
- Tag restrictions and definition restrictions can be combined.
- Items are accepted when they match required tags or an allowed definition id.
- Slots and sections with no restrictions still allow any otherwise valid item.

### 24. Reviewed Transaction Builder Conversion APIs

- Removed old public transaction-builder conversion methods.
- `Build()` and `TryBuild(...)` are the explicit transaction creation APIs.
- Preferred normal workflow is committing builders through `Inventory<TKey>`.
- Tests and examples use the current builder commit workflow.

### 25. Reviewed Transaction Move And Swap Support

- Transactions remain structural add/remove/amount-delta plans.
- Move and swap remain inventory-level layout operations.
- This avoids adding ordered layout-operation payloads to structural transactions.
- Move and swap continue to emit dedicated movement event payloads.

### 26. Reviewed Transfer And Transaction API Semantics

- Transaction builders stage inventory changes and inventories commit them.
- Transfer builders stage outgoing source removals and source inventories commit them to target inventories.
- `InventoryTransfer.From(...)` remains the transfer builder factory.
- Static transfer action helpers were replaced by source-owned `Inventory<TKey>` transfer methods.
- Cross-inventory swaps are initiated through the first/source inventory.

### 27. Enforced Registered Definitions For Inventory Contents

- Inventories now reject item definitions that are not registered in the connected catalog registry.
- Detached definition objects with the same id as a registered definition are rejected.
- Add, transaction, transfer, split/repack, and deserialization paths preserve the registered-definition invariant.
- Examples now visibly register every definition before catalog freeze.

### 28. Added Flag-Based Stack Mutation Actions

- Runtime stack resolver parameter changes use independent mutation action flags.
- Stack resolver changes support layout repack, oversized-stack splitting, and compatible-stack compression.
- Layout parameter changes support layout repack only.
- Capacity policy parameter changes remain validation-only and do not support mutation actions.
- Oversized-stack splitting adds required chunks through normal layout placement without forcing repack.
- Compatible-stack compression merges later compatible stack amounts into earlier stacks without forcing repack.
- Repack remains an explicit action and triggers full refresh when used.

### 29. Added Throwing Metadata Mutation Wrappers

- Added non-Try metadata mutation wrappers for add, set, change, remove, clear, replace, and transform operations.
- Existing `Try...` metadata mutation APIs remain available for conditional flows.
- Throwing wrappers route through inventory-owned metadata validation when metadata belongs to an inventory item.
- Rejected wrapper mutations throw `InvalidOperationException`, remain atomic, and fire no events.

### 30. Explicit Item Registration Model

- Item definition ids are always explicit stable identities.
- Integer-like identity types remain supported through explicit ids.
- Migration behavior remains explicit-id based.
- Identity examples show explicit integer ids.

### 31. Supported Namespaced And Non-Namespaced Tag Catalog Modes

- Tag catalogs default to namespaced tag ids.
- Tag catalogs can opt into non-namespaced tag ids before any tags are defined.
- Namespaced and non-namespaced modes are mutually exclusive once selected.
- Non-namespaced tags support dot-separated hierarchy generation.
- Tag parsing is internal infrastructure and respects the catalog mode.
- `TagContainer` is hidden from normal public API.
- Item definitions expose direct tags as read-only string ids.

### 32. Locked Down Inventory-Owned Item Instances

- `ItemInstance<TKey>` is now a public readable inventory-owned stack handle.
- Direct item instance construction is no longer public.
- Direct amount mutation methods are no longer public.
- Ownership attach/detach remains internal infrastructure.
- `InventoryTransferEntry<TKey>` is produced by transfer workflows rather than manually constructed by callers.
- Inventory, transaction, transfer, split/repack, and deserialization internals still create and adjust item instances safely.

### 33. Focused Tests On Current API Behavior

- Removed broad discoverability checks that did not protect runtime behavior.
- Preserved behavior tests for current workflows.
- Preserved active extension-contract coverage for public extension interfaces.
- The test suite treats the current API as the intended public shape.

### 34. Completed Final Documentation And Code Consistency Sweep

- Reviewed XML documentation, comments, tests, examples, and tracker wording for stale pre-1.0 references.
- Rewrote documentation-planning notes to describe the current 1.0 baseline rather than removed or interim APIs.
- Removed or updated misleading comments, stale wording, and obsolete helper descriptions where behavior did not change.
- Left runtime behavior unchanged.
- No active implementation tracker points remain.

### 35. Registered Migrations To Canonical Definitions

- `ItemRegistry<TKey>.RegisterMigration(...)` now maps obsolete ids to registered replacement definitions.
- Migration targets must already be registered in the same registry.
- Detached same-id replacement definitions are rejected.
- Migration resolution returns the canonical registered definition object.
- Multiple obsolete ids can point directly to the same replacement definition.

### 36. Direct Layout Repack API

- Added `Inventory<TKey>.TryRepackLayout(out error)`.
- Added throwing wrapper `Inventory<TKey>.RepackLayout()`.
- Direct repack preserves item instances and inventory storage order.
- Direct repack compacts placement in current layout order through normal auto-placement.
- Direct repack emits full-refresh layout movement events when visible placement changes.

### 37. Final Release Sweep For System Code And Public API

- Reviewed source and public API surface for stale naming, release-blocking scaffolding, accidental implementation leakage, and inconsistent extension-boundary visibility.
- Confirmed built-in layout extension hooks remain hidden from normal IntelliSense with `EditorBrowsable(Never)` where practical.
- Corrected public null-argument diagnostics so `Inventory<TKey>`, deserialization, and registry APIs report proper parameter names.
- Corrected layout option diagnostics for null allowed definition ids.
- Added regression coverage for public argument diagnostics.
- Verified the full test suite passes after the sweep.

## Documentation Completed

These documentation/style points are completed for the current documentation slice.

### 1. Clarify The Three Levels Of Item Identity

- Item definition class.
- Registered repository definition.
- Item instance.
- README should introduce these terms early and use them consistently.

### 2. Separate Normal Usage From Extension APIs

- Normal users should mostly interact with `Inventory<TKey>`.
- Extension authors implement layouts, rules, policies, and resolvers.
- Low-level layout methods like `TryMove` and `TrySwap` should not be presented as normal user workflows unless clearly framed.

### 3. Improve Technical Tables

- Tables should explain practical meaning, not only type names.
- Layout context table should explain:
  - slot: index;
  - grid: `(x, y)`;
  - multi-cell grid: position plus footprint/anchor concerns.

### 4. Explain `FootprintDefinition` Better

- It is an example subclass of `ItemDefinition`.
- It maps footprint attributes in its constructor.
- The package does not ship it because doing so would create another base item class.

### 5. Catalog-Owned Attribute Vocabulary

- Attributes are declared through `catalog.Attributes.Define<T>("id")`.
- Attribute declarations are part of the item universe.
- Schemas and definitions refer to attributes by string ID and value type.

### 6. `AttributeKey<T>` Is Not Public API

- Users should not create static `AttributeKey<T>` fields.
- Attribute IDs can still be represented as string constants if a project wants compile-time discoverability.
- Static string constants are optional convenience, not the source of truth.

### 7. Update Schema Examples

- Preferred schema style is:

```csharp
ItemSchema<string>.Create("equipment")
    .RequireAttribute<int>("weight", inherited: true);
```

- Constructor-defined attributes should use:

```csharp
DefineAttribute("weight", weight);
```

### 8. Update Attribute Rule Examples

- Rules now use string IDs:

```csharp
new AttributePredicateRule<string, int>("weight", weight => weight <= 6)
```

### 9. Update Capacity/Layout Attribute Examples

- `WeightCapacityPolicy` and `AttributeGridFootprintProvider` use string attribute IDs, not key objects.

### 10. Clarify Manager Catalog Optionality

- The catalog parameter is optional because the manager creates a default internal catalog.
- A catalog always exists.
- Inventory creation still requires the catalog to be frozen.

### 11. Document Inventory-Owned Rule Mutation

- Runtime rule changes should be performed through `Inventory<TKey>`, not by mutating a shared `RuleContainer<TKey>` directly after inventory creation.
- Explain that default and override rule containers are cloned into inventories at creation time.
- Explain that rule changes are validated against the current inventory contents before they are committed.

### 12. Document Runtime Policy Parameter Mutation

- Runtime policy, layout, and stack changes should go through `Inventory<TKey>`.
- Built-in parameter ids should be listed.
- Explain that current contents are validated before changes commit.
- Explain that this slice preserves placements/stacks and rejects changes requiring repack or compression.
- Explain that layout parameter changes fire a full-refresh change event.

### 13. Document Repack/Compress Policy Mutation

- Explain preserve-only default behavior.
- Explain `InventoryParameterMutationOptions.RepackLayout`.
- Explain `InventoryParameterMutationOptions.CompressStacks`.
- Explain that compression preserving metadata may split stacks.
- Explain that repack uses normal layout placement rules and may fail.
- Explain that successful repack/compress changes require full UI refresh.

### 14. Document Transaction/Transfer Commit Ownership

- Builders stage and build.
- `Inventory<TKey>` commits inventory transactions.
- `InventoryTransfer` commits cross-inventory operations.
- Transfer helper methods such as `TryMoveAll` and `TryTransferMaximum` are inventory-level actions, not builder APIs.

### 15. Document Inventory-Owned Metadata Mutation

- Metadata is intentionally loose and has no catalog/schema.
- `item.Metadata.TryAdd/TrySet/TryChange/TryRemove` is the normal ergonomic API.
- Inventory-owned metadata methods validate through the owning inventory.
- Metadata on a stack applies to the whole stack.
- To mutate only part of a stack, split it first or use `ItemInstance.TrySplitAndSetMetadata`.
- Metadata changes fire `Inventory.Changed`.

### 16. Document Attribute-Driven Stacking Policies

- Explain `ConditionalMaxStackResolver<TKey>`.
- Explain `AttributeMaxStackResolver<TKey>`.
- Clarify that stack attributes are definition attributes, not metadata.
- Clarify that missing stack attributes do not fail catalog freeze by themselves.
- Explain strict runtime behavior when `AttributeMaxStackResolver` has no missing-attribute fallback.
- List supported runtime parameter ids.

### 17. Document Normal API Surface Versus Extension Contracts

- Normal users should mutate inventories through `Inventory<TKey>` and `InventoryTransfer`.
- Layout/rule/policy/resolver interfaces are extension contracts.
- Concrete layout infrastructure methods may be hidden from IntelliSense but remain callable.
- UI code can safely use layout query methods and inventory changed event payloads.
- Metadata-change UI updates should use `InventoryChangedEventArgs.MetadataChanged`.

### 18. Document The Identity Type Model Near The Top Of The README

- Explain that `TKey` is the item definition identity type used by catalogs, registries, managers, inventories, item definitions, and item instances.
- State that the documentation uses `string` identities for readability and examples.
- Clarify that other stable key types, such as `Guid`, integer-like types, enums, and custom value objects/classes, are intended to work when their equality and hash behavior is appropriate for dictionary lookup.
- Mention that `float` and `double` are tested but generally not recommended as identity choices.
- Clarify that integer-like identities are supported through explicit stable ids.
- Explain that explicit ids are the identity model because item definition ids are persistent values used by registry lookup, serialization, and migrations.

### 19. Add README Table Of Contents

- Add a table of contents near the top of the README.
- Make the document easier to scan as the feature surface has grown.

### 20. Move Tags Near Attributes Before Item Schemas

- Place the tags section immediately before or after the attributes section.
- Ensure users understand tags and attributes before reaching the item schema section.
- Adjust README flow so item schemas can reference tags and attributes without introducing both concepts for the first time there.

### 21. Document Tag Catalog Modes

- Explain that namespaced tags are the default.
- Explain how to opt into non-namespaced tags before defining tags.
- Explain that tag catalog modes are mutually exclusive.
- Explain dot hierarchy behavior for both namespaced and non-namespaced tags.
- Show examples for namespaced tags such as `core:equipment.tools.knife`.
- Show examples for non-namespaced tags such as `equipment.tools.knife`.
- Explain that direct definition tags are exposed as read-only string ids.
- Explain that all tag declaration and lookup authority lives in `TagCatalog`.

### 22. Clarify Class-Owned Schema Integration

- Explain the intended schema workflow:
  - define an `ItemDefinition<TKey>` subclass;
  - declare the schema as a static member of that class;
  - prefer `ItemSchema<TKey>.CreateFor<TDefinition>(...)`;
  - pass the schema through protected constructor chaining.
- Explain that schemas can record their owner definition type.
- Explain that catalog validation rejects owned schemas used by unrelated definition types.
- Explain that public schema-taking constructors on concrete registered definitions are rejected.
- Explain that simple definitions can still use the default schema.
- Avoid examples that pass schemas into public item definition constructors.

### 23. Document Inventory-Owned Item Instances

- Explain that item instances are created by inventories, transactions, transfers, deserialization, and internal rebuild flows.
- Explain that callers should not construct `ItemInstance<TKey>` directly.
- Explain that callers should not mutate item amounts directly.
- Show inventory-owned ways to add, remove, merge, split, transfer, and inspect item instances.
- Explain that `ItemInstance<TKey>.Metadata` remains the supported per-instance mutation surface.
- Explain that `SplitAndSetMetadata` remains an inventory-routed item instance operation.
- Explain that `InventoryTransferEntry<TKey>` objects are transfer-builder inspection results.

### 24. Rewrite Custom Definition Subclass Example

- The README example that shows subclassing `ItemDefinition<TKey>` currently includes an item schema constructor parameter.
- Rewrite the example from scratch to show the current class-owned schema pattern.
- The schema should be declared by the definition class, and the constructor should accept only normal definition data such as id, attribute values, and optional tags.
- The table directly below the example should show the current constructor surface.

### 25. Correct Item Catalog Optionality In Inventory And Manager Docs

- The README `Inventory and Managers` section documents the item catalog as required.
- The related table uses the explicit shared-catalog model.
- `InventoryManager<TKey>` requires an explicit `ItemCatalog<TKey>`, because inventories need a catalog-backed item universe.
- The runtime model and documentation no longer describe an implicit/default manager catalog.

### 26. Separate Usage Documentation From Extension Documentation

- Make the split between using the system and extending the system much more apparent in the README.
- The usage section should generally avoid references to extension-only APIs or public APIs that exist primarily for custom implementations.
- Add a small early segment explaining that the system is highly extensible.
- In that early segment, state that extension details are covered later after the main usage section.
- Keep normal usage documentation focused on the APIs application developers are expected to call directly.
- Move or reframe extension-facing interfaces, hooks, and implementation contracts into a dedicated extension section.

### 27. Vary README Information Structures

- The README currently uses a lot of tables.
- Tables are useful, but the document should use a more varied selection of structural formats.
- Consider replacing some tables with short examples, bullet summaries, flow-style lists, decision notes, or compact callout sections where those would read better.
- Keep tables where comparison or matrix-style information is genuinely useful.

### 28. Document Registered Definition Invariant

- Explain that inventories can only contain definitions registered in their catalog registry.
- Explain that callers should use the canonical registered definition object.
- Explain that detached same-id definitions are rejected.
- Show registering all definitions before catalog freeze.
- Clarify that deserialization resolves definitions through the registry.
- Clarify that transfers cannot introduce definitions outside the target inventory catalog.

### 29. Write README As The 1.0 Baseline

- Present the current code as the only existing iteration of the package.
- Avoid changelog-style wording in the README.
- Document only the current API as the intended model.
- Omit retired or experimental mechanics from normal usage documentation.
- Save historical migration notes, compatibility notes, and removed-API explanations for after the 1.0 release.

### 30. Document Multiplied Attribute Stack Resolver

- Explain `MultipliedAttributeStackResolver<TKey>`.
- Clarify that it reads integer definition attributes, not metadata.
- Explain that the multiplier is inventory-specific resolver state.
- Explain flooring and minimum-one behavior.
- List supported runtime parameter ids.
- Show a ratio-style example where the same item universe stacks differently in different inventories.

### 31. Document Definition Id Migrations

- Explain that migrations map obsolete definition ids to canonical registered replacement definitions.
- Show `registry.RegisterMigration(oldId, replacementDefinition)`.
- Explain that the replacement definition must already be registered in the same registry.
- Explain that detached same-id replacement definitions are rejected.
- Show multiple old ids migrating to the same replacement definition.
- Clarify that deserialization resolves definition ids through registry migrations.

### 32. Separate Inventory Transaction And Builder API Tables

- Split the current table that mixes `InventoryTransaction<TKey>` APIs and `InventoryTransactionBuilder<TKey>` APIs.
- Use one table for transaction objects and one table for transaction builders.
- Make the preferred workflow clear in surrounding text.

### 33. Document Transaction Builder Commit Workflow

- Explain that transaction builders stage structural changes.
- Show `Inventory<TKey>.TryCommitTransaction(builder, ...)` as the normal commit workflow.
- Explain when `Build()` and `TryBuild(...)` are useful.
- Clarify that move and swap are inventory-level layout operations, not transaction-builder operations.

### 34. Document Transfer Builder Commit Workflow

- Explain that transfer builders stage outgoing source removals.
- Show `source.TryCommitTransfer(transfer, target, ...)` as the builder commit workflow.
- Explain that `InventoryTransfer.From(source)` only creates a transfer builder.
- Explain that one-shot transfer actions live on the source inventory.
- Clarify the difference between transaction builders, transfer builders, and source-owned one-shot transfer methods.

### 35. Document Source-Owned Cross-Inventory Transfer APIs

- Show `TryTransferTo`, `TryMoveAllTo`, `TryMoveWhereTo`, and tag-based transfer helpers.
- Show `TryTransferMaximumTo` and maximum move helpers.
- Show `TrySwapItemsWithInventory` and `TrySwapWithInventory`.
- Explain source/target context parameters for cross-inventory swaps.

### 36. Document Definition-Based Layout Restrictions

- Explain tag-based and definition-based restrictions for equipment slots.
- Explain tag-based and definition-based restrictions for sectioned layout sections.
- Clarify that definition restrictions compare item definition ids.
- Clarify that tag and definition restrictions are combined with OR semantics.
- Show examples for tag-only, definition-only, mixed, and unrestricted slots/sections.

### 37. Add Sorting Usage Examples

- Add README examples showing how sorting is used.
- Include at least one sorting example for `MultiCellGridLayout<TKey>`, because that layout differs significantly from simpler layouts.
- Explain any layout-specific behavior that affects sort results, placement, or repacking.

### 38. Reorder Runtime Mutation Sections

- Reorder the README sections so runtime parameter mutation appears directly after rule mutation.
- Rule mutation should appear directly after the general rules section.
- The surrounding feature flow should be:
  - stack resolver;
  - layout;
  - capacity policy;
  - rules;
  - rule mutation;
  - stack resolver / capacity policy / layout parameter mutation.
- This should make the runtime mutation sections read as follow-up workflows after the base systems are introduced.

### 39. Document Stack Parameter Mutation Actions

- Explain preserve-only stack resolver parameter mutation.
- Explain `InventoryParameterMutationActions` flags.
- Explain that stack resolver parameter changes can use split, compression, and repack actions.
- Explain that layout parameter changes can use repack only.
- Explain that capacity policy parameter changes are validation-only.
- Explain layout repack as an explicit action.
- Explain oversized-stack splitting for max-stack reductions.
- Explain compatible-stack compression for max-stack increases.
- Clarify that split and compression do not force layout repack.
- Document `SplitOversizedStacks`, `CompressCompatibleStacks`, `CompactCompatibleStacks`, and the `CompressStacks` alias.
- Show examples for no action, repack only, split only, compression only, and combined stack resolver actions.
- Clarify which action combinations require full UI refresh.
- IMPORTANT: Document string-id semantics like SectionLayout's "parameterId: "section:bag.slotCount"". As these are strings, there is no intellisense helping the user write the right thing

### 40. Move Or Split Pitfalls And Caveats

- Pitfalls and caveats are currently in the extension section, but they should not live only there.
- If some caveats require extension context to understand, split the content into two lists:
  - usage pitfalls and caveats;
  - extension pitfalls and caveats.
- Place usage-focused caveats in the usage section.
- Place extension-specific caveats in the extension section.

### 41. Document Metadata Mutation APIs

- Explain detached versus inventory-owned metadata mutation.
- Show the `Try...` APIs for conditional metadata mutation flows.
- Show throwing wrappers for normal expected-success flows.
- Explain that inventory-owned metadata mutations validate through the inventory.
- Explain that accepted direct metadata mutations emit `MetadataChanged`.
- Explain that split-and-set metadata emits added/modified payloads rather than `MetadataChanged`.

### 42. Rewrite Usage Pitfalls And Caveats For The Current README

- Shorten the usage caveat list now that the main usage sections explain more of the model directly.
- Remove caveats that are only leftovers from earlier API shapes, such as warning users away from `AttributeKey<T>` when normal public attribute authoring is already string-id based.
- Keep caveats that still prevent likely misuse, such as storage order versus UI order, metadata applying to whole stacks, partial-stack metadata requiring split, source-owned transfer placement limits, runtime parameter action limits, and persistence compatibility.
- Group remaining caveats by practical concern instead of one long mixed list.
- Avoid repeating details already covered clearly in the relevant usage sections.

### 43. Audit Extension And Reference Sections For Stale Public API Claims

- Review the extension section, feature map, and public API quick reference against current source visibility.
- Remove or reframe references to internal infrastructure types that should not be presented as normal public API.
- Verify tag and attribute rows reflect the current string-id public model.
- Ensure runtime mutation references use `InventoryParameterMutationActions` directly and do not mention removed option presets or aliases.
- Ensure transfer and transaction reference wording reflects source-owned transfers and inventory-owned transaction commits.

### 44. Define The Extension Section Reader Flow

- Add an opening extension roadmap that explains what extension authors can customize and what invariants they must preserve.
- Keep extension documentation clearly separate from normal application usage.
- Recommended flow:
  - custom definition classes and schema ownership;
  - custom stack resolvers;
  - custom capacity policies;
  - custom rules;
  - parameterized components;
  - custom layouts and layout contexts;
  - custom layout persistence, sorting, cloning, and events;
  - extension pitfalls.
- Cross-reference usage sections instead of re-explaining normal workflows in the extension section.

### 45. Document Custom Stack Resolver Implementations

- Explain when to implement `IStackResolver<TKey>` instead of composing built-in resolvers.
- Document expected behavior for max-stack resolution, strict failures, metadata-aware stack compatibility boundaries, and error messages.
- Show a compact custom resolver example that uses definition attributes through string ids.
- Explain how custom resolvers interact with add/merge behavior and runtime stack mutation.
- Include guidance for deterministic behavior because stack resolution affects transactions, transfers, deserialization, and rebuild flows.

### 46. Document Parameterized Stack Resolver Implementations

- Expand `IParameterizedStackResolver<TKey>` documentation beyond the current contract table.
- Explain `InventoryParameterDefinition` ids, value types, descriptions, and stable string-id expectations.
- Show a custom resolver that creates replacement resolver instances from parameter changes.
- Explain why parameterized components should not mutate shared component instances in place.
- Document which mutation actions the inventory may apply around stack resolver parameter changes.

### 47. Document Custom Capacity Policy Implementations

- Improve the custom capacity policy section so it explains `CanAdd(...)` versus `CanApply(...)`.
- Explain why `CanApply(...)` receives `NormalizedInventoryTransaction<TKey>` and should be preferred for real transaction validation.
- Show how to account for added and removed normalized amounts without trusting storage order.
- Explain capacity policy behavior during transfers, transactions, metadata mutation, deserialization restore, and runtime parameter changes.
- Replace the current coin-only capacity example with an example that limits inventory-level capacity rather than a specific item family; item-specific limits are better represented as rules.
- Add a parameterized capacity policy cross-reference to the parameterized component section.

### 48. Document Custom Rule Implementations

- Explain the difference between `IRulePolicy<TKey>`, `IInventoryStructuralRulePolicy<TKey>`, and `InventorySnapshotRulePolicy<TKey>`.
- Clarify semantic transaction checks, structural storage/index checks, and final snapshot checks.
- Show when to wrap rules with `IdentifiedRulePolicy<TKey>` or `IdentifiedSnapshotRulePolicy<TKey>`.
- Explain how custom rules participate in inventory-owned rule mutation validation.
- Include examples for metadata rules, tag/definition rules, and whole-inventory constraints without duplicating all built-in rule docs.

### 49. Expand Custom Layout Authoring Into A Real Implementation Guide

- Turn the current custom layout sketch into a clearer staged guide.
- Cover layout-owned state, storage-index mappings, addressable contexts, direct contexts, mapped contexts, merge candidates, placement validation, move/swap, sorting, persistence, and cloning.
- Explain how layouts validate proposed placements against simulated removals, deltas, and additions.
- Explain how custom layouts should keep mappings accurate before and after inventory mutations.
- Clarify that layout methods are extension contracts even when public, while normal application code should prefer inventory-level APIs.

### 50. Document Custom Layout Context Design

- Explain direct versus mapped layout contexts in extension-author terms.
- Document how mapped contexts target `InventoryTransaction<TKey>.Added` indices and transfer-builder entry order where relevant.
- Show a compact context type with single-position and mapped-position forms.
- Explain `IsMapped` as the shared orchestration signal.
- Include guidance for context validation and error messages.

### 51. Document Custom Layout Persistence, Cloning, Sorting, And Events

- Explain `ILayoutPersistentData` responsibilities for custom layouts.
- Document restore compatibility expectations and failure behavior for incompatible saved layout data.
- Explain why `Clone()` must deep-copy mutable placement state for simulations and inventory creation.
- Explain how custom layouts report affected contexts through mapping methods used by event payloads.
- Document sorting responsibilities, including unsupported sorting, item-comparer sorting, and layout-specific sort contexts.

### 52. Document Custom Grid Footprint Providers

- Explain when to implement `IGridFootprintProvider<TKey>` instead of using `AttributeGridFootprintProvider<TKey>`.
- Document deterministic footprint resolution, validation failures, anchors, and how footprint data affects placement and sorting.
- Show a small provider example or cross-reference the multi-cell layout usage section when the built-in attribute provider is enough.

### 53. Make Extension Examples Consistent And Deliberately Scoped

- Decide which extension examples should be compilable snippets and which should be conceptual skeletons.
- Add setup context where needed so examples do not rely on undefined definitions, catalogs, or imports.
- Keep examples short enough for README readability while still showing the invariants that matter.
- Prefer one strong example per extension point over many partial examples.
- Ensure examples use current API names, direct action flags, explicit catalogs, registered definitions, and class-owned schemas.

### 54. Rewrite Extension Pitfalls And Caveats After Extension Docs Are Expanded

- Replace the current short caveat list with extension-specific warnings tied to the completed extension sections.
- Keep usage caveats out of the extension caveat list.
- Include layout simulation/cloning/state risks, parameter id stability, replacement-instance parameter mutation, rule validation phase selection, custom resolver determinism, and persistence compatibility.
- Keep this section concise and avoid repeating full implementation guidance.

### 55. Final Release Sweep For XML Docs And README

- Reviewed README release-readiness wording, examples, terminology, quick-reference content, and next-step guidance against the current API.
- Updated installation and release next-step wording so the README no longer describes README review as pending work.
- Corrected transfer-builder wording in README and XML documentation.
- Verified targeted stale-wording scans are clean for removed planning terms and cross-package context.
- Verified `dotnet build` completes with zero warnings and `dotnet test` passes.

## Maintenance Rules

1. Update This File After Every Completed Slice
2. Move Completed Implementation Points To `Completed`
3. Keep README/Style Work In `Documentation`
4. Keep Deferred Implementation Ideas In `To-do`
5. Prefer Updating This File Instead Of Restating The Entire Point List In Chat
6. Normalize numbering when moving / adding / removing elements from any of the lists
7. Ensure numbering for all elements on the list
