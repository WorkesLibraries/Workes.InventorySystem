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

Snapshot restoration is a separate API evolution. This guide describes the capture and representation contract.

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
sealed class ItemKey
{
    public string Value { get; }
}

sealed class ItemKeyCodec :
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
future snapshot application.

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
decoded entry contexts for the later snapshot-application workflow.

Layout kinds are global rather than scoped to `TKey`: one kind identifies one layout type across all closed key types.
Derived layouts inherit a built-in codec only when its complete persistent shape truly remains identical. A derived
layout with extra state must override `SnapshotCodec`.

## Definition IDs And Migrations

Definition IDs use the built-in codec or type-level codec assigned to the exact `TKey` type. During future restoration,
the target inventory will:

1. decode the ID through that registered codec.
2. pass the decoded ID to `ItemRegistry<TKey>.Resolve(...)`.
3. receive the target catalog's canonical definition or registered migration replacement.

The snapshot does not serialize definition objects.

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
explicit migration path until the new snapshot-application APIs are available.

## Common Mistakes

- Treating `InventorySnapshot` as the whole application save file.
- Expecting snapshot capture to persist policies, rules, or catalog definitions.
- Adding a custom `TKey` without its `InventorySnapshotKeyCodec` attribute.
- Changing a codec ID when evolving its data version.
- Assuming arbitrary collections or domain objects serialize automatically.
- Persisting a cyclic metadata graph.
- Using a derived layout whose inherited codec omits derived persistent state.
- Treating snapshot-local entry IDs as runtime item-instance identity.
- Calling the obsolete `Serialize()` API for new persistence work.

## Continue Reading

- [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory Operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Events And UI Integration](EVENTS_AND_UI.md)
