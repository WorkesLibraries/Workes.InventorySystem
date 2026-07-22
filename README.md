# Workes.InventorySystem

[![NuGet](https://img.shields.io/nuget/v/Workes.InventorySystem.svg)](https://www.nuget.org/packages/Workes.InventorySystem)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/LICENSE)

`Workes.InventorySystem` is a reusable .NET inventory library for games and other applications that need structured item
ownership. It is not tied to one genre, UI, container shape, or persistence format.

The package separates item identity, inventory contents, stacking, capacity, rules, placement, transactions, events,
and persistence so each concern can be configured or extended without replacing the rest of the system.

## Highlights

- Catalog-registered item definitions with stable IDs, tags, typed attributes, schemas, and ID migrations.
- Configurable stack resolvers, capacity policies, and inventory rules.
- Entry, slot, grid, multi-cell grid, equipment, and sectioned layouts.
- Direct placement, automatic placement, movement, swapping, sorting, and repacking.
- Atomic local and cross-inventory transactions, bulk transformations, and swaps.
- Per-instance metadata with inventory-owned validation and events.
- Structured change events for gameplay, auditing, and incremental UI synchronization.
- Portable, serializer-friendly snapshots with exact restoration, reconciliation, and salvage workflows.
- Extension contracts for definitions, policies, rules, layouts, footprints, sorting, and persistence codecs.

## Installation

Install the package from [NuGet](https://www.nuget.org/packages/Workes.InventorySystem):

```bash
dotnet add package Workes.InventorySystem --version 2.0.0
```

Or add a package reference:

```xml
<PackageReference Include="Workes.InventorySystem" Version="2.0.0" />
```

The package targets .NET Standard 2.1.

## Quick Example

Create a catalog, register the canonical item definitions, freeze the catalog, and use an inventory created by a
manager:

```csharp
using System;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

var catalog = new ItemCatalog<string>();

var apple = new ItemDefinition<string>("apple");
var coin = new ItemDefinition<string>("coin");

catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Freeze();

var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(maxStack: 10),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);

var inventory = manager.CreateInventory();

inventory.Add("apple", amount: 5);
inventory.Add("coin", amount: 25);

Console.WriteLine(inventory.Count("apple"));     // 5
Console.WriteLine(inventory.Find("coin").Count); // 3 stacks: 10, 10, 5
```

Use throwing methods such as `Add(...)` when success is expected. Use their `Try...` counterparts when rejection is a
normal application branch; rejected operations leave the inventory unchanged and report an `InventoryFailure` with a
stable `Kind`, stable `Code`, and human-readable `Message`.

See the [Quick Start](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/QUICK_START.md) for the
complete first-use walkthrough.

## Mental Model

| Component | Responsibility |
|---|---|
| `ItemCatalog<TKey>` | Owns the valid item universe and canonical definitions. |
| `InventoryManager<TKey>` | Combines a catalog with defaults used to create related inventories. |
| `Inventory<TKey>` | Owns runtime item stacks and coordinates validated mutation. |
| Stack resolver | Determines the maximum size of compatible stacks. |
| Capacity policy | Validates shared limits such as total quantity or weight. |
| Rules | Enforce semantic constraints for one inventory. |
| Layout | Owns placement and presentation without owning the items themselves. |

Two rules prevent many integration mistakes:

1. Reuse the exact definition objects registered in the catalog. A detached definition with the same ID is not the
   canonical definition.
2. Treat `Inventory.Items` as ownership and storage order, not UI order. Query the active layout for visible placement.

## Built-In Placement Models

| Layout | Typical use |
|---|---|
| `EntryLayout<TKey>` | Lists, bags, and collections without stable empty positions. |
| `SlotLayout<TKey>` | Hotbars and fixed-size containers. |
| `GridLayout<TKey>` | Single-cell items on a two-dimensional grid. |
| `MultiCellGridLayout<TKey>` | Rectangular item footprints, anchors, and packing. |
| `EquipmentLayout<TKey>` | Named positions with definition or tag restrictions. |
| `SectionedLayout<TKey>` | Multiple named slot groups with independent restrictions. |

Layouts own contexts, placement, sorting, and repacking. Inventory remains responsible for item lifetime, validation,
atomic commit, events, and application-facing layout queries such as `GetItemAt(context)`.

## Documentation

Start here:

1. [Quick Start](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/QUICK_START.md)
2. [Core Concepts](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/CONCEPTS.md)
3. [Inventory Operations](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/INVENTORY_OPERATIONS.md)

Focused guides:

- [Catalogs And Definitions](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/CATALOGS_AND_DEFINITIONS.md)
- [Layouts](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/LAYOUTS.md)
- [Policies And Rules](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/POLICIES_AND_RULES.md)
- [Transactions](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/TRANSACTIONS.md)
- [Events And UI Integration](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/EVENTS_AND_UI.md)
- [Failure Handling](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/FAILURES.md)
- [Persistence](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/PERSISTENCE.md)
- [Extending The Inventory System](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/docs/EXTENDING.md)

See the [Changelog](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/CHANGELOG.md) for release history
and migration-sensitive changes.

The repository also contains
[executable examples](https://github.com/WorkesLibraries/Workes.InventorySystem/tree/main/tests/Examples) covering item
setup, layouts, transfers, policies, events, metadata, and other common workflows. Use them when you find the documentation to be lacking examples.

## Persistence Boundaries

`CaptureSnapshot()` returns a non-generic, deeply detached object model suitable for ordinary serializers. The package
owns inventory snapshot semantics, validation, and restoration; your application chooses JSON, MessagePack, another
serializer, and its own file or database workflow.

The package does not own save slots, compression, encryption, application-envelope versioning, or file I/O. The older
generic `Serialize()` and `Deserialize(...)` APIs remain only as obsolete compatibility APIs.

## License

Workes.InventorySystem is available under the
[MIT License](https://github.com/WorkesLibraries/Workes.InventorySystem/blob/main/LICENSE).
