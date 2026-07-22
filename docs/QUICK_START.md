# Quick Start

This guide takes a new project from package installation to a working inventory with registered items, stacking, adding,
and querying.

## Prerequisites

You need:

- a .NET project compatible with .NET Standard 2.1.
- the .NET SDK or another NuGet-capable development environment.

The examples use `string` item-definition IDs and top-level C# statements.

## Install The Package

From the project directory:

```bash
dotnet add package Workes.InventorySystem
```

The NuGet package ID is `Workes.InventorySystem`.

## Create A Working Inventory

Add the following code to your application:

```csharp
using System;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

// 1. Create the item universe.
var catalog = new ItemCatalog<string>();

var apple = new ItemDefinition<string>("apple");
var coin = new ItemDefinition<string>("coin");
var healthPotion =
    new ItemDefinition<string>("health_potion");

// 2. Register the canonical definition objects.
catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Registry.Register(healthPotion);

// 3. Validate and lock the item universe.
catalog.Freeze();

// 4. Choose defaults for inventories created by this manager.
var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(maxStack: 10),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);

// 5. Create and use an inventory.
var inventory = manager.CreateInventory();

inventory.Add("apple", amount: 5);
inventory.Add("coin", amount: 25);
inventory.Add("health_potion", amount: 2);

// 6. Query total amounts and current stacks.
Console.WriteLine(
    $"Apple amount: {inventory.Count("apple")}");

Console.WriteLine(
    $"Coin amount: {inventory.Count("coin")}");

Console.WriteLine(
    $"Coin stacks: {inventory.Find("coin").Count}");

Console.WriteLine(
    $"Contains a health potion: " +
    inventory.Contains("health_potion"));
```

The output is:

```text
Apple amount: 5
Coin amount: 25
Coin stacks: 3
Contains a health potion: True
```

The coin amount becomes three stacks because the fixed stack resolver allows at most `10` items per compatible stack.
`Count("coin")` returns the total amount across those stacks. Definition-object overloads such as `Count(coin)` and
`Add(coin)` are also available when your code already holds the canonical registered definition object.

## What Each Part Does

| Part | Responsibility |
|---|---|
| `ItemCatalog<string>` | Owns the valid item universe. |
| `ItemDefinition<string>` | Describes one registered kind of item. |
| `catalog.Registry.Register(...)` | Makes one definition object canonical for its ID. |
| `catalog.Freeze()` | Validates the catalog and enables inventory creation. |
| `FixedSizeStackResolver<string>` | Limits compatible stacks to 10 items. |
| `UnlimitedCapacityPolicy<string>` | Adds no inventory-wide capacity limit. |
| `EntryLayout<string>` | Presents stacks as an ordered entry list. |
| `InventoryManager<string>` | Combines the catalog with inventory defaults. |
| `manager.CreateInventory()` | Creates an independent runtime inventory. |

## Two Rules To Remember

### Reuse registered definition objects

Pass the canonical objects registered in the catalog:

```csharp
inventory.Add(apple);
```

Do not construct a new same-ID definition when adding:

```csharp
var detachedApple =
    new ItemDefinition<string>("apple");

// Rejected: detachedApple is not the registered object.
inventory.TryAdd(
    detachedApple,
    out var failure);
```

When loading saved IDs, resolve them through the catalog registry.

### Freeze before creating inventories

Register definitions and declare any tags or attributes before calling:

```csharp
catalog.Freeze();
```

An `InventoryManager<TKey>` cannot create inventories from an unfrozen catalog.

## Expected Rejection

Use `Try...` methods when failure is an ordinary application branch:

```csharp
if (!inventory.TryAdd(
        apple,
        out var failure,
        amount: 5))
{
    Console.WriteLine(
        $"Could not add apples: {failure?.Message}");
}
```

Use throwing methods such as `Add(...)` when the operation is expected to succeed.
Those throwing wrappers raise project-owned inventory exceptions carrying the same `InventoryFailure`.
See [Failure Handling](FAILURES.md) for categories, stable codes, and exception behavior.

Rejected operations leave the inventory unchanged.

## Where To Go Next

Recommended reading:

1. [Core Concepts](CONCEPTS.md) explains identity, ownership, managers, inventories, and layouts.
2. [Catalogs And Definitions](CATALOGS_AND_DEFINITIONS.md) covers tags, attributes, schemas, and ID migrations.
3. [Inventory Operations](INVENTORY_OPERATIONS.md) covers adding, removing, querying, movement, sorting, and metadata.

Focused guides:

- [Layouts](LAYOUTS.md)
- [Policies And Rules](POLICIES_AND_RULES.md)
- [Transactions](TRANSACTIONS.md)
- [Events And UI Integration](EVENTS_AND_UI.md)
- [Failure Handling](FAILURES.md)
- [Persistence](PERSISTENCE.md)
