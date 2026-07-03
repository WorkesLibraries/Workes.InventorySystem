# Core Concepts

`Workes.InventorySystem` separates the description of available item types from the runtime inventories that own item stacks.

The central model is:

```text
ItemCatalog<TKey>
  Registry   -> canonical registered item definitions
  Schemas    -> definition structure and required attributes
  Tags       -> declared tag vocabulary and hierarchy
  Attributes -> declared definition-attribute vocabulary

InventoryManager<TKey>
  Catalog
  Default stack resolver
  Default capacity policy
  Default layout
  Default rules

Inventory<TKey>
  Items      -> owned runtime stacks in storage order
  Layout     -> placement and presentation
  Policies   -> stacking and capacity
  Rules      -> semantic constraints
  Changed    -> committed-change notifications
```

Most applications follow two phases:

1. Define and freeze an item universe.
2. Create inventories and mutate them through inventory-owned APIs.

## The Item Universe

An `ItemCatalog<TKey>` describes the kinds of items that a related group of inventories may contain. It owns:

- the `ItemRegistry<TKey>` of registered item definitions and ID migrations.
- the schemas referenced by those definitions.
- the tag vocabulary.
- the definition-attribute vocabulary.

The catalog is setup-time state. Register definitions and declare the vocabulary they use, then call `ItemCatalog<TKey>.Freeze()`.

Freezing validates the complete item universe and prevents further registry or schema changes. An `InventoryManager<TKey>` cannot create inventories until its catalog is frozen.

```csharp
var catalog = new ItemCatalog<string>();

var apple = new ItemDefinition<string>("apple");
var coin = new ItemDefinition<string>("coin");

catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Freeze();
```

This simple form uses the default item schema and requires no custom tags or attributes.

## Three Levels Of Item Identity

The phrase “item type” can refer to several different things. Keeping these levels distinct prevents many common mistakes.

| Term | Meaning | Example |
|---|---|---|
| Item definition class | The C# type used to author a family of definitions. | `WeaponDefinition` |
| Registered definition | The canonical object registered under a stable ID in a catalog. | The registered `"iron_sword"` definition |
| Item instance | A runtime stack owned by an inventory. | Five iron swords in a chest |

### Item definition class

An item definition class controls how a family of definitions is authored. Plain items can use `ItemDefinition<TKey>` directly. More structured item families can derive from it and own a schema.

For example, a `WeaponDefinition` class might require every registered weapon to provide weight and damage attributes. The class and schema describe the family; they are not runtime inventory items.

### Registered definition

A registered definition is one concrete, catalog-known item type. It has:

- an explicit stable `Id`.
- the schema selected by its definition class.
- definition-level attributes.
- direct tags.

Definitions are registered before catalog freeze. After registration, the same object is the canonical definition for that ID within the catalog.

### Item instance

An `ItemInstance<TKey>` is a runtime stack owned by an inventory. It contains:

- a reference to its canonical registered definition.
- an amount.
- a unique runtime `InstanceId`.
- per-instance metadata.

Application code may inspect item instances, but it does not construct them or change their amounts directly.
Inventories, committed transactions, transfers, portable snapshot application, splits, and rebuild operations create
and update them.

## Stable IDs And Canonical Definitions

`TKey` is the definition-ID type used consistently by catalogs, definitions, inventories, transactions, transfers, and serialized items.

Common choices include:

- `string` for readable content and persistence IDs.
- `Guid` for generated stable IDs.
- explicit integer or enum values for closed definition sets.
- custom value types with stable equality and hash behavior.

An ID is persistent identity, not a list index, layout position, or display name.

### Equal IDs do not make definitions interchangeable

An inventory accepts only the definition object registered in its catalog. A detached object with the same ID is still a different definition and is rejected.

```csharp
var catalog = new ItemCatalog<string>();
var registeredApple = new ItemDefinition<string>("apple");

catalog.Registry.Register(registeredApple);
catalog.Freeze();

var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(99),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);

var inventory = manager.CreateInventory();

inventory.Add(registeredApple, amount: 5);

var detachedApple = new ItemDefinition<string>("apple");
var accepted = inventory.TryAdd(detachedApple, out var error);

// accepted is false because detachedApple is not the registered object.
```

This invariant ensures that rules, schemas, tags, attributes, serialization, and migrations all refer to the same authoritative definition.

When loading persistent data, resolve saved IDs through the catalog registry instead of constructing replacement definition objects. Registry migrations can map obsolete IDs to current canonical definitions.

## Schemas Belong To Definition Classes

An `ItemSchema<TKey>` describes the expected structure of an item definition of the definition class it belongs to.
It can be seen as a contract, which also means it gives you assurances about the definition class. A schema can:

- require typed definition attributes.
- contribute schema-level tags.
- inherit requirements and tags from a parent schema.

Simple definitions (`ItemDefinition<TKey>`) use `ItemSchema<TKey>.Default`.

Custom definition classes normally declare a class-owned schema with `ItemSchema<TKey>.CreateFor<TDefinition>(...)` and pass it through protected constructor chaining. Callers then construct the appropriate definition class using ordinary authoring data; they do not select arbitrary schemas at registration time.

```csharp
sealed class WeaponDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<WeaponDefinition>("weapon")
            .RequireAttribute<int>("damage")
            .AddTag("core:equipment.weapons");

    public WeaponDefinition(string id, int damage)
        : base(id, Schema)
    {
        DefineAttribute("damage", damage);
    }
}
```

Before freezing this catalog, the application must declare the referenced `damage` attribute and `core:equipment.weapons` tag in the catalog vocabulary.

The important conceptual boundary is:

- the C# class owns how its definitions are authored.
- the schema describes and validates that definition family.
- each registered definition supplies the concrete ID, attributes, and direct tags.
- item instances refer to a registered definition but carry runtime amount and metadata.

## Managers Create Related Inventories

An `InventoryManager<TKey>` combines a frozen catalog with defaults for:

- stack resolution.
- capacity policy.
- layout.
- rules.

The manager is a factory and shared configuration point. Every inventory created by it uses the same catalog, so all of those inventories agree on canonical definitions and identity.

```csharp
var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(99),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);

var backpack = manager.CreateInventory();
var chest = manager.CreateInventory();
```

Different managers can share one frozen catalog when they need different default policies or layouts over the same item universe.

## Inventories Own Runtime State

`Inventory<TKey>` is the normal application-facing coordinator. It owns the item instances it contains and routes state changes through stacking, capacity, rules, and layout validation.

Normal operations include:

- adding and removing amounts.
- moving, swapping, merging, and splitting stacks.
- committing transactions.
- transferring items between inventories.
- changing inventory-owned rules or component parameters.
- mutating metadata on owned instances.
- applying portable snapshots.

The inventory exposes both conditional and expected-success styles:

- `Try...` methods return `false` with an error when rejection is an expected branch.
- Throwing wrappers such as `Add(...)` are convenient when the operation is expected to succeed.

Rejected operations leave observable inventory state unchanged and do not emit committed-change events. Successful operations commit the validated result and then notify through `Inventory<TKey>.Changed`.

## Ownership Is Not Presentation

`Inventory<TKey>.Items` is the inventory’s read-only storage order. It answers what stacks the inventory owns; it is not necessarily the order or position shown in a UI.

The layout owns presentation concerns such as:

- addressable slots or cells.
- automatic placement.
- empty positions.
- movement and swapping.
- visual sorting.
- affected layout contexts for UI refresh.

Sorting a layout changes placement, not the order of `Inventory<TKey>.Items`.

This distinction allows the same inventory ownership model to support entry lists, slots, grids, equipment, sections, and multi-cell grids.

## Separate Runtime Concerns

Several components participate in inventory validation, but each answers a different question.

| Concern | Responsibility |
|---|---|
| Stack resolver | How large may a compatible stack become? |
| Capacity policy | Does the inventory have enough resource capacity for the proposed result? |
| Rules | Are the item and resulting inventory state semantically allowed? |
| Layout | Can the proposed structure be represented and where should it appear? |
| Metadata | What runtime variation belongs to this particular stack? |

These concerns are composed by the inventory rather than collapsed into one container implementation. Applications can therefore reuse the same definitions with different inventory behavior.

For example, two inventories may share one catalog while using different layouts, stack limits, capacity policies, or rules.

## Setup And Runtime At A Glance

```text
Setup
  Create catalog
  Declare tags and attributes when needed
  Create definitions
  Register canonical definition objects
  Register ID migrations when needed
  Freeze catalog

Inventory creation
  Create manager with the frozen catalog and default components
  Create one or more inventories

Runtime
  Pass canonical definitions to inventory operations
  Inspect inventory-owned item instances
  Route mutations through inventory APIs
  Use layouts for placement and presentation
  React to committed changes through events
```

## Common Mistakes

- Creating inventories before freezing the catalog.
- Constructing a new definition object when the canonical registered object should be reused or resolved.
- Treating numeric IDs as storage indexes or UI positions.
- Constructing or directly changing item instances instead of using inventory operations.
- Treating `Inventory.Items` as visual layout order.
- Mutating a layout, rule set, stack resolver, or capacity policy around the inventory’s validation paths.
- Mixing definition-level attributes with per-instance metadata.

## Continue Reading

Continue with the detailed guides for each subsystem:

- [Catalogs and definitions](CATALOGS_AND_DEFINITIONS.md)
- [Inventory operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies and rules](POLICIES_AND_RULES.md)
- [Transactions and transfers](TRANSACTIONS_AND_TRANSFERS.md)
- [Metadata](INVENTORY_OPERATIONS.md#instance-metadata)
- [Events and UI integration](EVENTS_AND_UI.md)
- [Persistence](PERSISTENCE.md)
- [Extending the system](EXTENDING.md)
