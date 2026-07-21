# Failure Handling

`Workes.InventorySystem` treats rejected inventory operations as structured domain results. This lets application code
make reliable decisions without parsing human-readable messages.

Use this guide when you need to:

- handle normal inventory rejection in gameplay or UI code.
- choose between `Try...` APIs and throwing wrappers.
- branch on stable failure categories or codes.
- write custom policies, rules, layouts, or codecs that report useful failures.

For ordinary add/remove examples, see [Inventory Operations](INVENTORY_OPERATIONS.md). For extension contracts, see
[Extending The Inventory System](EXTENDING.md).

## Expected Failure Versus Programmer Misuse

The package separates two kinds of failure:

| Situation | API behavior | Examples |
|---|---|---|
| Expected domain rejection | `Try...` returns `false` with `InventoryFailure`; throwing wrappers throw a project exception containing that same failure | Capacity is full, a rule rejects an item, a layout position is unavailable, a snapshot cannot be applied |
| Programmer or setup misuse | Standard .NET exceptions are thrown directly | Null arguments, negative amounts, using a transaction with the wrong inventory, committing an already-applied transaction |

This distinction is intentional. Domain rejection is something application code may normally branch on. Programmer
misuse usually means the calling code is wrong and should be fixed.

## The Basic Pattern

Use `Try...` methods when rejection is part of normal control flow:

```csharp
if (!inventory.TryAdd(
        "apple",
        out var failure,
        amount: 5))
{
    if (failure?.Kind == InventoryFailureKind.Capacity)
    {
        ShowInventoryFullMessage();
    }
    else
    {
        Log(failure?.ToString());
    }
}
```

Use throwing wrappers when success is expected:

```csharp
try
{
    inventory.Add("apple", amount: 5);
}
catch (InventoryOperationException ex)
{
    Log(ex.Failure.Code);
    ShowError(ex.Failure.Message);
}
```

The throwing wrappers do not invent a second error model. They throw an `InventorySystemException` subclass carrying the
same `InventoryFailure` that the corresponding `Try...` method would have returned.

## Project Exceptions

`InventorySystemException` is the base exception for expected-success inventory-system wrappers that fail because the
inventory domain rejected the operation.

It derives from `InvalidOperationException`, so broad existing catches for invalid operation failures can still catch
it. Prefer catching the project-owned type when you need structured failure details:

```csharp
catch (InventorySystemException ex)
{
    InventoryFailure failure = ex.Failure;
}
```

Common project exception types include:

| Exception | Meaning |
|---|---|
| `InventorySystemException` | Base class for project-owned expected-success failures. |
| `InventoryOperationException` | Thrown by inventory operation wrappers such as `Add`, `Remove`, `Move`, `CommitTransaction`, snapshot application, and parameter mutation wrappers. |
| `DefinitionValidationException` | Thrown when definition or schema validation fails in a package-owned validation path. |

Standard `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, and ordinary
`InvalidOperationException` are still used for programmer misuse and invalid object state.

## Anatomy Of `InventoryFailure`

`InventoryFailure` is immutable and contains:

| Member | Use |
|---|---|
| `Kind` | Broad category, suitable for coarse branching such as capacity versus layout versus rules. |
| `Code` | Stable machine-readable identifier, suitable for precise branching, localization keys, telemetry, and tests. |
| `Message` | Human-readable description for logs, tools, or simple UI display. Messages may evolve over time. |
| `Component` | Optional subsystem or component type that reported or wrapped the failure. |
| `Source` | Optional stable source identifier, such as a rule ID or parameter ID. |
| `Cause` | Optional nested failure when a higher-level subsystem wraps a lower-level rejection. |

Branch on `Kind` or `Code`. Do not parse `Message`.

```csharp
if (!inventory.TryMove(from, to, out var failure))
{
    switch (failure?.Code)
    {
        case InventoryFailureCodes.LayoutInvalidContext:
            HighlightInvalidSlot();
            break;

        case InventoryFailureCodes.LayoutRejected:
            ShowPlacementBlocked();
            break;

        default:
            Log(failure);
            break;
    }
}
```

`ToString()` returns the message plus concise cause context when a nested cause exists.

## Failure Categories

`InventoryFailureKind` describes the broad reason category:

| Kind | Typical meaning |
|---|---|
| `Validation` | General validation rejected the request. |
| `Definition` | A definition object, definition ID, catalog registration, schema, tag, or attribute requirement failed. |
| `Metadata` | Inventory or item metadata rejected a value, missing key, wrong type, or unsupported portable shape. |
| `Stacking` | Stack compatibility or maximum stack-size resolution rejected the request. |
| `Capacity` | A capacity policy rejected the projected inventory. |
| `Rules` | An inventory rule rejected semantic, structural, or projected state. |
| `Layout` | Placement, context, movement, sorting, repacking, reconciliation, or layout shape rejected the request. |
| `Transfer` | Cross-inventory transfer planning or commit rejected the request. |
| `Transaction` | Transaction formulation, validation, or commit rejected the request. |
| `Persistence` | Legacy persistence or persistence-bound conversion rejected the request. |
| `Snapshot` | Snapshot capture, validation, assessment, codec decoding, or application rejected the request. |
| `Configuration` | Runtime component configuration or parameter mutation rejected the request. |
| `Extension` | Extension-provided code rejected or failed inside an expected validation path. |
| `Unknown` | The package could not classify the failure more precisely. |

## Built-In Failure Codes

Built-in codes are constants on `InventoryFailureCodes` and use the reserved package prefix
`workes.inventory.`. They are more stable than messages and are the best choice for tests, telemetry, and precise
application behavior.

Common groups include:

| Group | Example codes |
|---|---|
| General validation | `Unknown`, `ValidationRejected` |
| Definitions | `DefinitionInvalid`, `DefinitionUnresolved` |
| Metadata | `MetadataRejected`, `MetadataMissingKey`, `MetadataTypeMismatch`, `MetadataUnsupportedValue` |
| Stacking and capacity | `StackingRejected`, `CapacityRejected` |
| Rules | `RulesRejected` |
| Layouts | `LayoutRejected`, `LayoutInvalidContext`, `LayoutMappedIndexOutOfRange` |
| Transfers and transactions | `TransferRejected`, `TransferIncompatibleInventories`, `TransferEmpty`, `TransactionRejected` |
| Snapshots and persistence | `SnapshotRejected`, `SnapshotMalformed`, `SnapshotUnsupportedVersion`, `SnapshotMissingCodec`, `SnapshotCodecRejected`, `PersistenceRejected` |
| Runtime configuration | `ConfigurationRejected`, `ConfigurationUnsupportedParameter` |
| Extensions | `ExtensionRejected` |

Package-owned codes are reserved. Extension authors should use their own namespaced codes, for example
`com.example.inventory.rule.quest_locked`.

## Nested Causes

Higher-level systems can wrap lower-level failures while preserving the original cause.

For example, a transfer may fail because its target inventory rejects a simulated add. The transfer failure can have:

- `Kind = InventoryFailureKind.Transfer`
- `Code = InventoryFailureCodes.TransferRejected`
- `Cause.Kind = InventoryFailureKind.Layout`
- `Cause.Code = InventoryFailureCodes.LayoutRejected`

Likewise, `RuleContainer<TKey>` can wrap a rule rejection with the stable rule ID in `Source` and the rule type in
`Component`.

```csharp
if (!inventory.TryAdd("sword", out var failure))
{
    Console.WriteLine(failure?.Source);          // e.g. "equipment-only"
    Console.WriteLine(failure?.Component);       // e.g. "RequireAnyTagRule`1"
    Console.WriteLine(failure?.Cause?.Message);  // original rule explanation, when wrapped
}
```

This gives the UI and tooling both the high-level failing operation and the underlying reason.

## Extension Authoring

Extension contracts use `out InventoryFailure? failure` for conditional rejection:

```csharp
public bool CanAccept(
    Inventory<string> inventory,
    InventoryTransaction<string> transaction,
    out InventoryFailure? failure)
{
    if (IsQuestLocked(inventory))
    {
        failure = InventoryFailure.Create(
            InventoryFailureKind.Rules,
            "com.example.inventory.rule.quest_locked",
            "Quest-locked inventories cannot accept this item.",
            component: nameof(QuestLockRule),
            source: "quest-lock");
        return false;
    }

    failure = null;
    return true;
}
```

Use `InventoryFailure.Wrap(...)` when your extension rejects because of a lower-level failure and you want to preserve
that cause:

```csharp
failure = InventoryFailure.Wrap(
    InventoryFailureKind.Layout,
    "com.example.inventory.layout.shelf_rejected",
    "Shelf layout rejected placement",
    cause,
    component: nameof(ShelfLayout<string>));
```

Guidelines:

- return `false` with `InventoryFailure` for expected runtime rejection.
- use stable, namespaced custom codes for extension-owned behavior.
- keep messages useful to humans but avoid branching on them.
- throw standard exceptions for invalid constructor arguments or programmer misuse.
- keep validation side-effect free; rejected proposals must leave active inventory state unchanged.

## Migration From String Errors

In 2.0, public expected-failure APIs use structured failures instead of string errors:

```csharp
// Old shape
// inventory.TryAdd("apple", out string? error, amount: 5);

// New shape
inventory.TryAdd("apple", out InventoryFailure? failure, amount: 5);
```

Typical migration:

- replace `out string? error` with `out InventoryFailure? failure`.
- display `failure?.Message` where the old error string was shown.
- branch on `failure?.Kind` or `failure?.Code` instead of comparing text.
- catch `InventorySystemException` or `InventoryOperationException` when using throwing wrappers.

The failure object is for inventory-domain rejection, not for serializer choice, file I/O errors, network errors, save
slot management, or other application-owned infrastructure.
