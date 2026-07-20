# Changelog

This file records notable changes to `Workes.InventorySystem`.

## [2.0.0] - Unreleased

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

### Added

- Added portable, non-generic `InventorySnapshot` capture through `CaptureSnapshot()` and `TryCaptureSnapshot(...)`,
  including deeply detached entries, item metadata, inventory metadata, and complete built-in layout state.
- Added `InventoryMetadata`, inventory-owned validation and reconciliation, `InventoryMetadataChanged`, and portable
  root-metadata restoration across exact, reconciliation, and salvage workflows.
- Added snapshot validation and application workflows: `AssessSnapshot(...)`, exact `RestoreSnapshot(...)`, lossless
  `ReconcileSnapshot(...)`, deterministic `SalvageSnapshot(...)`, and their conditional `Try...` forms. Application is
  atomic and returns structured assessment, result, issue, and item-loss information.
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
- Added inventory-owned layout query wrappers such as `GetLayoutPositionCount()`,
  `GetAddressableLayoutContexts()`, `GetItemAt(context)`, `GetLayoutContextsForStorageIndex(...)`, and
  `GetLayoutContextsForItem(...)` so application code no longer needs to pass an inventory back into its own layout for
  ordinary reads.
- Added `ItemCatalog<TKey>(bool areTagsNamespaced)` and `TagCatalog(bool areTagsNamespaced)` constructors for explicit
  namespaced or non-namespaced tag-mode selection. Parameterless construction retains the existing compatibility
  workflow.

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

### Deprecated

- Deprecated `Inventory<TKey>.Serialize()`, `Inventory<TKey>.Deserialize(...)`, `SerializedInventory<TKey>`, and
  `SerializedItem<TKey>`. Use portable inventory snapshots for new persistence code.
- Deprecated `ItemMoved<TKey>.IsSortResult`. Use `Cause == ItemMovementCause.Sort`.

### Fixed

- Parameterized layout repacks no longer replace item identities or reorder inventory storage.
- Custom mapped transactions can replace added-entry contexts without losing amount deltas, removals, or item
  instances.
- Reflow, sort, repack, direct-move, and swap events now report affected movement and contexts consistently.
- `InstanceMetadata.TryTransform(...)` now reports rejected transformation errors through its conditional result
  instead of leaking an `InvalidOperationException`.

### Documentation

- Replaced the monolithic README manual with a concise landing page, a beginner-first quick start, focused guides for
  each major subsystem, and a complete extension-authoring guide.

### Internal

- Consolidated candidate validation under inventory-owned simulation and added extensive regression coverage for
  snapshots, restoration modes, repacking, layout reflow, events, and custom extension contracts.

## [1.0.1]

### Fixed

- Corrected the packaged README and repository commit pointer.

[2.0.0]: https://github.com/WorkesLibraries/Workes.InventorySystem/compare/6d6a449...main
[1.0.1]: https://github.com/WorkesLibraries/Workes.InventorySystem/commit/6d6a449
