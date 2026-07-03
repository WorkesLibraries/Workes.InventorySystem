# Persistence

`Workes.InventorySystem` captures runtime state as a portable, non-generic `InventorySnapshot`. The package converts
inventory state to this DTO graph; the application chooses how to serialize and store it.

```text
Inventory.CaptureSnapshot()
  -> InventorySnapshot
  -> application serializer
  -> application storage
```

The package deliberately does not provide JSON, MessagePack, file or database I/O, save slots, compression,
encryption, cloud synchronization, or an application save envelope.

Snapshot application is atomic and supports exact restoration, lossless reconciliation, and explicitly lossy salvage.

## Capture

Expected-success capture throws when runtime state is not representable:

```csharp
InventorySnapshot snapshot =
    inventory.CaptureSnapshot();
```

Use the conditional form when a custom key or layout codec may reject capture:

```csharp
if (!inventory.TryCaptureSnapshot(
        out var snapshot,
        out var error))
{
    Log(error);
}
```

Capture either returns one complete snapshot or no snapshot. It never mutates the inventory.

## Snapshot Contents

`InventorySnapshot` contains:

| Member | Meaning |
|---|---|
| `FormatVersion` | Package-owned snapshot schema version. The current version is `1`. |
| `Entries` | Item instances in `Inventory.Items` storage order. |
| `Attributes` | Inventory-level attributes. |
| `Layout` | Stable layout kind, layout-data version, shape, and placement state. |

Each entry contains:

- a deterministic snapshot-local ID such as `e0`.
- an encoded definition ID.
- the stack amount.
- encoded per-instance metadata.

Snapshots do not contain catalog definitions, schemas, tags, definition attributes, policies, rules, stack-resolver
configuration, runtime `InstanceId` values, or application save versions. Recreate that configuration independently.

Every application mode replaces the target inventory's attributes with the snapshot attributes as part of the same
atomic commit. An unsupported runtime attribute makes capture fail; an encoded attribute that cannot be decoded makes
assessment and application fail. Salvage does not silently discard malformed attributes.

## Portable Values

`SnapshotEncodedValue` stores:

- a stable codec ID.
- the codec data version.
- a concrete `SnapshotValue`.

`SnapshotValue` is a closed value tree containing only:

- null.
- Boolean and string scalars.
- lists of encoded values.
- string-keyed objects of encoded values.

There are no `object` properties or serializer-discovered runtime subtypes. Numeric codecs intentionally use exact
string payloads internally. Floating-point values preserve their complete IEEE bit pattern, including negative zero,
infinities, and NaN payloads. Decimal values preserve all four decimal words.

Built-in codecs cover:

- `string`, `char`, and `bool`.
- every signed and unsigned integral type.
- `float`, `double`, and `decimal`.
- `Guid`, `DateTime`, `DateTimeOffset`, and `TimeSpan`.
- one-dimensional arrays and `List<T>` recursively.

Metadata deliberately does not accept dictionaries, enums, multidimensional arrays, arbitrary enumerable
implementations, literal `object` values, or domain objects. Unsupported metadata is rejected immediately by
`InstanceMetadata.Add(...)`, `Set(...)`, `Change(...)`, `Replace(...)`, and `Transform(...)`, including their `Try`
forms. The complete proposed metadata state is checked before commit. Collection contents are preserved, but reference
identity is not; cyclic graphs are rejected.

## Custom Key Codecs

Custom codecs extend definition IDs only, not metadata. Implement `IInventorySnapshotKeyCodec<TKey>` in a separate
stateless class and associate it directly with the custom key type:

```csharp
[InventorySnapshotKeyCodec(
    typeof(ItemKeyCodec))]
public sealed record ItemKey(string Value);

public sealed class ItemKeyCodec :
    IInventorySnapshotKeyCodec<ItemKey>
{
    public string FormatId =>
        "com.example.inventory.item-key";

    public int CurrentVersion => 1;

    public bool TryEncode(
        ItemKey value,
        out SnapshotValue? encoded,
        out string? error)
    {
        encoded =
            SnapshotValue.String(value.Value);
        error = null;
        return true;
    }

    public bool TryDecode(
        SnapshotValue encoded,
        int version,
        out ItemKey value,
        out string? error)
    {
        if (version != 1 ||
            encoded.Kind != SnapshotValueKind.String ||
            encoded.StringValue == null)
        {
            value = default!;
            error = "Unsupported item-key data.";
            return false;
        }

        value =
            new ItemKey(encoded.StringValue);
        error = null;
        return true;
    }
}
```

The attribute uses `typeof(ItemKeyCodec)` directly; there is no assembly scanning, inventory option, or public
registration step. The codec needs a public parameterless constructor. An exact key type has one codec and each custom
codec ID identifies one key type process-wide. The `workes.inventory.` prefix is reserved for package codecs.

Codecs must be stateless and safe for concurrent calls. When evolving a codec, keep its stable `FormatId`, increase
`CurrentVersion`, and continue decoding every historical version the application supports.

`InventorySnapshotCodecs.TryEncode(...)` and `TryDecode(...)` are also available for testing codec contracts.
They cover package-supported portable values; custom key association is resolved by inventory snapshot capture and
application.

## Deep Detachment

Capture recursively creates new DTOs and collections. Later mutation of inventory metadata, inventory attributes, or
layout state cannot change an existing snapshot. Mutating the snapshot cannot change the inventory.

Shared runtime references are encoded as independent values. Object identity is not a persistence concept. Custom codec
output is validated and copied before it enters the snapshot.

## Layout Snapshots

Built-in layout kinds are stable and versioned:

| Layout | Snapshot kind |
|---|---|
| Entry | `workes.inventory.layout.entry` |
| Slot | `workes.inventory.layout.slot` |
| Grid | `workes.inventory.layout.grid` |
| Multi-cell grid | `workes.inventory.layout.multi-cell-grid` |
| Equipment | `workes.inventory.layout.equipment` |
| Sectioned | `workes.inventory.layout.sectioned` |

Layout placement references entry IDs rather than storage indexes. Reordering the `Entries` DTO list therefore does not
silently retarget saved positions.

Grid layouts capture dimensions and placement order. Multi-cell grids also capture their default anchor. Equipment and
sectioned layouts capture stable slot or section identity and shape. Configuration objects such as footprint providers
and restrictions remain application configuration and are not embedded.

Every `IInventoryLayout<TKey>` exposes its separate stateless codec through `SnapshotCodec`:

```csharp
public IInventoryLayoutSnapshotCodec<ItemKey>
    SnapshotCodec =>
        MyLayoutSnapshotCodec.Instance;
```

There is no public layout registration. Package and custom layouts use the same contract. `TryCapture(...)` receives an
`InventoryLayoutSnapshotCaptureContext<TKey>` that resolves an `ItemInstance<TKey>` directly to a stable entry ID; it
never exposes storage indexes. `TryDecode(...)` validates every supported data version and returns an inert
`InventoryLayoutSnapshotCandidate<TKey>` without mutating a live layout. The candidate includes detached data and
decoded entry contexts for snapshot application.

`TryCreateExactLayout(...)` completes the codec contract by reconstructing an isolated, exactly placed layout using
current runtime configuration. A successful capture must be exactly restorable into an equivalently configured
inventory. Restoration may reject changed layout shape, restrictions, footprints, or other runtime configuration.

Layout kinds are global rather than scoped to `TKey`: one kind identifies one layout type across all closed key types.
Derived layouts inherit a built-in codec only when its complete persistent shape truly remains identical. A derived
layout with extra state must override `SnapshotCodec`.

## Definition IDs And Migrations

Definition IDs use the built-in codec or type-level codec assigned to the exact `TKey` type. During application, the
target inventory:

1. decodes the ID through the built-in or type-assigned codec.
2. passes the decoded ID to `ItemRegistry<TKey>.Resolve(...)`.
3. receives the target catalog's canonical definition or registered migration replacement.

The snapshot does not serialize definition objects.

## Assess Before Applying

`AssessSnapshot(...)` runs the same candidate planning and validation used by application without mutating the
inventory:

```csharp
SnapshotAssessmentResult assessment =
    inventory.AssessSnapshot(snapshot);

if (assessment.CanRestoreExactly)
{
    inventory.RestoreSnapshot(snapshot);
}
else if (assessment.CanReconcileWithoutLoss)
{
    inventory.ReconcileSnapshot(snapshot);
}
```

The result distinguishes exact restoration, lossless reconciliation, and salvage. `Issues` explains why stronger
outcomes failed, while `ProjectedLosses` describes salvage loss. Assessment is advisory: application always plans and
validates again because inventory policies, rules, layout configuration, catalog migrations, or the snapshot DTO may
change afterward.

## Exact Restoration

`RestoreSnapshot(...)` and `TryRestoreSnapshot(...)` preserve:

- snapshot entry storage order.
- one stack per snapshot entry with the exact saved amount and metadata.
- every saved layout position.
- the captured inventory attributes.
- all item quantities.

Exact restoration resolves definitions through the current key codec, registry, and migrations. Every layout codec is
required to reconstruct exact saved placement. Restoration rejects changed
stack limits, capacity, rules, layout shape, slot restrictions, footprints, or other current configuration that makes
the saved state invalid. It never implements restoration as repeated ordinary adds, so compatible saved stacks are not
merged or split.

The operation builds and validates an isolated candidate. Failure leaves contents, placement, attributes, and events
untouched.

## Lossless Reconciliation

`ReconcileSnapshot(...)` and `TryReconcileSnapshot(...)` ignore saved placement and instance boundaries while retaining
every resolved definition/metadata quantity. Current stack limits may split stacks; compatible entries may merge; the
current layout chooses automatic placement. Current capacity and rules validate the complete final replacement.

Use reconciliation for saves whose logical items remain valid but whose presentation shape has changed.

## Salvage

`SalvageSnapshot(...)` and `TrySalvageSnapshot(...)` retain a deterministic best-effort subset. Successful loss is
reported through `SnapshotApplicationResult.Losses`; it is never hidden as a failed or partially committed mutation.

`SnapshotSalvageOptions<TKey>` controls:

- priority through `PriorityComparer` (greater values are attempted first).
- partial quantities versus `WholeEntryOnly`.
- whether unknown definitions fail or may be discarded.
- the placement strategy, currently `GreedyAutomatic`.

Greedy salvage is deterministic but not globally optimal. A different order can produce a different retained subset,
especially for multi-cell layouts, capacity interactions, or rules. Partial-quantity search assumes ordinary monotonic
capacity behavior; use whole-entry retention or application-level preprocessing when rules have unusual
non-monotonic quantity constraints.

Malformed snapshots, unsupported codecs, invalid options, and unrecoverable configuration remain operation failures
even in salvage mode.

All application modes return `SnapshotApplicationResult`. Exact and reconciliation never report loss. Runtime
`InstanceId` values are newly created because they are deliberately not persistent identity.

## Application Events

Successful application emits one coherent `Inventory.Changed` event after final state is visible when it replaces any
contents or inventory attributes. Previous instances are `Removed`, replacements are `Added`, and they are not
represented as `Moved`. Inventory-attribute replacement has no dedicated semantic payload, so an application involving
attributes requests a full refresh.

`InventoryChangedEventArgs.Origin` identifies `SnapshotExactRestore`, `SnapshotReconciliation`, or `SnapshotSalvage`.
Failure emits no event. `RequiresFullRefresh` remains reserved for observable state not fully represented by semantic
payloads and contexts.

## External Serializers

The DTO graph uses mutable parameterless classes, concrete lists, enums, strings, integers, and Booleans. Ordinary
serializers can round-trip it without type discriminators for application domain objects.

For example:

```csharp
string json =
    JsonSerializer.Serialize(snapshot);

InventorySnapshot restoredDto =
    JsonSerializer.Deserialize<InventorySnapshot>(json)!;
```

The package does not depend on `System.Text.Json`; it is only an application-side example.

After external deserialization, structural validation is available:

```csharp
if (!InventorySnapshotValidator.TryValidate(
        restoredDto,
        out var error))
{
    RejectSave(error);
}
```

Generic validation rejects unsupported snapshot versions, malformed value trees, duplicate entry/property IDs, invalid
amounts, and malformed layout envelopes. Codec-specific layout decoding performs complete version and shape validation.
Actual compatibility with current inventory rules and configuration is part of snapshot assessment and restoration,
not DTO validation.

## Application Versioning

`InventorySnapshot.FormatVersion` versions only the package-owned snapshot schema. Wrap it in an application save model
for game or application migrations:

```csharp
sealed class PlayerSave
{
    public int Version { get; set; }

    public InventorySnapshot Inventory
    {
        get;
        set;
    } = new();
}
```

Application versions remain responsible for inventory purpose, current policy configuration, metadata conventions,
save-slot identity, and cross-system migrations.

## Legacy Compatibility

The following APIs remain behaviorally unchanged but are obsolete:

- `Inventory<TKey>.Serialize()`.
- `Inventory<TKey>.Deserialize(...)`.
- `SerializedInventory<TKey>`.
- `SerializedItem<TKey>`.

Their generic IDs, `Dictionary<string,object>` metadata, polymorphic `LayoutData`, shallow value copying, storage-index
placement maps, and non-atomic restoration make them unsuitable as a portable contract. They remain only as an
explicit migration path.

## Common Mistakes

- Treating `InventorySnapshot` as the whole application save file.
- Expecting snapshot capture to persist policies, rules, or catalog definitions.
- Adding a custom `TKey` without its `InventorySnapshotKeyCodec` attribute.
- Changing a codec ID when evolving its data version.
- Assuming arbitrary collections or domain objects serialize automatically.
- Persisting a cyclic metadata graph.
- Using a derived layout whose inherited codec omits derived persistent state.
- Treating snapshot-local entry IDs as runtime item-instance identity.
- Trusting a previous assessment instead of handling application failure.
- Assuming greedy salvage finds the globally optimal retained subset.
- Expecting exact restoration to adapt changed stack limits or layout shape.
- Calling the obsolete `Serialize()` API for new persistence work.

## Continue Reading

- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Events And UI Integration](EVENTS_AND_UI.md)
- [Extending The System](EXTENDING.md)
