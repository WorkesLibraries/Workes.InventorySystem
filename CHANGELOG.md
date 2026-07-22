# Changelog

This file records notable changes to `Workes.InventorySystem`.

## [3.0.0] - Unreleased

### Breaking Changes

- Removed the public `Inventory<TKey>.TryFormulateFromNormalized(...)` authoring API. `NormalizedInventoryTransaction<TKey>`
  remains extension-facing validation data for rules, capacity policies, layouts, and internal validation. User-authored
  changes should use direct inventory operations or the transaction/delta APIs.

### Added

- Added `InventoryItemDelta<TKey>` as a reusable, context-free semantic net-change model with add/remove operations,
  metadata-aware remove modes, unique operation labels, mirroring, and semantic prefixed combination.

## [2.0.0] - 2026-07-21

### Breaking Changes

- Custom `IInventoryLayout<TKey>` implementations must now expose a complete `SnapshotCodec`. The codec must capture
  portable layout state, decode and validate its persisted representation, and reconstruct exact placement.
- `InstanceMetadata` now rejects values outside the portable snapshot value model. Metadata must consist of null,
  supported scalar values, one-dimensional arrays, or `List<T>` values, recursively; custom objects, dictionaries, enums,
  multidimensional arrays, and arbitrary enumerable types are rejected.
- Removed `Inventory<TKey>.Attributes` and replaced it with inventory-owned `Metadata`. Definition attributes on
  catalogs and definitions are unchanged.
- `AttributeCatalog.Define<T>(id)` now rejects every duplicate ID declaration, including attempts using the same value
  type, instead of returning the existing declaration.
- `InventoryChangedEventArgs<TKey>.RequiresFullRefresh` now means that the event payload and affected contexts do not
  fully represent the observable change. Sorts, repacks, and layout reflows no longer request a full refresh merely
  because they move many items; UI integrations should process their movement and affected-context payloads.
- Layout repacking is now capability-based. `EntryLayout<TKey>` no longer supports a guaranteed no-op repack, and
  `EquipmentLayout<TKey>` no longer permits semantically meaningful equipment positions to be reassigned by repacking.
  Custom layouts must implement `IRepackableInventoryLayout<TKey>` to opt in.
- Public expected-failure contracts now return `out InventoryFailure? failure` instead of `out string? error`. Consumer
  logic should branch on `failure.Kind` or stable `failure.Code` and use `failure.Message` only for display.
- Removed the temporary public string-to-`InventoryFailure` conversion; extension authors must create structured
  failures explicitly.

### Added

- Added portable, non-generic `InventorySnapshot` capture through `CaptureSnapshot()` and `TryCaptureSnapshot(...)`,
  including deeply detached entries, item metadata, inventory metadata, and complete built-in layout state.
- Added `InventoryMetadata`, inventory-owned validation and reconciliation, `InventoryMetadataChanged`, and portable
  root-metadata restoration across exact, reconciliation, and salvage workflows.
- Added snapshot validation and application workflows: `AssessSnapshot(...)`, exact `RestoreSnapshot(...)`, lossless
  `ReconcileSnapshot(...)`, deterministic `SalvageSnapshot(...)`, and their conditional `Try...` forms. Application is
  atomic and returns structured assessment, result, issue, and item-loss information.
- Added `InventorySnapshotBuilder` through `InventorySnapshot.ToBuilder()` for detached save migrations before
  assessment or application, including definition-ID, amount, metadata, entry-order, entry-removal, and layout-reset
  edits.
- Added serializer-friendly snapshot DTOs, built-in codecs for supported key and value types,
  `InventorySnapshotKeyCodecAttribute`, `IInventorySnapshotKeyCodec<TKey>`, and
  `IInventoryLayoutSnapshotCodec<TKey>` for custom persistence contracts.
- Added `IRepackableInventoryLayout<TKey>` and `IParameterizedRepackableInventoryLayout<TKey>` so layouts explicitly
  opt into inventory-owned repacking and parameterized repacking.
- Added `IInventoryLayoutReconciler<TKey>` and `InventoryLayoutReconciliationResult<TKey>` for layouts that reposition
  surviving items after inventory mutations.
- Added `ItemMovementCause`, `ItemMoved<TKey>.Cause`, and `ItemMoved<TKey>.IsAutomatic` to distinguish explicit moves,
  sorting, repacking, and collateral layout reflow. Added `InventoryChangeOrigin` to identify ordinary operations and
  each snapshot-application workflow.
- Added `InventoryTransaction<TKey>.WithAddedEntryContexts(...)` so custom layouts can map contexts for added entries
  without replacing transaction structure.
- Added target binding to `InventoryTransferBuilder<TKey>` through `InventoryTransfer.From(source).To(target)`,
  including per-removal direct target contexts, staged target validation, and source-safe commit revalidation.
- Added `GetMaxStackSize(...)` and `TryGetMaxStackSize(...)` overloads for querying an inventory's current stack limit
  by registered definition, definition ID, metadata-aware prototype, or existing item instance.
- Added inventory-owned layout query wrappers such as `GetLayoutPositionCount()`,
  `GetAddressableLayoutContexts()`, `GetItemAt(context)`, `GetLayoutContextsForStorageIndex(...)`, and
  `GetLayoutContextsForItem(...)` so application code no longer needs to pass an inventory back into its own layout for
  ordinary reads.
- Added migration-aware definition-ID overloads for common inventory, transaction-builder, and transfer-builder
  workflows, including add, count, contains, find, and remove-by-definition operations.
- Added rule configuration change events for inventory-owned rule add/replace/remove/enable/priority mutations,
  including typed rule-change details and immutable before/after rule-state snapshots.
- Added `ItemCatalog<TKey>(bool areTagsNamespaced)` and `TagCatalog(bool areTagsNamespaced)` constructors for explicit
  namespaced or non-namespaced tag-mode selection. Parameterless construction retains the existing compatibility
  workflow.
- Added `InventoryFailure`, `InventoryFailureKind`, `InventoryFailureCodes`, `InventorySystemException`, and
  `InventoryOperationException` for structured expected-failure reporting and project-owned expected-success
  exceptions.

### Changed

- Repacking is layout-agnostic and inventory-owned: layouts create empty configured candidates while the inventory
  controls item ordering, placement simulation, validation, atomic commit, and events.
- Layout parameter repacks preserve the original item instances and `Inventory.Items` storage order while rebuilding
  placement under the new configuration.
- Inventory mutations can now reconcile layout-owned presentation state and report every surviving item whose context
  changed in the same coherent event.
- Snapshot application replaces contents and layout state atomically and emits one final event only after the committed
  state is visible.
- Metadata arrays and lists now use value-snapshot ownership at every input, candidate, event, persistence, and read
  boundary. `InstanceMetadata` also supports typed `Update<T>(...)` and `TryUpdate<T>(...)`.
- `InventoryConfigurationChanged<TKey>` now uses `ConfigurationId` for both component parameter IDs and rule IDs.
- Throwing operation wrappers now throw project-owned inventory exceptions carrying the same structured
  `InventoryFailure` returned by the corresponding `Try...` method. Programmer misuse still uses standard argument or
  state exceptions.

### Deprecated

- Deprecated `Inventory<TKey>.Serialize()`, `Inventory<TKey>.Deserialize(...)`, `SerializedInventory<TKey>`, and
  `SerializedItem<TKey>`. Use portable inventory snapshots for new persistence code.
- Deprecated `ItemMoved<TKey>.IsSortResult`. Use `Cause == ItemMovementCause.Sort`.
- Deprecated `InventoryConfigurationChanged<TKey>.ParameterId`. Use `ConfigurationId`.

### Fixed

- Parameterized layout repacks no longer replace item identities or reorder inventory storage.
- Custom mapped transactions can replace added-entry contexts without losing amount deltas, removals, or item
  instances.
- Reflow, sort, repack, direct-move, and swap events now report affected movement and contexts consistently.
- `InstanceMetadata.TryTransform(...)` now reports rejected transformation errors through its conditional result
  instead of leaking an `InventoryOperationException`.

### Documentation

- Replaced the monolithic README manual with a concise landing page, a beginner-first quick start, focused guides for
  each major subsystem, and a complete extension-authoring guide.
- Added a dedicated failure-handling guide covering `InventoryFailure`, failure categories and codes, project-owned
  exceptions, migration from string errors, and extension-authored failures.
- Clarified how extension authors should use custom namespaced failure codes, fixed failure categories, component/source
  metadata, nested causes, and project-owned exceptions.

### Internal

- Consolidated candidate validation under inventory-owned simulation and added extensive regression coverage for
  snapshots, restoration modes, repacking, layout reflow, events, and custom extension contracts.

## [1.0.1] - 2026-06-25

### Fixed

- Corrected the packaged README and repository commit pointer.

[2.0.0]: https://github.com/WorkesLibraries/Workes.InventorySystem/compare/6d6a449...main
[1.0.1]: https://github.com/WorkesLibraries/Workes.InventorySystem/commit/6d6a449
