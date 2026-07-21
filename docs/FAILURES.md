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

Custom codes are the intended extension point for application-specific rejection behavior. Keep them:

- stable once application code, telemetry, localization, or saved diagnostics may depend on them.
- namespaced to your application or package, not `workes.inventory.*`.
- specific enough for branching. Prefer `com.example.inventory.rule.quest_locked` over
  `com.example.inventory.rejected`.
- independent from `Message`. Messages are display and debugging text and may be rewritten without breaking callers.

Do not try to extend `InventoryFailureKind`. The kinds are intentionally broad package-owned categories so application
code can make coarse decisions consistently across built-in and custom components. Put extension-specific meaning in
`Code`, `Component`, `Source`, and `Cause`.

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

## Extension-Authored Failures

Extension contracts use `out InventoryFailure? failure` for conditional rejection. This includes custom rules, capacity
policies, stack resolvers, layouts, repack/reconciliation contracts, snapshot codecs, and custom key codecs.

Expected rejection should return `false` with an `InventoryFailure`; it should not throw. The inventory can then keep
the operation atomic, return the same failure from the `Try...` API, or wrap it in an `InventoryOperationException` when
the caller used an expected-success wrapper.

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

Choose the broad `InventoryFailureKind` that describes the failing subsystem and use a custom namespaced `Code` for the
application-specific reason. For example:

| Extension | Suggested kind | Example code |
|---|---|---|
| Gameplay rule | `Rules` | `com.example.inventory.rules.requires_license` |
| Capacity policy | `Capacity` | `com.example.inventory.capacity.guild_rank_too_low` |
| Stack resolver | `Stacking` | `com.example.inventory.stacking.quality_mismatch` |
| Layout | `Layout` | `com.example.inventory.layout.shelf_too_short` |
| Snapshot or key codec | `Snapshot` | `com.example.inventory.snapshot.item_key_malformed` |

Use `Component` for the component type or stable component name that reported the failure. Use `Source` for a stable
configured identifier, such as a rule ID, parameter ID, slot ID, section ID, codec format ID, or layout-kind ID.

```csharp
failure = InventoryFailure.Create(
    InventoryFailureKind.Layout,
    "com.example.inventory.layout.shelf_too_short",
    "The selected shelf is too short for this item.",
    component: nameof(ShelfLayout<string>),
    source: shelfId);
return false;
```

Use `InventoryFailure.Wrap(...)` when your extension rejects because of a lower-level failure and you want to preserve
that cause. A custom layout might wrap a metadata rejection, or a codec might wrap a malformed field failure:

```csharp
failure = InventoryFailure.Wrap(
    InventoryFailureKind.Layout,
    "com.example.inventory.layout.shelf_rejected",
    "Shelf layout rejected placement",
    cause,
    component: nameof(ShelfLayout<string>));
```

In application code, branch on your custom code just like a built-in code:

```csharp
if (!inventory.TryAdd("pickaxe", out var failure))
{
    if (failure?.Code == "com.example.inventory.rules.requires_license")
        ShowLicensePrompt();
    else
        ShowToast(failure?.Message);
}
```

### Exceptions In Extensions

Custom exception types are rarely useful inside inventory extension contracts. The package already provides
`InventorySystemException` and `InventoryOperationException` for expected-success wrappers, and those exceptions carry
the same `InventoryFailure` that a `Try...` method would have returned.

Use this split:

- return `false` with `InventoryFailure` for expected runtime rejection.
- throw `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, or another standard exception for
  programmer/setup misuse such as invalid constructor arguments.
- let unexpected extension bugs throw normally; the package may convert exceptions caught inside expected extension
  paths with `InventoryFailure.FromException(...)`.
- avoid custom exception hierarchies for normal gameplay, layout, capacity, stacking, or codec rejection.
- use stable, namespaced custom codes for extension-owned behavior.
- keep messages useful to humans, but avoid branching on them.
- create failures explicitly; assigning a plain string to `InventoryFailure` is not supported.
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
- replace any string-returning extension rejection with `InventoryFailure.Create(...)` or `InventoryFailure.Wrap(...)`.
- catch `InventorySystemException` or `InventoryOperationException` when using throwing wrappers.

The failure object is for inventory-domain rejection, not for serializer choice, file I/O errors, network errors, save
slot management, or other application-owned infrastructure.
