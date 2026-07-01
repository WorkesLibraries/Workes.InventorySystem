# Persistence

`Workes.InventorySystem` can capture and restore an inventory object graph. It does not choose a file format, database,
serializer, save location, or application-level versioning strategy.

The boundary is:

```text
Inventory.Serialize()
  -> SerializedInventory<TKey> object graph
  -> application serializer and storage

application serializer and storage
  -> SerializedInventory<TKey> object graph
  -> Inventory.Deserialize(...)
```

The package owns inventory semantics and restore validation. The application owns conversion between the DTO object
graph and bytes, text, documents, database rows, or network messages.

Read [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md) for definition identity and migrations,
[Layouts](LAYOUTS.md) for placement models, and
[Events And UI Integration](EVENTS_AND_UI.md) for restore-time view synchronization.

## Capture A Snapshot

```csharp
SerializedInventory<string> snapshot =
    inventory.Serialize();
```

`Serialize()` captures:

- item instances in `Inventory.Items` storage order.
- each definition's stable ID.
- each stack amount.
- each stack's metadata dictionary.
- layout-specific persistent data.

The returned DTOs are mutable application-facing data objects. Changing them does not mutate the live inventory.
Collection containers are copied, but object values stored inside metadata are not recursively deep-cloned.

## Persistence DTOs

`SerializedInventory<TKey>` contains:

| Member | Meaning |
|---|---|
| `Items` | Serialized item entries in inventory storage order. |
| `LayoutData` | Concrete layout-owned persistent data, exposed as `object`. |

Each `SerializedItem<TKey>` contains:

| Member | Meaning |
|---|---|
| `DefinitionId` | Stable ID resolved through the target catalog during restore. |
| `Amount` | Saved stack amount. |
| `Metadata` | Per-instance key/object values. |

The DTOs do not contain:

- an `InventoryManager<TKey>`.
- catalog definitions, schemas, tags, or attributes.
- stack resolver, capacity policy, or rule configuration.
- the active layout type.
- a general save-format version.
- item-instance `InstanceId` values.
- serializer-specific type discriminators.

Restored item instances receive new runtime identity.

## The Package Does Not Provide A Serializer

`[Serializable]` marks the DTO classes as serializable object models; it does not select or configure a serializer.

Applications must decide how to represent:

- `TKey`.
- metadata values stored as `object`.
- the concrete type held in `LayoutData`.
- application save versions and migrations.

This matters especially for JSON serializers. An `object` property may deserialize as an untyped JSON representation
rather than the original CLR type unless the application supplies converters, discriminators, or an explicit envelope.

A practical application save envelope might contain:

```text
Save format version
Inventory kind
Layout kind
Serialized item data
Typed layout data
Application-specific configuration version
```

The envelope can convert to and from `SerializedInventory<TKey>` at the package boundary.

Do not assume that serializing `SerializedInventory<TKey>` with default serializer settings is sufficient for
polymorphic layout data or arbitrary metadata value types.

## Recreate Configuration Before Restore

`Deserialize(...)` restores into an existing inventory. The application must first recreate:

1. the item catalog.
2. all current canonical definitions.
3. any obsolete-ID migrations.
4. the frozen catalog.
5. the manager's policies and rules.
6. a compatible layout.
7. the target inventory.

```csharp
var catalog = new ItemCatalog<string>();

var apple =
    new ItemDefinition<string>("apple");

catalog.Registry.Register(apple);
catalog.Freeze();

var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(20),
    new UnlimitedCapacityPolicy<string>(),
    new SlotLayout<string>(12),
    catalog);

var restoredInventory =
    manager.CreateInventory();

restoredInventory.Deserialize(
    snapshot,
    strict: true);
```

The snapshot does not replace the target inventory's configuration. Current policies, rules, stacking, and layout
behavior participate in restoration.

## Definition Resolution

Every saved definition ID is resolved through:

```csharp
Manager.Registry.Resolve(serializedItem.DefinitionId)
```

The resolved object is the target catalog's canonical registered definition. Saved object references are never reused.

This allows save data to survive definition-ID changes through registry migrations:

```csharp
var healthPotion =
    new ItemDefinition<string>("potion.health");

catalog.Registry.Register(healthPotion);

catalog.Registry.RegisterMigration(
    oldId: "health_potion",
    replacementDefinition: healthPotion);

catalog.Freeze();
```

A saved `"health_potion"` entry now resolves to the canonical `"potion.health"` definition.

Register migrations before catalog freeze. The replacement must already be registered in the same catalog.

An unknown ID with no migration causes `Registry.Resolve(...)` to throw. This happens regardless of the
`Deserialize(..., strict)` value.

## Restore Flow

The current implementation restores in this order:

```text
Clear the target inventory
  -> resolve each saved definition ID
  -> clone saved metadata
  -> stage each item through normal inventory add behavior
  -> commit the staged item transaction
  -> restore layout-specific persistent data
```

Consequences:

- existing target contents are cleared before saved data is fully validated.
- current stack resolution, capacity, rules, and layout validation apply.
- item additions may split or merge according to current behavior.
- layout placement is restored after item contents commit.
- restore can emit more than one inventory change event.
- layout-data restoration itself does not emit a final change event.

`Deserialize(...)` is therefore a restore operation, not an atomic replacement transaction.

## Strict And Non-Strict Restore

The `strict` argument controls failures returned while staging individual saved items:

| Mode | Item rejected by current inventory validation |
|---|---|
| `strict: true` | Throws `InvalidOperationException` at the first rejected item. |
| `strict: false` | Skips the rejected item and continues. |

It does not suppress every restore failure:

- unknown definition IDs still throw during registry resolution.
- incompatible layout data still throws.
- malformed application-deserialized DTO data can still fail.
- final transaction or layout restoration can still throw.

Non-strict restore requires special care because layout maps refer to saved storage indices. If an item is skipped,
remaining layout data is not remapped automatically. A layout map can therefore become incompatible with the restored
item list.

Use `strict: true` when exact item and layout restoration matters. Treat non-strict mode as a recovery tool whose result
must be inspected and whose layout may need an application-defined fallback.

## Restore Into A Fresh Candidate

Because restore is not atomic, the safest application pattern is to restore into a newly created inventory:

```csharp
Inventory<string> RestoreInventory(
    InventoryManager<string> manager,
    SerializedInventory<string> snapshot)
{
    var candidate =
        manager.CreateInventory();

    candidate.Deserialize(
        snapshot,
        strict: true);

    return candidate;
}
```

Only replace the application's active inventory reference after the candidate succeeds:

```csharp
var candidate =
    RestoreInventory(manager, snapshot);

activeInventory = candidate;
RebuildInventoryView();
```

If restore fails, discard the candidate and retain the previous active inventory.

This pattern does not make invalid save data valid; it contains failure so existing runtime state is not destroyed.

## Stack Shape Compatibility

Serialized entries describe amounts and metadata, but restoration uses normal add behavior rather than directly
reconstructing internal item instances.

Current stack resolution can therefore:

- split a saved amount into several stacks.
- merge compatible saved entries.
- reject definition data needed by an attribute-driven resolver.

Layout persistent data stores inventory storage indices. Exact layout restoration therefore assumes that restored item
entry order and count remain compatible with the saved layout map.

For reliable round trips, recreate the same relevant stack configuration and preserve metadata types and values. When
changing stack behavior between save versions, use an application migration step rather than assuming old layout maps
remain valid.

## Current Policies And Rules Apply

Saved contents are validated against the target inventory's current:

- registered canonical definitions.
- stack resolver.
- capacity policy.
- rules.
- layout placement behavior.

A save accepted by an older application version can be rejected by a newer version with stricter capacity, rules, or
layout configuration.

Version configuration changes deliberately. When compatibility cannot be preserved directly, migrate the application
save DTO before calling `Deserialize(...)`.

## Layout Persistent Data

Every layout implements:

```csharp
ILayoutPersistentData GetPersistentData();

void RestorePersistentData(
    ILayoutPersistentData? persistentData);
```

`Inventory.Serialize()` places the returned concrete object in `SerializedInventory<TKey>.LayoutData`.
`Inventory.Deserialize(...)` passes it to the active layout after item restoration.

Built-in data types:

| Layout | Persistent data | Important compatibility information |
|---|---|---|
| `EntryLayout<TKey>` | `EntryLayoutPersistentData` | Storage-index presentation order |
| `SlotLayout<TKey>` | `SlotLayoutPersistentData` | Slot-to-storage-index map |
| `GridLayout<TKey>` | `GridLayoutPersistentData` | Width, height, placement order, and cell map |
| `MultiCellGridLayout<TKey>` | `MultiCellGridLayoutPersistentData` | Width, height, placement order, default anchor, and cell map |
| `EquipmentLayout<TKey>` | `EquipmentLayoutPersistentData` | Slot IDs and slot map |
| `SectionedLayout<TKey>` | `SectionedLayoutPersistentData` | Section IDs, section sizes, and flattened slot map |

Persistent maps refer to `SerializedInventory.Items` storage indices. Item data and layout data must be stored,
migrated, and restored as one coordinated snapshot.

## Layout Configuration Is Not Fully Stored

Layout persistent data captures placement state and enough shape data for built-in compatibility checks. It does not
necessarily contain every object needed to construct the layout.

Examples:

- equipment slot restrictions come from the target `EquipmentLayout<TKey>` configuration.
- section restrictions come from the target `SectionedLayout<TKey>` definitions.
- a multi-cell footprint provider must be recreated by the application.
- definition attributes used for footprints must exist in the current catalog.

Construct the intended layout first, then restore matching persistent data into it.

Built-in grid, multi-cell, equipment, and sectioned layouts reject mismatched shape information with
`InvalidOperationException`. Entry and slot layouts have simpler compatibility behavior, so applications should still
validate their own save version and intended inventory kind before restore.

## Metadata Persistence

Each serialized item receives a copied `Dictionary<string, object>`.

The application serializer must preserve the runtime types needed by later calls such as:

```csharp
item.Metadata.TryGet<int>(
    "durability",
    out var durability);
```

If an integer is restored as another numeric type or as an untyped JSON value, typed metadata reads and rules can
behave differently.

Metadata dictionary containers are copied during capture and restore. Nested mutable objects stored as values are not
deep-cloned by the package.

Prefer serializer-friendly, stable metadata values. For complex domain objects, use an application DTO or converter
rather than relying on arbitrary runtime object serialization.

## Application-Level Versioning

The package DTO has no built-in schema-version member. Wrap it in an application save type:

```csharp
public sealed class PlayerInventorySave
{
    public int Version { get; set; }

    public SerializedInventory<string> Inventory
    {
        get;
        set;
    } = new();
}
```

An application migration can:

- rename metadata keys.
- convert metadata value types.
- remap inventory kinds or layouts.
- adjust stack entries for new stack rules.
- replace or discard incompatible layout data.
- prepare obsolete definition IDs for registry migration.

Registry migrations solve definition identity changes only. They do not migrate metadata, policies, stack shape, layout
configuration, or the application save format.

## Events And UI Refresh

`Deserialize(...)` may:

1. emit a clear event for previous contents.
2. emit a transaction event for restored contents.
3. restore layout data without emitting another event.

The final placement can therefore differ from the placement visible during the content event.

Rebuild the view after restore returns:

```csharp
inventory.Deserialize(
    snapshot,
    strict: true);

RebuildInventoryView();
```

When restoring into a fresh candidate, subscribe UI listeners only after successful restoration or ignore its
intermediate events.

## Failure Behavior

| Failure | Current result |
|---|---|
| `data` is `null` | `ArgumentNullException` before clearing |
| Unknown definition ID | `InvalidOperationException` after clearing |
| Strict item-validation failure | `InvalidOperationException` after clearing |
| Non-strict item-validation failure | Item is skipped |
| Aggregate commit failure | Exception after clearing |
| Wrong or incompatible layout data | `InvalidOperationException` after item commit |

A failed restore can leave the target empty or partially replaced. Layout restoration failure can leave restored
contents using their temporary automatic placement.

For recoverable loading, use a fresh candidate inventory and retain the prior active instance until success.

## Custom Layout Persistence

A custom layout owns its persistent-data type and compatibility checks.

Its `GetPersistentData()` implementation should:

- return plain serializer-friendly values where practical.
- copy mutable collections.
- include enough shape identity to detect incompatible restore targets.
- store placement using inventory storage indices.

Its `RestorePersistentData(...)` implementation should:

- accept only its own persistent-data type.
- validate dimensions, IDs, counts, order modes, anchors, or other defining configuration.
- validate collection sizes and stored index assumptions.
- validate before replacing live layout state.
- copy mutable collections rather than retaining caller-owned lists.
- reject incompatible data clearly.

Custom layout state must also be supported by the application's external serializer and type-discriminator strategy.

The extension guide covers the complete custom-layout contract.

## Common Mistakes

- Treating `SerializedInventory<TKey>` as a complete application save format.
- Assuming the package writes JSON, files, database records, or network messages.
- Forgetting to recreate and freeze the catalog before restore.
- Reconstructing definitions from IDs instead of registering canonical definitions.
- Expecting `strict: false` to suppress missing-definition or layout failures.
- Restoring directly into valuable live state without accounting for non-atomic failure.
- Changing stack configuration while expecting saved storage-index maps to remain valid.
- Persisting item data separately from its layout map.
- Assuming layout persistent data constructs the target layout.
- Expecting registry migrations to migrate metadata or layouts.
- Losing CLR types in object-valued metadata.
- Assuming metadata values are deeply cloned.
- Expecting item-instance IDs to survive restore.
- Relying only on restore-time content events instead of rebuilding the final view.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies And Rules](POLICIES_AND_RULES.md)
- [Transactions And Transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Events And UI Integration](EVENTS_AND_UI.md)
- [Extending the system](../README.md#extending-the-system)
