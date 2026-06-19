# Workes.InventorySystem

`Workes.InventorySystem` is a broad, reusable .NET inventory system. It is not tied to one game genre, one UI shape, or one container model.

The package uses explicit item definition ids as persistent identity. A catalog owns the item universe, inventories own runtime item stacks, and layouts own placement/presentation. Stack resolution, capacity policies, rules, transactions, transfers, sorting, events, metadata, and persistence are separate systems that combine around that model.

Examples use `string` ids for readability. The system is extensible, but the first half of this README focuses on normal application usage. Extension contracts are covered later.

## Installation

NuGet publishing is planned after README wording, package metadata, package readme wiring, and licensing are finalized. Until then, reference the project directly:

```xml
<ItemGroup>
  <ProjectReference Include="..\Workes.InventorySystem\src\Workes.InventorySystem.csproj" />
</ItemGroup>
```

The main project is `src/Workes.InventorySystem.csproj`.

## Table Of Contents

- [Quick Start](#quick-start)
- [Using The System As-Is](#using-the-system-as-is)
  - [Mental Model](#mental-model)
  - [Identity And Registered Definitions](#identity-and-registered-definitions)
  - [Catalog, Tags, Attributes, Schemas, And Definitions](#catalog-tags-attributes-schemas-and-definitions)
  - [Inventories And Managers](#inventories-and-managers)
  - [Stack Resolution](#stack-resolution)
  - [Layouts And Layout Contexts](#layouts-and-layout-contexts)
  - [Capacity Policies](#capacity-policies)
  - [Rules](#rules)
  - [Runtime Parameter Mutation](#runtime-parameter-mutation)
  - [Transactions](#transactions)
  - [Transfers](#transfers)
  - [Metadata](#metadata)
  - [Events And UI Integration](#events-and-ui-integration)
  - [Persistence](#persistence)
  - [Usage Pitfalls And Caveats](#usage-pitfalls-and-caveats)
- [Extending The System](#extending-the-system)
  - [Custom Stack Resolver](#custom-stack-resolver)
  - [Parameterized Stack Resolvers](#parameterized-stack-resolvers)
  - [Parameterized Components](#parameterized-components)
  - [Custom Capacity Policy](#custom-capacity-policy)
  - [Custom Rule](#custom-rule)
  - [Custom Layout](#custom-layout)
  - [Custom Layout Contexts](#custom-layout-contexts)
  - [Layout Persistence](#layout-persistence)
  - [Layout Cloning](#layout-cloning)
  - [Layout Sorting](#layout-sorting)
  - [Layout Events And Affected Contexts](#layout-events-and-affected-contexts)
  - [Custom Layout Implementation Checklist](#custom-layout-implementation-checklist)
  - [Custom Grid Footprint Providers](#custom-grid-footprint-providers)
  - [Extension Example Scope](#extension-example-scope)
- [Reference And Summary](#reference-and-summary)
  - [Extension Pitfalls And Caveats](#extension-pitfalls-and-caveats)

## Quick Start

This is the smallest useful flow: create a catalog, register definitions, freeze the catalog, create a manager, then create and mutate an inventory.

```csharp
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

var catalog = new ItemCatalog<string>();

var apple = new ItemDefinition<string>("apple");
var coin = new ItemDefinition<string>("coin");
var potion = new ItemDefinition<string>("health_potion");

catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Registry.Register(potion);
catalog.Freeze();

var manager = new InventoryManager<string>(
    new FixedSizeStackResolver<string>(99),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog: catalog);

var inventory = manager.CreateInventory();

inventory.TryAdd(apple, out var appleError, amount: 5);
inventory.TryAdd(coin, out var coinError, amount: 25);
inventory.TryAdd(potion, out var potionError, amount: 2);
inventory.TryRemoveByDefinition(apple, amount: 1, ignoreMetadata: true, out var removeError);
```

| Step | API | Why it matters |
|---|---|---|
| Define item universe | `ItemCatalog<TKey>` / `ItemRegistry<TKey>` | Shared definitions, tags, schemas, and attribute vocabulary. |
| Freeze catalog | `catalog.Freeze()` | Validates the item universe and enables inventory creation. |
| Create manager | `InventoryManager<TKey>` | Shared defaults for inventories. |
| Create inventory | `CreateInventory()` | Clones mutable layout and rule state. |
| Mutate inventory | `TryAdd`, `Add`, `TryRemove...`, `Remove...` | Validated inventory changes. |

# Using The System As-Is

## Mental Model

```text
ItemCatalog<TKey>
  Registry   -> ItemDefinition<TKey>
  Schemas    -> ItemSchema<TKey>
  Tags       -> TagCatalog
  Attributes -> AttributeCatalog

InventoryManager<TKey>
  Catalog
  DefaultStackResolver
  DefaultCapacityPolicy
  DefaultLayout
  DefaultRules

Inventory<TKey>
  Items         -> ownership/storage order
  Layout        -> UI/presentation placement
  Rules         -> inventory-owned semantic checks
  Transactions  -> validated structural changes
  Changed event -> UI/gameplay/audit integration
```

There are three levels of item identity:

| Term | Meaning | Example |
|---|---|---|
| Item definition class | The C# type used to author a kind of item. | `WeaponDefinition` |
| Registered definition | The catalog-owned item type instance. | registered `"iron_sword"` definition |
| Item instance | A stack or owned runtime instance in an inventory. | sword stack in backpack slot 3 |

`Inventory<TKey>.Items` is storage order: it says what the inventory owns. It is not UI order. Layouts own presentation placement, addressable positions, empty-position behavior, sorting, and UI refresh contexts.

Keep these concerns separate:

| Concern | Answers |
|---|---|
| Stack resolver | How large can a compatible stack become? |
| Capacity policy | Does the container have enough resource capacity? |
| Rule policy | Is this item or final state semantically allowed? |
| Layout | Can this structural placement be represented? |
| Metadata | What instance-level variation does this stack carry? |

Normal application code should primarily call `Inventory<TKey>` and source-owned transfer methods. Use layout contexts for placement, query `Inventory.Changed` for UI updates, and configure catalogs, rules, policies, and layouts through their normal setup APIs. Custom layouts, rules, policies, and stack resolvers are extension work and are described after the usage section.

## Identity And Registered Definitions

`TKey` is the item definition identity type used throughout the system:

- `ItemCatalog<TKey>` and `ItemRegistry<TKey>` use it to register and resolve definitions.
- `InventoryManager<TKey>` and `Inventory<TKey>` use it through their shared catalog.
- `ItemDefinition<TKey>` stores it as `Id`.
- `ItemInstance<TKey>`, transactions, transfers, and serialized items carry definitions identified by it.

This README uses `string` identities because they are easy to read in examples. Other stable key types are supported when their equality and hash behavior is appropriate for dictionary lookup.

| Identity type | Guidance |
|---|---|
| `string` | Good default for examples, content ids, and serialized data. |
| `Guid` | Useful for generated stable ids. |
| Integer-like ids | Supported as explicit stable ids, not implicit array positions. |
| Enums | Suitable for small closed definition sets. |
| Custom value objects/classes | Suitable when equality and hashing are stable. |
| `float` / `double` | Tested, but generally not recommended as persistent identity choices. |

Integer-like ids are still explicit ids:

```csharp
var catalog = new ItemCatalog<int>();

var wood = new ItemDefinition<int>(1001);
var stone = new ItemDefinition<int>(1002);

catalog.Registry.Register(wood);
catalog.Registry.Register(stone);
catalog.Freeze();
```

Definition ids are persistent values used by registry lookup, serialization, migrations, transfer validation, and detached-definition rejection.

> Registered definition rule: same id is not enough. Use the definition object registered in the inventory catalog.

Inventories can only contain definitions registered in their catalog registry. Register all definitions before `catalog.Freeze()`. Deserialization resolves definitions through the registry, and transfers cannot introduce definitions outside the target inventory catalog.

```csharp
var apple = new ItemDefinition<string>("apple");
catalog.Registry.Register(apple);
catalog.Freeze();

inventory.Add(apple, amount: 1);

var detachedApple = new ItemDefinition<string>("apple");
// inventory.TryAdd(detachedApple, out var error) returns false.
```

Rule of thumb:

- Do use the canonical registered definition object returned by your content setup or registry lookup.
- Do register every item definition before freezing the catalog.
- Do not treat a detached definition with the same id as interchangeable with the registered definition.
- Do not use numeric identities as list offsets or UI positions.

### Definition Id Migrations

Migrations map obsolete definition ids to canonical registered replacement definitions. Use them for save/content compatibility when a definition id changes or several obsolete ids collapse into one current definition.

Register the replacement definition first, then register migrations before `catalog.Freeze()`. The replacement definition must already be registered in the same registry, and it must be the same registered definition object. A detached same-id replacement definition is rejected. Migrations do not create new definitions and do not bypass the registered-definition invariant.

```csharp
var catalog = new ItemCatalog<string>();

var healthPotion = new ItemDefinition<string>("health_potion");
catalog.Registry.Register(healthPotion);

catalog.Registry.RegisterMigration("minor_healing_potion", healthPotion);
catalog.Registry.RegisterMigration("major_health_potion", healthPotion);

catalog.Freeze();

var resolved = catalog.Registry.Resolve("health_potion");
// resolved is the canonical registered healthPotion definition.
```

Rejected migration cases:

- obsolete id is already registered;
- obsolete id already has a migration;
- replacement definition is not registered;
- replacement definition has the same id as a registered definition but is a detached same-id object;
- registry is frozen.

During deserialization, serialized `DefinitionId` values resolve through registry migrations, so multiple obsolete ids can load into the same canonical registered definition.

## Catalog, Tags, Attributes, Schemas, And Definitions

The catalog is the item universe. It owns registered definitions, schemas, tags, and the attribute vocabulary.

### Catalog And Registry

| API | Meaning |
|---|---|
| `ItemCatalog<TKey>.Registry` | Registered item definitions and migrations. |
| `ItemCatalog<TKey>.Schemas` | Schema registry discovered from registered definitions. |
| `ItemCatalog<TKey>.Tags` | Catalog-owned tag vocabulary and hierarchy. |
| `ItemCatalog<TKey>.Attributes` | Catalog-owned definition attribute vocabulary. |
| `ItemCatalog<TKey>.Freeze()` | Validates and freezes registry, schemas, tags, and attributes. |
| `ItemCatalog<TKey>.ResolveTags(...)` | Resolves schema and definition tags through the catalog. |
| `ItemCatalog<TKey>.Satisfies(...)` | Checks resolved direct, schema, and hierarchical tag satisfaction. |
| `ItemRegistry<TKey>.Register(...)` | Adds a definition before freeze. |
| `ItemRegistry<TKey>.RegisterMigration(oldId, replacementDefinition)` | Maps an obsolete id to a registered replacement definition. |
| `ItemRegistry<TKey>.Contains(...)` | Checks registered ids. |
| `ItemRegistry<TKey>.TryGet(...)` | Attempts to get a registered definition. |
| `ItemRegistry<TKey>.Resolve(...)` | Resolves a definition id, including migrations. |

The public freeze workflow is `catalog.Freeze()`. Inventory creation requires the manager catalog to be frozen.

### Tags

`TagCatalog` is the authority for tag declaration and lookup. Schemas, definitions, rules, layouts, inventory helpers, and transfer helpers use string tag ids.

New catalogs default to namespaced tags. Call `catalog.Tags.UseNonNamespacedTagsOnly()` before defining any tags when your project wants non-namespaced dot hierarchy ids instead. `UseNamespacedTagsOnly()` can explicitly select the default namespaced mode before definitions, but as the catalog defaults to this, it does not really have an effect. Tag catalog modes are mutually exclusive.

| Mode | Example | How to enable | Hierarchy |
|---|---|---|---|
| Namespaced | `core:equipment.tools.knife` | default or `UseNamespacedTagsOnly()` before definitions | namespace:dot.path |
| Non-namespaced | `equipment.tools.knife` | `UseNonNamespacedTagsOnly()` before definitions | dot.path |

Namespaced example:

```csharp
var catalog = new ItemCatalog<string>();
catalog.Tags.UseNamespacedTagsOnly(); // Only to make it clear

catalog.Tags.Define("core:equipment");
catalog.Tags.Define("core:equipment.tools");
catalog.Tags.Define("core:equipment.tools.knife");
```

Non-namespaced example:

```csharp
var catalog = new ItemCatalog<string>();
catalog.Tags.UseNonNamespacedTagsOnly();

catalog.Tags.Define("equipment");
catalog.Tags.Define("equipment.tools");
catalog.Tags.Define("equipment.tools.knife");
```

Dot hierarchy works in both modes. Declaring `core:equipment.tools.knife` also creates parent hierarchy entries such as `core:equipment.tools` and `core:equipment` in the catalog. Direct definition tags are exposed as read-only string ids through `ItemDefinition<TKey>.Tags`. Use `ItemCatalog<TKey>.Satisfies(...)` when you want catalog-resolved tag matching that includes schema tags and hierarchy.

Useful tag APIs:

- `TagCatalog.Define(id)` declares a canonical catalog tag.
- `TagCatalog.Get(id)` reads a declared tag or throws.
- `TagCatalog.TryGet(id, out tag)` checks declaration.
- `TagCatalog.Contains(id)` checks whether an id is declared.
- `TagCatalog.GetHierarchy(id)` returns generated parent hierarchy tags.
- `ItemDefinition<TKey>.Tags` returns direct definition tag ids.
- `ItemSchema<TKey>.DirectTags` returns direct schema tag ids.

### Attribute Vocabulary

Attributes are declared in `ItemCatalog<TKey>.Attributes`. Schemas and definitions refer to attributes by string id and value type.

```csharp
catalog.Attributes.Define<int>("weight");
catalog.Attributes.Define<int>("damage");
catalog.Attributes.Define<bool>("stackable");
catalog.Attributes.Define<int>("max-stack");
```

| API | Meaning |
|---|---|
| `ItemCatalog<TKey>.Attributes` | Catalog-owned attribute vocabulary. |
| `AttributeCatalog.Define<T>(id)` | Declares an attribute id and value type. |
| `AttributeCatalog.Get<T>(id)` | Gets a declared attribute definition or throws. |
| `AttributeCatalog.TryGet<T>(id, out attributeDefinition)` | Gets a declared typed attribute definition, if present. |
| `AttributeCatalog.Contains<T>(id)` | Checks typed declaration. |
| `AttributeDefinition.Id` | Stable attribute id. |
| `AttributeDefinition.ValueType` | Declared .NET value type. |
| `IAttributeView.TryGet<T>(id, out value)` | Reads definition attributes by id and type. |

String constants are fine for project ergonomics, but the catalog declaration is the source of truth:

```csharp
private const string Weight = "weight";

catalog.Attributes.Define<int>(Weight);
```

The generic attribute-key type used internally by the package is not the public authoring API. User code should declare catalog attributes and read/write by string id and value type.

### Item Definitions

There are two related things called "item definition" in normal conversation:

- Item definition class: the C# type used to author a family of item definitions, such as `EquipmentDefinition`.
- Registered definition: the catalog-registered object instance with a stable id, such as the registered `"iron_sword"` definition.

Schemas belong to definition classes. Normal code does not pick an arbitrary schema for each registry entry; it creates an instance of the appropriate definition class, and that class passes its schema through protected constructor chaining.

Simple/default definitions use the base `ItemDefinition<TKey>` class and therefore use `ItemSchema<TKey>.Default`. Schema-specific item families use subclasses. Registered definitions are the instances registered into `catalog.Registry` before `catalog.Freeze()`.

| Term | What it is | Owns/contains |
|---|---|---|
| Item definition class | C# authoring type, such as `EquipmentDefinition`. | Class-owned schema and constructor logic. |
| Registered definition | Catalog-registered instance, such as `iron_sword`. | Stable id, schema from its class, direct tags, definition attributes. |
| Item instance | Runtime stack in an inventory. | Amount, metadata, ownership/layout state. |

```csharp
var apple = new ItemDefinition<string>("apple");
var potion = new ItemDefinition<string>("health_potion", "core:consumable");

catalog.Registry.Register(apple);
catalog.Registry.Register(potion);
catalog.Freeze();
```

These use the base ItemDefinition<string> class. The base class uses `ItemSchema<TKey>.Default`. `apple` and `potion` become registered definitions when passed to `catalog.Registry.Register(...)`. `potion` has a direct tag. No schema-specific attributes are required.

| Constructor | Availability | Meaning |
|---|---|---|
| `ItemDefinition<TKey>(id)` | public | Creates a base-class registered definition using the default schema. |
| `ItemDefinition<TKey>(id, params string[] tags)` | public | Creates a base-class registered definition with direct tags and the default schema. |
| `ItemDefinition<TKey>(id, schema)` | protected | Used by derived definition classes to pass their class-owned schema. |
| `ItemDefinition<TKey>(id, schema, IEnumerable<string>? tags)` | protected | Used by derived definition classes to pass their class-owned schema and direct tags. |

| Member | Meaning |
|---|---|
| `Id` | Stable registry/persistence identifier for a registered definition. |
| `Schema` | Schema determined by the definition class. |
| `Attributes` | Read-only definition attribute view. |
| `Tags` | Direct definition tag ids. |
| `Validate()` | Validates the registered definition against its schema. |
| `DefineAttribute<T>(string id, T value)` | Protected authoring helper for derived definition classes. |
| `DefineTag(...)` / `DefineTags(...)` | Protected string-tag authoring helpers for derived definition classes. |

A schema-specific item family is modeled as a derived definition class:

```csharp
sealed class EquipmentDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<EquipmentDefinition>("equipment")
            .RequireAttribute<int>("weight")
            .AddTag("core:equipment");

    public EquipmentDefinition(string id, int weight, params string[] tags)
        : base(id, Schema, tags)
    {
        DefineAttribute("weight", weight);
    }
}
```

```csharp
var sword = new EquipmentDefinition("iron_sword", weight: 5, "core:equipment.weapons");
var helmet = new EquipmentDefinition("iron_helmet", weight: 3, "core:equipment.armor");

catalog.Registry.Register(sword);
catalog.Registry.Register(helmet);
catalog.Freeze();
```

`EquipmentDefinition` is the item definition class. `sword` and `helmet` are registered definitions after registration. Both use the schema owned by `EquipmentDefinition`. Registration only receives the definition objects; schema choice has already happened inside the class constructor. The constructor accepts normal authoring data: id, weight, and optional direct tags. `DefineAttribute("weight", weight)` writes the required definition attribute for each registered definition instance.

The C# class owns the schema through `CreateFor<EquipmentDefinition>(...)`. The registered definitions created from that class inherit that schema because the class constructor calls the protected base constructor with `Schema`. Registry code does not assign a different schema per registered definition.

The protected schema constructor is used by the derived class, not by normal calling code. `RequireAttribute<int>("weight")` means every registered `EquipmentDefinition` must define a `weight` attribute. `AddTag("core:equipment")` gives all registered definitions of that class a schema-level tag.

Catalog freeze validates schema requirements against `catalog.Attributes` and validates registered definitions against their class-owned schemas. Catalog validation also rejects owned schemas used by unrelated definition types. Catalog validation rejects public constructors on registered concrete definition types that expose `ItemSchema<TKey>` parameters; schema choice should be class-owned, not a normal caller concern.

| API | Meaning |
|---|---|
| `ItemSchema<TKey>.Default` | Default schema for plain definitions. |
| `ItemSchema<TKey>.CreateFor<TDefinition>(id)` | Creates a schema owned by a definition type. |
| `ItemSchema<TKey>.Create(id)` | Creates an unowned schema for advanced shared-schema workflows. |
| `OwnerDefinitionType` | Definition type allowed to use an owned schema. |
| `WithParent(parent)` | Inherits parent schema requirements and tags. |
| `Require<T>(id)` | Requires a typed definition attribute. |
| `RequireAttribute<T>(id, inherited: true)` | Requires a typed definition attribute and controls child inheritance. |
| `AddTag(tag)` | Adds a direct schema tag. |
| `DirectAttributes` | Requirements declared directly on the schema. |
| `DirectTags` | Tags declared directly on the schema. |

### Class And Schema Hierarchies

Schema inheritance is meant to mirror the C# definition class hierarchy. If `WeaponDefinition` derives from `EquipmentDefinition`, `WeaponDefinition.Schema` should normally use `.WithParent(EquipmentDefinition.Schema)`.

The parent schema is taken directly from the parent definition class. The child class passes its own schema through protected constructor chaining. The registry still receives registered definition objects; it does not assign schemas. Child schemas inherit parent schema tags and parent attributes that were declared with `inherited: true`.

```csharp
public class EquipmentDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<EquipmentDefinition>("equipment")
            .RequireAttribute<int>("weight")
            .AddTag("core:equipment");

    protected EquipmentDefinition(
        string id,
        ItemSchema<string> schema,
        int weight,
        params string[] tags)
        : base(id, schema, tags)
    {
        DefineAttribute("weight", weight);
    }
}

public class WeaponDefinition : EquipmentDefinition
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<WeaponDefinition>("weapon")
            .WithParent(EquipmentDefinition.Schema)
            .RequireAttribute<int>("damage")
            .AddTag("core:equipment.weapons");

    public WeaponDefinition(string id, int weight, int damage, params string[] tags)
        : base(id, Schema, weight, tags)
    {
        DefineAttribute("damage", damage);
    }
}
```

```csharp
var ironSword = new WeaponDefinition(
    "iron_sword",
    weight: 5,
    damage: 12,
    "core:equipment.weapons.blades");

catalog.Registry.Register(ironSword);
catalog.Freeze();
```

`WeaponDefinition` is the C# class. `ironSword` is the registered definition after registration. `ironSword.Schema` is `WeaponDefinition.Schema`, and `WeaponDefinition.Schema.Parent` is `EquipmentDefinition.Schema`. The registry is not choosing a schema for `ironSword`.

`ironSword` must define `weight` because the parent `EquipmentDefinition.Schema` requires it and the requirement is inherited. It must define `damage` because `WeaponDefinition.Schema` requires it directly. Catalog tag resolution can see both `core:equipment` and `core:equipment.weapons`. Direct item tags such as `core:equipment.weapons.blades` are still separate direct definition tags.

`RequireAttribute<T>(id, inherited: false)` keeps a requirement on the declaring schema but prevents child schemas from inheriting it. The default is `true`.

Owned schema parents must follow the C# type relationship. A schema owned by `WeaponDefinition` can use a parent schema owned by `EquipmentDefinition`, because `WeaponDefinition` derives from `EquipmentDefinition`. It cannot use a parent schema owned by an unrelated definition class.

## Inventories And Managers

`InventoryManager<TKey>` creates inventories from an explicit shared catalog and default inventory policies. The catalog is required: register definitions in that catalog, freeze it, then create inventories from the manager.

| Constructor parameter | Meaning |
|---|---|
| `defaultStackResolver` | Default `IStackResolver<TKey>`. |
| `defaultCapacityPolicy` | Default `ICapacityPolicy<TKey>`. |
| `defaultLayout` | Layout template cloned for each inventory. |
| `catalog` | Required shared catalog. |
| `defaultRules` | Optional default rules cloned into each inventory. |

| Manager API | Meaning |
|---|---|
| `DefaultStackResolver` | Default stack resolver. |
| `DefaultCapacityPolicy` | Default capacity policy. |
| `DefaultLayout` | Default layout template. |
| `DefaultRules` | Default rule container. |
| `Catalog` | Shared catalog. |
| `Registry` | Shortcut to `Catalog.Registry`. |
| `CreateInventory()` | Creates from defaults. |
| `CreateInventory(stackResolver, layout, capacityPolicy, rules)` | Creates with overrides. |

| Inventory API | Meaning |
|---|---|
| `Manager` | Creating manager. |
| `Catalog` | Shared catalog. |
| `Items` | Owned item stacks in storage order. |
| `InstanceCount` | Number of stacks. |
| `TotalItemCount` | Sum of item amounts. |
| `Layout` | Placement/presentation layout. |
| `Rules` | Inventory-owned rule set. |
| `StackResolver` | Stack size resolver. |
| `CapacityPolicy` | Inventory capacity policy. |
| `Attributes` | Inventory-level attributes. |
| `Changed` | Event fired after successful mutations. |

| You want to... | Use |
|---|---|
| Add and throw on failure | `Add(...)` |
| Add conditionally | `TryAdd(...)` |
| Remove by definition | `RemoveByDefinition(...)`, `TryRemoveByDefinition(...)` |
| Move by UI position | `Move(...)`, `TryMove(...)` |
| Swap by UI position | `Swap(...)`, `TrySwap(...)` |
| Move items between compatible stacks | `MergeMove(...)`, `TryMergeMove(...)` |
| Sort visual placement | `SortLayout(...)`, `TrySortLayout(...)` |
| Query by definition | `Count`, `Contains`, `Find` |
| Query by tag | `FindByTag`, `CountByTag`, `ContainsAllTags` |
| Query by predicate | `FindWhere` |
| Get serialized object for persistence | `Serialize` |
| Get inventory object from persistence | `Deserialize` |

### Item Instances

`ItemInstance<TKey>` is a readable inventory-owned stack handle. Application code should inspect item instances, but it should not construct them directly or mutate item amounts directly.

Item instances are created by:

- inventory add operations;
- committed inventory transactions;
- committed transfers;
- deserialization;
- split, repack, compression, and internal rebuild flows.

Normal instance workflows are routed through inventory-owned APIs:

- Add items with `Add(...)` or `TryAdd(...)`.
- Remove items with `Remove(...)`, `TryRemove(...)`, `RemoveByDefinition(...)`, or `TryRemoveByDefinition(...)`.
- Merge compatible stacks with `MergeMove(...)` or `TryMergeMove(...)`.
- Transfer items through source-owned transfer APIs.
- Inspect stacks through `Items`, `Find(...)`, `FindByTag(...)`, and `FindWhere(...)`.
- Mutate per-instance data through `ItemInstance<TKey>.Metadata`.
- Change metadata on only part of a stack by splitting first or using `ItemInstance<TKey>.TrySplitAndSetMetadata(...)`.

`InventoryTransferEntry<TKey>` objects are produced by transfer-builder workflows for inspection and planning. They are not normal caller-constructed item instances.

## Stack Resolution

Stack resolvers decide maximum stack size. Adds first merge into compatible stacks selected by the layout, then create new stacks for remaining amount.

`ItemInstance<TKey>.IsStackCompatible(...)` requires the same definition id and structurally equal metadata. The stack resolver decides whether an add becomes amount deltas on existing stacks or new added entries.

| Resolver | Constructor | Behavior |
|---|---|---|
| `FixedSizeStackResolver<TKey>` | `(maxStack)` | Same max stack for every definition. |
| `ConditionalMaxStackResolver<TKey>` | `(stackableAttributeId, maxStack, missingAttributeIsStackable = false)` | Boolean definition attribute chooses max stack or 1. |
| `AttributeMaxStackResolver<TKey>` | `(maxStackAttributeId, missingAttributeMaxStack = null)` | Integer definition attribute supplies max stack size. |
| `MultipliedAttributeStackResolver<TKey>` | `(baseStackAttributeId, multiplier, missingAttributeBaseStack = null)` | Integer definition attribute supplies a base stack value, then resolver multiplier scales it per inventory. |

Attribute-driven stacking reads definition attributes, not metadata. Missing stack attributes do not fail catalog freeze by themselves. `ConditionalMaxStackResolver<TKey>` uses `missingAttributeIsStackable` when the boolean attribute is absent. `AttributeMaxStackResolver<TKey>` enters strict mode when `missingAttributeMaxStack == null`; strict mode fails at runtime when resolving a definition missing the max-stack attribute.

```csharp
catalog.Attributes.Define<bool>("stackable");
catalog.Attributes.Define<int>("max-stack");

var stackResolver = new ConditionalMaxStackResolver<string>(
    stackableAttributeId: "stackable",
    maxStack: 20,
    missingAttributeIsStackable: false);

var attributeResolver = new AttributeMaxStackResolver<string>(
    maxStackAttributeId: "max-stack",
    missingAttributeMaxStack: 1);
```

### Multiplied Attribute Stacking

`MultipliedAttributeStackResolver<TKey>` reads integer definition attributes, not metadata. The attribute should be declared in `catalog.Attributes`, and definition classes write it with `DefineAttribute(...)`.

The resolver computes max stack size as `floor(baseStack * multiplier)` with a minimum 1 result. The multiplier is resolver state, so inventories that share the same catalog can still choose different stack sizes. Missing base-stack attributes do not fail catalog freeze by themselves:

- with `missingAttributeBaseStack`, the fallback is used as the base stack;
- with `missingAttributeBaseStack: null`, strict mode fails at runtime when a missing attribute is resolved.

```csharp
private const string StackRatio = "stackRatio";

sealed class RatioStackDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<RatioStackDefinition>("ratio-stack")
            .RequireAttribute<int>(StackRatio);

    public RatioStackDefinition(string id, int stackRatio)
        : base(id, Schema)
    {
        DefineAttribute(StackRatio, stackRatio);
    }
}

catalog.Attributes.Define<int>(StackRatio);

var coin = new RatioStackDefinition("coin", 10);
var gem = new RatioStackDefinition("gem", 2);

catalog.Registry.Register(coin);
catalog.Registry.Register(gem);
catalog.Freeze();

var smallPouch = manager.CreateInventory(
    stackResolver: new MultipliedAttributeStackResolver<string>(StackRatio, multiplier: 1));

var warehouse = manager.CreateInventory(
    stackResolver: new MultipliedAttributeStackResolver<string>(StackRatio, multiplier: 5));
```

With this setup, coin base ratio 10 stacks to 10 in the small pouch and 50 in the warehouse. Gem base ratio 2 stacks to 2 in the small pouch and 10 in the warehouse.

Runtime tuning uses the stack resolver parameter API:

```csharp
warehouse.TrySetStackResolverParameter(
    "multiplier",
    2.0,
    InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks,
    out var error);
```

## Layouts And Layout Contexts

Layouts own placement. Normal user code asks the inventory to move, swap, add, sort, or query by layout context. Extension code implements the layout contract.

### Default layouts

| Layout | Context | Placement model | Empty positions |
|---|---|---|---|
| `EntryLayout<TKey>` | `EntryLayoutContext<TKey>` | ordered entries | no fixed gaps |
| `SlotLayout<TKey>` | `SlotLayoutContext<TKey>` | fixed slots | yes |
| `GridLayout<TKey>` | `GridLayoutContext<TKey>` | fixed single-cell grid | yes |
| `MultiCellGridLayout<TKey>` | `MultiCellGridLayoutContext<TKey>` | fixed grid, rectangular footprints | yes |
| `EquipmentLayout<TKey>` | `EquipmentLayoutContext<TKey>` | named slots with tag or definition restrictions | yes |
| `SectionedLayout<TKey>` | `SectionedLayoutContext<TKey>` | named sections with tag or definition restrictions | yes |

### Layout contexts

| Layout | Context meaning | Notes |
|---|---|---|
| `EntryLayout<TKey>` | entry index | Will adjust entries after the specified index |
| `SlotLayout<TKey>` | integer slot index | fixed empty positions |
| `GridLayout<TKey>` | `(x, y)` cell | one item per cell |
| `MultiCellGridLayout<TKey>` | anchor coordinate plus footprint | item may occupy many cells |
| `EquipmentLayout<TKey>` | named slot id | tag or definition restrictions decide compatibility |
| `SectionedLayout<TKey>` | section id plus slot index | section rules, definition restrictions, and slot position |

### Context builders

| Layout | Single context | Mapped context |
|---|---|---|
| Entry | `EntryLayoutContext<TKey>.Single(index)` | `.Map().Insert(addedEntryIndex, targetIndex).Build()` |
| Slot | `SlotLayoutContext<TKey>.Single(slot)` | `.Map().Add(addedEntryIndex, slot).Build()` |
| Grid | `GridLayoutContext<TKey>.Single(x, y)` | `.Map().Add(addedEntryIndex, x, y).Build()` |
| Multi-cell grid | `MultiCellGridLayoutContext<TKey>.Single(x, y[, anchor])` | `.Map().Add(addedEntryIndex, x, y[, anchor]).Build()` |
| Equipment | `EquipmentLayoutContext<TKey>.Single(slotId)` | `.Map().Add(addedEntryIndex, slotId).Build()` |
| Sectioned | `SectionedLayoutContext<TKey>.Single(sectionId, slotIndex)` | `.Map().Add(addedEntryIndex, sectionId, slotIndex).Build()` |

`ILayoutContext<TKey>.IsMapped` distinguishes direct operation contexts from transaction-level mappings. Mapped contexts target `InventoryTransaction<TKey>.Added` indices. Deltas and removals are simulated during layout validation but are not explicitly mapped.

Null layout context means auto-placement where the layout supports it.

### Default Layout Details

| Layout | Details | Persistent data |
|---|---|---|
| `EntryLayout<TKey>` | Storage-entry style visual order; insertion reorders entries; no fixed gaps. | `EntryLayoutPersistentData` |
| `SlotLayout<TKey>` | `SlotLayout<TKey>(int slotCount)` or persistent context constructor; first available slot auto-placement. | `SlotLayoutPersistentData` |
| `GridLayout<TKey>` | Width/height grid of single-cell items; `GridPlacementOrder` controls scan order. | `GridLayoutPersistentData` |
| `MultiCellGridLayout<TKey>` | Width/height grid with rectangular `GridFootprint`; rejects overlap; supports anchors. | `MultiCellGridLayoutPersistentData` |
| `EquipmentLayout<TKey>` | Named `EquipmentSlot<TKey>` entries with tag or definition restrictions; first compatible empty slot auto-placement; no sorting. | `EquipmentLayoutPersistentData` |
| `SectionedLayout<TKey>` | Named `SectionDefinition<TKey>` sections with slot counts and tag or definition restrictions, such as hotbar/tools/bag. | `SectionedLayoutPersistentData` |

### Layout Repack

`TryRepackLayout(out error)` compacts the current layout without changing parameters. `RepackLayout()` is the throwing wrapper for expected-success workflows.

Repack reads items in current layout order, then places those same item instances again using normal auto-placement. In slot and grid-style layouts, that removes empty spaces before or between items while keeping visible order. Repack preserves item instances and `Inventory<TKey>.Items` storage order.

Repack is not sorting and does not use a comparer. It can fail if the current contents cannot be represented by normal auto-placement in the current layout. A successful visible repack fires a full-refresh change event with moved payloads and no configuration change. If no visible placement changes, no event is fired.

```csharp
inventory.TryAdd(sword, out _, context: SlotLayoutContext<string>.Single(4));
inventory.TryAdd(apple, out _, context: SlotLayoutContext<string>.Single(1));
inventory.TryAdd(potion, out _, context: SlotLayoutContext<string>.Single(3));

var repacked = inventory.TryRepackLayout(out var error);
```

Before repack, the visible order is `apple`, `potion`, `sword`. After repack, a slot layout places them at slots `0`, `1`, and `2`, while maintaining the same order, so it would still be `apple`, `potion`, `sword`. `Inventory<TKey>.Items` remains in add/storage order.

### Equipment And Sectioned Compatibility

Equipment slots and sectioned sections can restrict placement by tag ids, definition ids, or both. Tag restrictions use catalog-resolved tags. Definition restrictions compare `ItemDefinition<TKey>.Id` with `EqualityComparer<TKey>.Default`, not item definition object reference.

`AllowedDefinitions` is a convenience authoring option when the canonical registered definition objects are in scope; the layout stores their ids. `AllowedDefinitionIds` is useful when ids are already available from data or generated content.

> Layout compatibility answers "can this item be represented here?" It does not replace inventory rules, capacity, stack resolution, or catalog registration.

| Configuration | Accepts |
|---|---|
| Tags only | Items satisfying all required tags. |
| Definitions only | Items whose definition id is explicitly allowed. |
| Tags and definitions | Items satisfying all required tags OR matching an allowed definition id. |
| Neither | Any item that otherwise passes inventory validation. |

Tag satisfaction is resolved through `ItemCatalog<TKey>.Satisfies(...)`, so hierarchical tags follow the catalog tag model documented earlier. Definition restrictions are still subject to the registered-definition invariant: layout compatibility compares ids, but inventory contents must still use canonical registered definitions.

No restrictions means the slot or section is unrestricted by layout compatibility and accepts any item that otherwise passes inventory validation.

Equipment example:

```csharp
var weapon = "gear:weapon";
var armor = "gear:armor";

catalog.Tags.Define(weapon);
catalog.Tags.Define(armor);

var sword = new ItemDefinition<string>("iron_sword", weapon);
var familyHeirloom = new ItemDefinition<string>("family_heirloom");
var helmet = new ItemDefinition<string>("helmet", armor);

catalog.Registry.Register(sword);
catalog.Registry.Register(familyHeirloom);
catalog.Registry.Register(helmet);
catalog.Freeze();

var equipment = new EquipmentLayout<string>(
    new EquipmentSlot<string>(
        "main-hand",
        new EquipmentSlotOptions<string>
        {
            RequiredTags = new[] { weapon },
            AllowedDefinitions = new[] { familyHeirloom }
        }),
    new EquipmentSlot<string>("head", armor));
```

`iron_sword` fits `main-hand` because it satisfies `gear:weapon`. `family_heirloom` also fits `main-hand` because its definition id is explicitly allowed. `helmet` does not fit `main-hand`, but fits `head`.

The same restriction can be authored by id:

```csharp
AllowedDefinitionIds = new[] { "family_heirloom" }
```

Sectioned example:

```csharp
var tool = "gear:tool";

catalog.Tags.Define(tool);

var axe = new ItemDefinition<string>("axe", tool);
var lockpick = new ItemDefinition<string>("lockpick");
var apple = new ItemDefinition<string>("apple");

catalog.Registry.Register(axe);
catalog.Registry.Register(lockpick);
catalog.Registry.Register(apple);
catalog.Freeze();

var layout = new SectionedLayout<string>(
    new SectionDefinition<string>(
        "tools",
        2,
        new SectionDefinitionOptions<string>
        {
            RequiredTags = new[] { tool },
            AllowedDefinitions = new[] { lockpick }
        }),
    new SectionDefinition<string>("bag", 2));
```

`axe` auto-places into `tools` because it satisfies `gear:tool`. `lockpick` auto-places into `tools` because it is explicitly allowed. `apple` cannot fit `tools`, so it auto-places into unrestricted `bag` when space is available.

Multi-cell support uses `GridFootprint`, `GridAnchor`, `IGridFootprintProvider<TKey>`, and `AttributeGridFootprintProvider<TKey>`.

```csharp
private const string Width = "footprint-width";
private const string Height = "footprint-height";

catalog.Attributes.Define<int>(Width);
catalog.Attributes.Define<int>(Height);

var footprintProvider = new AttributeGridFootprintProvider<string>(Width, Height);

var grid = new MultiCellGridLayout<string>(
    width: 8,
    height: 5,
    footprintProvider,
    placementOrder: GridPlacementOrder.RowMajor,
    defaultAnchor: GridAnchor.TopLeft);
```

`FootprintDefinition` in examples is an example subclass of `ItemDefinition<string>`. It writes width and height definition attributes in its constructor. The package does not ship this class because doing so would impose a second base item class and a specific attribute naming scheme. Real projects should define their own footprint-bearing item classes or definition authoring pipeline.

`MultiCellGridLayout<TKey>` compact sorting is deterministic heuristic packing, not guaranteed optimal bin packing.

## Capacity Policies

Capacity policies answer non-spatial resource limits, that is to say limitations in capacity not related to the availablility of structural space in the inventory, but the inventory as a whole.

| Policy | Constructor | Behavior | Common use |
|---|---|---|---|
| `UnlimitedCapacityPolicy<TKey>` | none | Always accepts. | Debug/default inventory. |
| `MaxTotalItemAmountCapacityPolicy<TKey>` | `maxTotalItemAmount` | Limits total item amount. | Small backpack. |
| `WeightCapacityPolicy<TKey>` | weight attribute id, max weight, missing-value mode | Limits summed item weight. | Encumbrance or crate load. |

```csharp
catalog.Attributes.Define<int>("weight");

var backpackCapacity = new WeightCapacityPolicy<string>(
    "weight",
    maxWeight: 25,
    treatMissingWeightAsZero: true);
```

## Rules

Rules answer semantic allow/deny constraints. They are separate from capacity and layout placement.

| Rule area | Meaning |
|---|---|
| `IRulePolicy<TKey>` | Validates normalized semantic transactions. |
| `IInventoryStructuralRulePolicy<TKey>` | Validates structural transaction details. |
| `IInventorySnapshotRulePolicy<TKey>` | Validates the final whole-inventory state. |
| `InventorySnapshotRulePolicy<TKey>` | Base class for snapshot rules. |
| `InventoryRuleSnapshot<TKey>` | Final-state snapshot used by snapshot rules. |
| `RuleContainer<TKey>` | Ordered, enableable rule collection with stable ids. |
| `IdentifiedRulePolicy<TKey>` | Rule wrapper with explicit id. |
| `IdentifiedSnapshotRulePolicy<TKey>` | Snapshot rule wrapper with explicit id. |

Built-in rules:

| Rule | Checks |
|---|---|
| `RequireAnyTagRule<TKey>` | Added items satisfy at least one tag. |
| `RequireAllTagsRule<TKey>` | Added items satisfy all tags. |
| `OnlyAllowItemsRule<TKey>` | Added definitions are in an allowed list. |
| `RequireAttributeRule<TKey, TValue>` | Definition has attribute. |
| `AttributeEqualsRule<TKey, TValue>` | Definition attribute equals value. |
| `AttributeOneOfValuesRule<TKey, TValue>` | Definition attribute is one of allowed values. |
| `AttributePredicateRule<TKey, TValue>` | Definition attribute satisfies predicate. |
| `ItemPredicateRule<TKey>` | Definition satisfies predicate. |
| `RequireMetadataKeyRule<TKey>` | Added item metadata contains key. |
| `RequireMetadataRule<TKey>` | Metadata key equals value. |
| `RequireMetadataOneOfValuesRule<TKey>` | Metadata value is in allowed set. |
| `MetadataRangeRule<TKey, T>` | Metadata value is in inclusive range. |
| `UniqueItemRule<TKey>` | Limits instances per item. |
| `MaxUniqueItemsRule<TKey>` | Limits unique definitions. |
| `OrRule<TKey>` | Any nested rule accepts. |
| `NotRule<TKey>` | Nested rule rejects. |

```csharp
var lightItemsOnly = new AttributePredicateRule<string, int>(
    "weight",
    weight => weight <= 6,
    "Expected weight to be 6 or less");
```

### Inventory-Owned Rule Mutation

Inventories clone default or override `RuleContainer<TKey>` instances at creation. Runtime rule mutation should go through the inventory, not a shared rule container. Proposed rule sets are validated against current contents before commit. Rejected changes are atomic and do not mutate the inventory rule set.

| API | Behavior |
|---|---|
| `TrySetRule(id, rule, out error)` | Add/replace rule after validating current contents. |
| `TrySetRule(id, rule, priority, enabled, out error)` | Add/replace with explicit priority/enabled state. |
| `TryRemoveRule(id, out error)` | Remove rule after validation. |
| `TrySetRuleEnabled(id, enabled, out error)` | Toggle enabled state after validation. |
| `TrySetRulePriority(id, priority, out error)` | Change priority after validation. |
| `SetRule`, `RemoveRule`, `SetRuleEnabled`, `SetRulePriority` | Throwing wrappers. |

```csharp
if (!inventory.TrySetRule("equipment-only", new RequireAnyTagRule<string>("core:equipment"), out var error))
{
    // Current contents would violate the proposed rule set.
}
```

## Runtime Parameter Mutation

Stack resolvers, capacity policies, and layouts can expose runtime parameters. Inventory code changes those parameters through `Inventory<TKey>`, not by mutating shared component instances directly.

Parameter changes are proposed as replacement components, validated against current contents, and then committed atomically. Rejected changes leave the inventory unchanged and fire no event. Successful changes appear in `InventoryChangedEventArgs<TKey>.ConfigurationChanged`.

```text
TrySet...Parameter(...)
  -> validate parameter id and supported actions
  -> create proposed component
  -> validate current contents or requested rebuild action
  -> commit replacement component
  -> fire ConfigurationChanged
```

The normal APIs are grouped by the component being changed:

- Stack resolver: use `TrySetStackResolverParameter(parameterId, value, out error)` for preserve-only changes, or `TrySetStackResolverParameter(parameterId, value, actions, out error)` when the current stacks may need splitting, compatible-stack compression, or layout repack.
- Capacity policy: use `TrySetCapacityPolicyParameter(parameterId, value, out error)` to validate current contents against a proposed policy value. Capacity policy changes do not own stack shape or layout placement, so actions must be `InventoryParameterMutationActions.None`.
- Layout: use `TrySetLayoutParameter(parameterId, value, out error)` when current placements must remain valid, or `TrySetLayoutParameter(parameterId, value, actions, out error)` with `RepackLayout` when automatic placement may rebuild the layout.
- Throwing wrappers: `SetStackResolverParameter`, `SetCapacityPolicyParameter`, and `SetLayoutParameter` are for expected-success workflows.

Mutation target rules:

- Stack resolver changes can preserve the existing stack shape, or use `RepackLayout`, `SplitOversizedStacks`, `CompressCompatibleStacks`, or combinations.
- Capacity policy changes are validation-only and reject mutation actions.
- Layout parameter changes can preserve current placements or use `RepackLayout`; they reject stack split/compression actions.
- Successful layout parameter changes require a full refresh. Stack resolver changes require a full refresh when they repack; split/compression without repack can be represented with normal change payloads.

### Mutation Actions

Preserve-only is the default. Add mutation actions only when the proposed parameter value cannot be applied while keeping the current stack shape and layout placement.

Pass action flags directly:

- `InventoryParameterMutationActions.None` preserves current stack shape and layout placement.
- `InventoryParameterMutationActions.RepackLayout` re-places current stack entries in current layout order using normal auto-placement, compacting empty spaces where the layout can.
- `InventoryParameterMutationActions.SplitOversizedStacks` splits stacks that exceed the proposed max stack size.
- `InventoryParameterMutationActions.CompressCompatibleStacks` merges compatible stack amounts into fuller earlier stacks.

Combine actions with `|` when a stack resolver change needs more than one rebuild behavior.

### Preserve-Only Parameter Changes

```csharp
var slotUpgrade = inventory.TrySetLayoutParameter("slotCount", 3, out var slotUpgradeError);
var stackUpgrade = inventory.TrySetStackResolverParameter("maxStack", 10, out var stackUpgradeError);
var lowerCapacity = inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 10, out var capacityError);
```

Increasing `slotCount` can preserve existing slots and succeeds when current placements still exist. Increasing `maxStack` can preserve existing stack entries and simply changes future stack resolution. Lowering capacity succeeds only if current contents fit the proposed policy; lowering capacity below current contents is rejected atomically.

### Layout Repack

```csharp
var shrinkWithoutRepack = inventory.TrySetLayoutParameter(
    "slotCount",
    2,
    out var shrinkError);

var shrinkWithRepack = inventory.TrySetLayoutParameter(
    "slotCount",
    3,
    InventoryParameterMutationActions.RepackLayout,
    out var repackError);
```

Without repack, the proposed layout must preserve current placements. With `InventoryParameterMutationActions.RepackLayout`, the inventory reads items in current layout order, then places them again using the proposed layout's normal auto-placement. In a slot layout, that removes empty slots before or between items while keeping the same visible item order. Repack can fail if normal placement cannot represent all current stacks. Successful parameter-mutation repack requires full UI refresh and emits `ConfigurationChanged`.

`RepackLayout` is not a sort operation and does not use item comparer ordering. It uses presentation order instead of the order exposed by `Inventory.Items`.

For compaction without changing a parameter, use `TryRepackLayout(out error)` or `RepackLayout()`. Direct layout repack uses the same current-order auto-placement model, but it does not emit `ConfigurationChanged`.

For sectioned layouts, string ids are generated from the section id:

```csharp
inventory.TrySetLayoutParameter(
    "section:bag.slotCount",
    6,
    InventoryParameterMutationActions.RepackLayout,
    out var error);
```

### Splitting Oversized Stacks

```csharp
var splitDowngrade = inventory.TrySetStackResolverParameter(
    "maxStack",
    4,
    InventoryParameterMutationActions.SplitOversizedStacks,
    out var splitError);
```

Use `SplitOversizedStacks` when reducing max stack size. A stack of `10` becomes `4, 4, 2` when the new max is `4`. Split chunks preserve metadata. Split-only does not force layout repack, but it still needs enough valid layout placement for the additional stacks. If placement cannot represent the split chunks, the change is rejected atomically.

### Compressing Compatible Stacks

```csharp
var compressUpgrade = inventory.TrySetStackResolverParameter(
    "maxStack",
    25,
    InventoryParameterMutationActions.CompressCompatibleStacks,
    out var compressError);
```

Use `CompressCompatibleStacks` when increasing max stack size and you want compatible stacks merged. Four stacks of `10` can become `25, 15` when the new max is `25`. Compatibility still requires same definition id and structurally equal metadata. Compatible-stack compression does not force layout repack.

### Combined Stack Actions

```csharp
var lowerWithSplitAndRepack = inventory.TrySetStackResolverParameter(
    "maxStack",
    5,
    InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks,
    out var lowerError);
```

```csharp
var upgradeWithCompressionAndRepack = inventory.TrySetStackResolverParameter(
    "maxStack",
    25,
    InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.CompressCompatibleStacks,
    out var compressionError);
```

```csharp
var fullRebuild = inventory.TrySetStackResolverParameter(
    "maxStack",
    25,
    InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.CompressCompatibleStacks,
    out var fullRebuildError);
```

Use `RepackLayout | SplitOversizedStacks` for lowering max stack with split and repack. Use `RepackLayout | CompressCompatibleStacks` for increasing max stack with compatible-stack compression and repack. Use all three actions when a stack resolver change may require both split and compression plus layout repack. Any action set containing `RepackLayout` requires full UI refresh on success.

### Events And Refresh

Successful parameter mutation produces `InventoryChangedEventArgs<TKey>.ConfigurationChanged`. Layout parameter changes set `RequiresFullRefresh`, and repack/rebuild changes set `RequiresFullRefresh`. Preserve-only capacity changes do not require full refresh. Preserve-only stack changes that only replace the resolver do not require full refresh. Split or compression without repack can use normal add/modify/remove/moved payloads and affected contexts.

```csharp
inventory.Changed += (_, args) =>
{
    foreach (var change in args.ConfigurationChanged)
    {
        if (change.RequiresFullRefresh || args.RequiresFullRefresh)
        {
            RefreshWholeInventory();
            return;
        }
    }

    RefreshContexts(args.AffectedLayoutContexts);
};
```

### Built-In Parameter Ids

Parameter ids are strings. Treat them like part of your application configuration: centralize constants where practical, especially for generated ids such as section layout parameters.

| Component | Parameter | Type | Meaning |
|---|---|---|---|
| `FixedSizeStackResolver<TKey>` | `maxStack` | `int` | Fixed max stack size. |
| `ConditionalMaxStackResolver<TKey>` | `maxStack` | `int` | Max size when stackable attribute is true. |
| `ConditionalMaxStackResolver<TKey>` | `missingAttributeIsStackable` | `bool` | Missing stackability attribute fallback. |
| `AttributeMaxStackResolver<TKey>` | `missingAttributeMaxStack` | `int` or `null` | Missing max-stack fallback; `null` enables strict mode. |
| `MultipliedAttributeStackResolver<TKey>` | `multiplier` | `double` | Multiplies each definition's base stack attribute. |
| `MultipliedAttributeStackResolver<TKey>` | `missingAttributeBaseStack` | `int` or `null` | Fallback base stack; `null` enables strict mode. |
| `MaxTotalItemAmountCapacityPolicy<TKey>` | `maxTotalItemAmount` | `int` | Maximum total item amount. |
| `WeightCapacityPolicy<TKey>` | `maxWeight` | `double` | Maximum total item weight. |
| `WeightCapacityPolicy<TKey>` | `treatMissingWeightAsZero` | `bool` | Missing weight handling. |
| `SlotLayout<TKey>` | `slotCount` | `int` | Number of slots. |
| `GridLayout<TKey>` | `width` | `int` | Grid width. |
| `GridLayout<TKey>` | `height` | `int` | Grid height. |
| `GridLayout<TKey>` | `placementOrder` | `GridPlacementOrder` | Auto-placement/sort scan order. |
| `MultiCellGridLayout<TKey>` | `width` | `int` | Grid width. |
| `MultiCellGridLayout<TKey>` | `height` | `int` | Grid height. |
| `MultiCellGridLayout<TKey>` | `placementOrder` | `GridPlacementOrder` | Auto-placement/repack scan order. |
| `MultiCellGridLayout<TKey>` | `defaultAnchor` | `GridAnchor` | Default explicit-placement anchor. |
| `SectionedLayout<TKey>` | `section:{sectionId}.slotCount` | `int` | Slot count for one named section. |

For `SectionedLayout<TKey>`, the parameter id is generated from the section id. A section with id `bag` exposes `"section:bag.slotCount"`. A section with id `tools` exposes `"section:tools.slotCount"`.

## Transactions

Transactions represent inventory-local structural changes. Builders stage and build; `Inventory<TKey>` commits.

```text
Builder mutates simulation
  -> Build() / TryBuild(...)
  -> optional layout context mapping
  -> capacity/rules/layout validation
  -> inventory commit
  -> one Changed event
```

### Transaction Objects

| API | Meaning |
|---|---|
| `InventoryTransaction<TKey>.From(inventory)` | Creates a simulation-backed builder for the inventory. |
| `Inventory` | Target inventory. |
| `AmountDeltas` | Amount changes for existing storage entries. |
| `Removed` | Removed storage entries and instances. |
| `Added` | New item instances and optional layout contexts. |
| `IsApplied` | Whether the transaction has already been committed. |
| `IsEmpty` | Whether the transaction has no structural changes. |
| `ForInventory(target)` | Copies structural data for another target inventory. |

### Transaction Builders

| API | Meaning |
|---|---|
| `TryAdd(definition, out error, amount, context)` | Stage an add in the builder simulation. |
| `TryAdd(definition, amount, context, metadata, out error)` | Stage an add with metadata. |
| `TryRemove(instance, out error, amount)` | Stage removal from an item instance. |
| `TryRemoveAtStorageIndex(index, out error, amount)` | Stage removal by storage index. |
| `TryRemoveByDefinition(definition, amount, ignoreMetadata, out error)` | Stage removal by definition. |
| `Build()` | Build a structural transaction for inspection or later commit. |
| `TryBuild(placementContext, out transaction, out error)` | Build after applying a transaction-level layout context. |
| `IsEmpty` | Whether the staged result has no structural changes. |

Preferred workflow: create a builder with `InventoryTransaction<TKey>.From(inventory)`, stage changes, then commit the builder through the same inventory.

```csharp
var builder = InventoryTransaction<string>.From(backpack);

builder.TryAdd(apple, out var appleError, amount: 5);
builder.TryRemoveByDefinition(coin, amount: 10, ignoreMetadata: true, out var removeError);

var committed = backpack.TryCommitTransaction(builder, out var commitError);
```

For multi-add placement, pass a transaction-level mapped context to the inventory commit method:

```csharp
var placement = SlotLayoutContext<string>.Map()
    .Add(0, 2)
    .Add(1, 3)
    .Build();

var committed = backpack.TryCommitTransaction(builder, placement, out var error);
```

Transaction-level mapped contexts target `InventoryTransaction<TKey>.Added` indices. `Build()` and `TryBuild(...)` are useful when code needs to inspect the structural transaction before committing. `TryCommitTransaction(builder, placementContext, out error)` is the normal direct path when no inspection is needed. `CommitTransaction(...)` variants throw when failure is unexpected.

Move and swap are inventory-level layout operations, not transaction-builder operations. Use `Move`, `TryMove`, `Swap`, and `TrySwap` for deliberate layout movement.

Commit APIs:

| API | Meaning |
|---|---|
| `TryCommitTransaction(builder, out error)` | Build and commit a builder. |
| `TryCommitTransaction(builder, placementContext, out error)` | Build and commit with transaction-level placement. |
| `TryCommitTransaction(transaction, out error)` | Commit an inspected transaction. |
| `TryCommitTransaction(transaction, placementContext, out error)` | Commit an inspected transaction with placement. |
| `CommitTransaction(...)` | Throwing wrappers for expected-success workflows. |

`NormalizedInventoryTransaction<TKey>` is the grouped semantic view used by capacity and rules.

## Transfers

Transfers move items between inventories that share the same manager or catalog. `InventoryTransfer.From(source)` creates a builder, but source inventories commit builders and own one-shot transfer actions.

### Transfer Builders

Transfer builders stage outgoing-only removals from a source inventory. Target additions are created during source-owned commit.

| API | Meaning |
|---|---|
| `InventoryTransfer.From(source)` | Creates an outgoing-only builder for the source inventory. |
| `Source` | Source inventory items are planned to leave. |
| `Entries` | Snapshot of outgoing planned entries. |
| `IsEmpty` | Whether there are no planned outgoing entries. |
| `TryRemove(item, amount, out error)` | Stage removal from a source item instance. |
| `TryRemoveAtStorageIndex(index, amount, out error)` | Stage removal by source storage index. |
| `TryRemoveByDefinition(definition, amount, ignoreMetadata, out error)` | Stage removal by definition. |

| Source inventory API | Meaning |
|---|---|
| `CanCommitTransfer(builder, target, out error)` | Validate staged transfer without committing. |
| `CanCommitTransfer(builder, target, targetContext, out error)` | Validate with target placement context. |
| `TryCommitTransfer(builder, target, out error)` | Commit staged transfer. |
| `TryCommitTransfer(builder, target, targetContext, out error)` | Commit with target placement context. |
| `CommitTransfer(builder, target)` | Throwing commit wrapper. |
| `CommitTransfer(builder, target, targetContext)` | Throwing commit wrapper with placement. |

```csharp
var builder = InventoryTransfer.From(backpack);
builder.TryRemove(backpack.Find(herb).Single(), 3, out var herbError);
builder.TryRemove(backpack.Find(bottle).Single(), 1, out var bottleError);

var moved = backpack.TryCommitTransfer(builder, craftingInput, targetContext: null, out var error);
```

One `targetContext` is shared for incoming entries unless it is a mapped context. Mapped target contexts can place multiple incoming entries by `InventoryTransferBuilder<TKey>.Entries` order.

```csharp
var transfer = InventoryTransfer.From(backpack);
transfer.TryRemove(backpack.Items[0], 5, out _);
transfer.TryRemove(backpack.Items[1], 1, out _);

var context = SlotLayoutContext<string>.Map()
    .Add(0, 2)
    .Add(1, 3)
    .Build();

var moved = backpack.TryCommitTransfer(transfer, chest, context, out var error);
```

### Source-Owned Transfer Actions

| API | Behavior |
|---|---|
| `CanTransferTo(target, item, amount, targetContext, out error)` | Validate a single item transfer. |
| `TryTransferTo(target, item, amount, targetContext, out error)` | Transfer one item amount. |
| `TransferTo(target, item, amount, targetContext = null)` | Throwing single item transfer. |

```csharp
var movedLogs = backpack.TryTransferTo(
    craftingInput,
    backpack.Find(oakLog).Single(),
    amount: 3,
    targetContext: null,
    out var error);
```

All-or-nothing bulk transfers:

| API | Behavior |
|---|---|
| `TryMoveAllTo(target, targetContext, out error)` | Move all source contents as one all-or-nothing operation. |
| `TryMoveWhereTo(target, predicate, targetContext, out error)` | Move all matching contents all-or-nothing. |
| `TryMoveByTagTo(target, tagId, targetContext, out error)` | Move all items satisfying one catalog-resolved tag. |
| `TryMoveAllTagsTo(target, tagIds, targetContext, out error)` | Move all items satisfying every provided tag. |

Best-effort transfers:

| API | Behavior |
|---|---|
| `TryTransferMaximumTo(target, item, requestedAmount, targetContext, out transferredAmount, out error)` | Transfer largest valid amount up to requested amount. |
| `TryMoveMaximumWhereTo(target, predicate, targetContext, out transferredAmount, out error)` | Move as much matching item amount as possible in source storage order. |
| `TryMoveMaximumByTagTo(target, tagId, targetContext, out transferredAmount, out error)` | Move as much tag-matching amount as possible. |

```csharp
var moved = chest.TryMoveMaximumByTagTo(
    backpack,
    "loot:treasure",
    targetContext: null,
    out var movedAmount,
    out var error);
```

Swaps:

| API | Behavior |
|---|---|
| `TrySwapItemsWithInventory(other, sourceItem, otherItem, sourceTargetContext, otherTargetContext, out error)` | Swap complete stacks. |
| `TrySwapItemsWithInventory(other, sourceItem, sourceAmount, otherItem, otherAmount, sourceTargetContext, otherTargetContext, out error)` | Swap item amounts. |
| `TrySwapWithInventory(other, sourceTargetContext, otherTargetContext, out error)` | Swap all contents. |

```csharp
var backpackContext = SlotLayoutContext<string>.Map().Add(0, 2).Build();
var chestContext = SlotLayoutContext<string>.Map().Add(0, 1).Build();

var swapped = backpack.TrySwapWithInventory(
    chest,
    sourceTargetContext: backpackContext,
    otherTargetContext: chestContext,
    out var error);
```

Context direction matters:

- `targetContext` is where incoming items land in the target inventory.
- `sourceTargetContext` is where the other inventory's incoming items land in the source inventory during swaps.
- `otherTargetContext` is where the source inventory's incoming items land in the other inventory during swaps.

Best-effort transfer APIs, such as `TryTransferMaximumTo`, `TryMoveMaximumWhereTo`, and `TryMoveMaximumByTagTo`, have simpler placement semantics than fully planned transfers. Use a transfer builder plus mapped target context when precise multi-entry placement matters.

## Sorting

Sorting changes layout placement, not storage order. Layouts own the interpretation of sort contexts, and sorting emits moved event payloads marked as sort results.

Use sorting when you want comparer-driven or layout-specific ordering. Use layout repack when you want to keep the current visible order but remove empty spaces through normal auto-placement.

| API | Meaning |
|---|---|
| `TrySortLayout(comparer, out error)` | Sort with an `IComparer<ItemInstance<TKey>>`. |
| `TrySortLayout(comparison, out error)` | Sort with a comparison delegate. |
| `TrySortLayout(sortContext, out error)` | Sort with a layout-specific context. |
| `SortLayout(...)` | Throwing wrappers for expected-success workflows. |
| `ItemSortContext<TKey>` | General item-comparer sort context. |
| `ItemSortContext<TKey>.FromComparison(...)` | Creates an item sort context from a comparison delegate. |
| `MultiCellGridSortContext<TKey>` | Multi-cell grid item-order or compact-space sort context. |

| Layout | Sort support |
|---|---|
| `EntryLayout<TKey>` | Reorders layout entry order by item comparer. |
| `SlotLayout<TKey>` | Moves items into sorted slot order; empty slots remain layout-owned. |
| `GridLayout<TKey>` | Moves single-cell items according to grid scan order. |
| `SectionedLayout<TKey>` | Sorts within section compatibility constraints by finding compatible target slots. |
| `MultiCellGridLayout<TKey>` | Can sort by item order or compact footprints. |
| `EquipmentLayout<TKey>` | Sorting unsupported. |

Slot layout sort example:

```csharp
inventory.TryAdd(sword, out _, context: SlotLayoutContext<string>.Single(4));
inventory.TryAdd(apple, out _, context: SlotLayoutContext<string>.Single(3));
inventory.TryAdd(potion, out _, context: SlotLayoutContext<string>.Single(1));

var sorted = inventory.TrySortLayout(
    (a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id),
    out var error);
```

The sorted layout places `apple`, `potion`, and `sword` according to the layout's slot scan order. `Inventory.Items` storage order remains the order created by add and commit operations. UI refresh should use `AffectedLayoutContexts` or `Moved`.

Multi-cell grid sorting has two useful modes:

```csharp
private const string Width = "sort-example-width";
private const string Height = "sort-example-height";

catalog.Attributes.Define<int>(Width);
catalog.Attributes.Define<int>(Height);

var provider = new AttributeGridFootprintProvider<string>(Width, Height);

var grid = new MultiCellGridLayout<string>(
    width: 3,
    height: 3,
    footprintProvider: provider);
```

```csharp
inventory.TrySortLayout(
    MultiCellGridSortContext<string>.ByItems(
        (a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id)),
    out var itemOrderError);

inventory.TrySortLayout(
    MultiCellGridSortContext<string>.Compact(
        (a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id)),
    out var compactError);
```

`MultiCellGridSortContext<TKey>.ByItems(...)` uses `MultiCellGridSortPriority.ItemOrder`: it prioritizes comparer order, then repacks in that order. `MultiCellGridSortContext<TKey>.Compact(...)` uses `MultiCellGridSortPriority.SpaceEfficiency`: it prioritizes deterministic space-efficient footprint packing and uses the comparer only as a tie-breaker. Compact sorting is heuristic and deterministic, not optimal bin packing.

Multi-cell item contexts can affect multiple occupied cells, so UI refresh should be prepared for multiple affected contexts per item.

Move events generated by sorting have `ItemMoved<TKey>.IsSortResult == true`, so UI code can skip drag/drop-style movement animations for sort-generated moves.

```csharp
inventory.Changed += (_, args) =>
{
    foreach (var move in args.Moved)
    {
        if (move.IsSortResult)
            continue;

        // Animate deliberate drag/drop movement here.
    }
};
```

## Metadata

`InstanceMetadata` stores per-item-instance key/object data. It is intentionally loose: there is no metadata catalog and no metadata schema.

Use definition attributes for shared item-universe data. Use instance metadata for runtime variation such as quality rolls, owner, durability state, quest flags, generated serial numbers, and crafting input/output annotations. Metadata values are compared structurally for stack compatibility, and metadata is serialized per item.

Dictionary containers are copied by `ToDictionary()`, `TryReplace(...)`, `Replace(...)`, and event snapshots, but stored values are not deep-cloned.

| Metadata state | Mutation behavior |
|---|---|
| Detached metadata | Mutates directly after local API checks. |
| Inventory-owned metadata | Routes through the owning inventory for validation and events. |

Metadata becomes inventory-owned when its `ItemInstance<TKey>` belongs to an inventory. Inventory-owned metadata mutation validates the proposed metadata result against rules, capacity, layout, stack constraints, and stack compatibility. Rejected mutations are atomic and do not fire events. Successful direct metadata mutations fire `Inventory.Changed` with `MetadataChanged`.

Read/state APIs:

| API | Meaning |
|---|---|
| `IsEmpty` | Whether metadata has no values. |
| `TryGet<T>(key, out value)` | Read a typed value. |
| `AsReadOnly()` | Get a read-only view. |
| `StructuralEquals(other)` | Compare for stack compatibility. |
| `RestoreMetadata(data)` | Restore serialized metadata. |
| `ToDictionary()` | Copy into a mutable dictionary. |

Conditional mutation APIs:

| API | Meaning |
|---|---|
| `TryAdd(key, value, out error)` | Add only if missing. |
| `TrySet(key, value, out error)` | Add or replace. |
| `TryChange(key, value, out error)` | Replace only if present. |
| `TryRemove(key, out error)` | Remove only if present. |
| `TryClear(out error)` | Clear all metadata. |
| `TryReplace(values, out error)` | Replace entire dictionary. |
| `TryTransform(transform, out error)` | Mutate a proposed clone. |

Throwing wrappers:

| API | Use when |
|---|---|
| `Add(key, value)` | Missing key is expected. |
| `Set(key, value)` | Add/replace should succeed. |
| `Change(key, value)` | Existing key is expected. |
| `Remove(key)` | Existing key is expected. |
| `Clear()` | Clear should succeed. |
| `Replace(values)` | Whole-dictionary replacement should succeed. |
| `Transform(transform)` | Callback transformation should succeed. |

Expected-success metadata mutation:

```csharp
var gemStack = inventory.Items.Single();

gemStack.Metadata.Set("quality", "common");
gemStack.Metadata.Change("quality", "polished");
```

Throwing wrappers are appropriate when the workflow expects success. If inventory-owned validation rejects the mutation, wrappers throw `InvalidOperationException`.

Conditional metadata mutation:

```csharp
if (!gemStack.Metadata.TryChange("quality", "polished", out var qualityError))
{
    // Existing key missing, or the owning inventory rejected the proposed state.
}

if (!gemStack.Metadata.TryRemove("quality", out var removeError))
{
    // The key was missing, or a rule such as RequireMetadataKeyRule rejected the result.
}
```

Rules can reject metadata changes because the proposed metadata is validated through the inventory:

```csharp
inventory.TrySetRule(
    "requires-quality",
    new RequireMetadataKeyRule<string>("quality"),
    out var ruleError);
```

Once the rule is active, removing `quality` from inventory-owned metadata can be rejected. This is why metadata mutation is inventory-routed instead of just dictionary mutation.

Transform and replace workflows:

```csharp
gemStack.Metadata.TryTransform(
    metadata =>
    {
        metadata.Set("inspected", true);
        metadata.Set("quality", "flawless");
    },
    out var transformError);
```

```csharp
gemStack.Metadata.TryReplace(
    new Dictionary<string, object>
    {
        ["quality"] = "polished",
        ["owner"] = "player"
    },
    out var replaceError);
```

`TryTransform` receives a proposed clone. The proposed result is validated before commit when inventory-owned. `TryReplace` copies the dictionary container, not nested object values.

### Partial-Stack Metadata

Stack metadata applies to the whole stack, not to each unit inside a stack. Directly setting metadata on a stack changes the whole stack. To mark only part of a stack, split first or use `TrySplitAndSetMetadata`.

```csharp
var split = gemStack.TrySplitAndSetMetadata(
    amount: 2,
    key: "quest-item",
    value: true,
    out var questStack,
    out var splitError);
```

The original stack keeps the remaining amount. The split stack receives copied existing metadata plus the new key/value. The operation is routed through the owning inventory and fails if the item instance is detached. Split-and-set emits added/modified style payloads, not necessarily a `MetadataChanged` payload.

The throwing wrapper returns the split metadata stack:

```csharp
var questStack = gemStack.SplitAndSetMetadata(2, "quest-item", true);
```

## Events And UI Integration

Subscribe to `Inventory<TKey>.Changed` for UI refresh, gameplay reactions, and audit logs.

| Event args member | Meaning |
|---|---|
| `Added` | New stacks. |
| `Removed` | Removed stacks. |
| `Modified` | Amount changes. |
| `Moved` | Layout movement, including sort-generated moves. |
| `Swapped` | Layout swaps. |
| `MetadataChanged` | Metadata mutation payloads. |
| `ConfigurationChanged` | Runtime stack/capacity/layout changes. |
| `Cleared` | Inventory clear/replace behavior. |
| `AffectedLayoutContexts` | Easiest targeted UI refresh path. |
| `RequiresFullRefresh` | Full UI rebuild is safest. |

Event DTOs include `ItemAdded<TKey>`, `ItemRemoved<TKey>`, `ItemModified<TKey>`, `ItemMoved<TKey>`, `ItemSwapped<TKey>`, `ItemMetadataChanged<TKey>`, and `InventoryConfigurationChanged<TKey>`.

| DTO detail | Meaning |
|---|---|
| `ItemMetadataChanged<TKey>.Instance` | Item instance after mutation. |
| `ItemMetadataChanged<TKey>.Index` | Storage index of the changed item. |
| `ItemMetadataChanged<TKey>.BeforeMetadata` | Snapshot before mutation. |
| `ItemMetadataChanged<TKey>.AfterMetadata` | Snapshot after mutation. |
| `ItemMetadataChanged<TKey>.LayoutContexts` | Contexts occupied by changed item. |
| `ItemMetadataChanged<TKey>.LayoutContext` | Single context when exactly one exists. |
| `InventoryConfigurationChanged<TKey>.Kind` | Stack resolver, capacity policy, or layout. |
| `InventoryConfigurationChanged<TKey>.RequiresFullRefresh` | Whether targeted refresh is insufficient. |
| `ItemMoved<TKey>.IsSortResult` | True for sort-generated movement. |

Use `AffectedLayoutContexts` as the simplest targeted refresh path. Use semantic groups for richer behavior such as animations, gameplay reactions, and audit logs. Multi-cell events can include multiple contexts per item.

```csharp
inventory.Changed += (_, args) =>
{
    if (args.RequiresFullRefresh)
    {
        RebuildInventoryView();
        return;
    }

    foreach (var context in args.AffectedLayoutContexts)
        RefreshCell(context);

    foreach (var change in args.MetadataChanged)
        RefreshMetadataBadges(change.LayoutContexts);

    foreach (var moved in args.Moved)
    {
        if (moved.IsSortResult)
            continue;

        PlayMoveAnimation(moved.FromContexts, moved.ToContexts);
    }
};
```

Metadata-specific UI handling should account for both direct metadata mutation and split-and-set workflows:

```csharp
inventory.Changed += (_, args) =>
{
    foreach (var change in args.MetadataChanged)
    {
        foreach (var context in change.LayoutContexts)
            RefreshMetadataBadge(context, change.AfterMetadata);
    }

    foreach (var added in args.Added)
        RefreshItemWithMetadata(added.LayoutContexts, added.Instance.Metadata.AsReadOnly());

    foreach (var modified in args.Modified)
        RefreshAmount(modified.AfterLayoutContexts);
};
```

Direct metadata changes appear in `MetadataChanged`. Split-and-set may appear as `Added` plus `Modified`, so UI code should handle both if it displays metadata.

## Persistence

`Inventory<TKey>.Serialize()` produces `SerializedInventory<TKey>`:

| DTO | Contents |
|---|---|
| `SerializedInventory<TKey>` | `Items` and `LayoutData`. |
| `SerializedItem<TKey>` | `DefinitionId`, `Amount`, and `Metadata`. |

`Inventory<TKey>.Deserialize(data, strict: false)` restores contents into the active inventory and layout. Catalog/registry definitions must exist for deserialization. Strict mode throws on restore failures. Layout data must match the active layout type.

During deserialization, each serialized `DefinitionId` is resolved through `Manager.Registry.Resolve(...)`. That means registered migrations can load older save data into current canonical definitions. Missing unmigrated ids still fail resolution, strict mode still controls restore failure behavior, and layout data compatibility is separate from definition id migration.

Layout persistent data types include `EntryLayoutPersistentData`, `SlotLayoutPersistentData`, `GridLayoutPersistentData`, `MultiCellGridLayoutPersistentData`, `EquipmentLayoutPersistentData`, and `SectionedLayoutPersistentData`.

## Examples And Common Workflows

Example-focused NUnit tests live under `tests/Examples/<FeatureArea>/` and write `.txt` artifacts under the test work directory. Low-level NUnit tests remain the source for exact behavior.

| Example folder | Covers |
|---|---|
| `AttributeDrivenStacking` | Definition-attribute stack policies. |
| `Capacity` | Amount and weight limits. |
| `CrossInventoryTransfer` | Source-owned transfers, transfer builders, mapped target contexts, maximum loot movement. |
| `EquipmentLayout` | Loadout slots, tag restrictions, definition restrictions, and rejection. |
| `Events` | UI refresh, metadata updates, configuration changes, and movement payloads. |
| `GridLayout` | Manual placement, auto order, mapped transactions/transfers. |
| `InventoryPolicyMutation` | Runtime stack/capacity/layout parameter changes, validation-only changes, repack, split, and compatible-stack compression. |
| `InventoryQueries` | Definition/tag queries. |
| `InventoryRuleMutation` | Inventory-owned runtime rule changes. |
| `ItemUniverseFoundation` | Definitions, tags, schemas, attributes. |
| `LayoutContexts` | Direct and mapped contexts. |
| `MetadataMutation` | Inventory-owned metadata mutation, rules rejecting metadata changes, and partial-stack split-and-set workflows. |
| `MultiCellGridLayout` | Footprints, anchors, overlap rejection. |
| `SectionedLayout` | Hotbar/tools/bag sections with tag and definition restrictions. |
| `Sorting` | Item sorting, affected UI contexts, sort-result moves, and compact multi-cell sorting. |

## Usage Pitfalls And Caveats

These are the usage details most likely to affect application code after the main workflow sections above.

### Catalog And Identity

- Catalogs must be frozen with `ItemCatalog<TKey>.Freeze()` before inventory creation.
- Inventories only accept canonical registered definitions from their manager catalog.
- Deserialization requires current registered definitions or explicit registry migrations for obsolete ids.

### Layout, Order, And UI

- `Inventory<TKey>.Items` is storage order, not UI order.
- Layout contexts and `AffectedLayoutContexts` are the simplest UI refresh path.
- Layout sorting changes placement, not `Inventory<TKey>.Items` order.
- `TryRepackLayout(...)` preserves current layout order and uses normal auto-placement; it is not a sort.
- Null layout context means auto-placement where the layout supports it.
- Best-effort transfer APIs have simpler placement semantics than fully planned transfer builders with mapped target contexts.
- `MultiCellGridLayout<TKey>` compact sorting is deterministic heuristic packing, not guaranteed optimal bin packing.

### Runtime Mutation

- Runtime rule, stack resolver, capacity policy, and layout changes should go through `Inventory<TKey>`.
- Preserve-only parameter mutation is the default and can reject changes that require split, compatible-stack compression, or repack.
- Capacity policy parameter changes do not support mutation actions.
- Layout parameter changes support `RepackLayout` only.
- Stack split/compression does not imply layout repack unless `RepackLayout` is included.

### Metadata

- Stack metadata applies to the whole stack.
- Partial-stack metadata changes require splitting or `TrySplitAndSetMetadata(...)`.
- Direct metadata changes emit `MetadataChanged`; split-and-set metadata workflows can emit `Added` and `Modified` instead.

# Extending The System

Extension code implements the contracts that normal inventory workflows call into. Application code should still prefer `Inventory<TKey>` APIs for mutation; extension implementations provide the behavior those APIs validate and apply.

Extension points in this README are organized by what they own:

- Definition classes own schema-backed item authoring.
- Stack resolvers own maximum stack size.
- Capacity policies own non-spatial inventory limits.
- Rules own semantic, structural, or final-state constraints.
- Layouts own placement state, contexts, sorting, movement, and layout persistence.
- Parameterized components expose stable runtime parameter ids and create replacement component instances.

Extension implementations should be deterministic, avoid mutating inventory state during validation, return consumer-facing error messages, and preserve the registered-definition invariant by working with inventory-owned item instances and catalog-registered definitions.

## Custom Stack Resolver

Implement `IStackResolver<TKey>` when the built-in resolvers cannot express the stack rule. The resolver returns the maximum amount allowed in one compatible stack. Stack compatibility still requires the same definition id and structurally equal metadata; the resolver only answers stack size.

A resolver can inspect the inventory, the item instance or prototype being evaluated, registered definition attributes, and metadata if the project intentionally wants metadata-dependent stack sizing. Definition attributes are read through string ids. Keep resolver behavior deterministic because it affects add/merge behavior, transactions, transfers, deserialization, split/repack rebuilds, and runtime parameter validation.

Return positive max stack sizes. If definition data cannot be represented as a valid max stack, throw `InvalidOperationException` with a clear message that includes the definition id and attribute id where practical.

```csharp
using System;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Stacking;

public sealed class DefinitionMaxStackResolver<TKey> : IStackResolver<TKey>
{
    private readonly string _attributeId;
    private readonly int _fallbackMaxStack;

    public DefinitionMaxStackResolver(string attributeId, int fallbackMaxStack)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be empty.", nameof(attributeId));
        if (fallbackMaxStack <= 0)
            throw new ArgumentOutOfRangeException(nameof(fallbackMaxStack));

        _attributeId = attributeId;
        _fallbackMaxStack = fallbackMaxStack;
    }

    public int ResolveMaxStackSize(
        Inventory<TKey> inventory,
        ItemInstance<TKey> instance)
    {
        if (instance.Definition.Attributes.TryGet<int>(_attributeId, out var maxStack))
        {
            if (maxStack <= 0)
            {
                throw new InvalidOperationException(
                    $"Item definition '{instance.Definition.Id}' attribute '{_attributeId}' must be greater than zero.");
            }

            return maxStack;
        }

        return _fallbackMaxStack;
    }
}
```

Use it like any other stack resolver:

```csharp
catalog.Attributes.Define<int>("max-stack");

var manager = new InventoryManager<string>(
    new DefinitionMaxStackResolver<string>("max-stack", fallbackMaxStack: 1),
    new UnlimitedCapacityPolicy<string>(),
    new EntryLayout<string>(),
    catalog);
```

This example is close to the built-in `AttributeMaxStackResolver<TKey>` and is included to show the extension contract shape. Prefer the built-in resolver when it fits.

## Parameterized Stack Resolvers

Implement `IParameterizedStackResolver<TKey>` when a custom resolver supports inventory-owned runtime tuning. `Parameters` describes the supported stable string ids. `TryCreateWithParameter(...)` validates the proposed value and returns a replacement resolver instance. Do not mutate the current resolver instance in place.

Inventory-owned mutation APIs validate current contents before committing the replacement. Stack resolver parameter changes can use `InventoryParameterMutationActions.None`, `InventoryParameterMutationActions.RepackLayout`, `InventoryParameterMutationActions.SplitOversizedStacks`, `InventoryParameterMutationActions.CompressCompatibleStacks`, or valid combinations. Capacity and layout action limits are covered in the runtime mutation usage section.

```csharp
using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Stacking;

public sealed class TunableDefinitionMaxStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition(
                "fallbackMaxStack",
                typeof(int),
                "Fallback max stack when a definition does not define the max-stack attribute.")
        };

    private readonly string _attributeId;
    private readonly int _fallbackMaxStack;

    public TunableDefinitionMaxStackResolver(string attributeId, int fallbackMaxStack)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be empty.", nameof(attributeId));
        if (fallbackMaxStack <= 0)
            throw new ArgumentOutOfRangeException(nameof(fallbackMaxStack));

        _attributeId = attributeId;
        _fallbackMaxStack = fallbackMaxStack;
    }

    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    public int ResolveMaxStackSize(
        Inventory<TKey> inventory,
        ItemInstance<TKey> instance)
    {
        if (instance.Definition.Attributes.TryGet<int>(_attributeId, out var maxStack))
        {
            if (maxStack <= 0)
            {
                throw new InvalidOperationException(
                    $"Item definition '{instance.Definition.Id}' attribute '{_attributeId}' must be greater than zero.");
            }

            return maxStack;
        }

        return _fallbackMaxStack;
    }

    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IStackResolver<TKey>? resolver,
        out string? error)
    {
        resolver = null;

        if (parameterId != "fallbackMaxStack")
        {
            error = $"Parameter '{parameterId}' is not supported.";
            return false;
        }

        if (value is not int fallbackMaxStack)
        {
            error = "Parameter 'fallbackMaxStack' expects value type 'Int32'.";
            return false;
        }

        if (fallbackMaxStack <= 0)
        {
            error = "Fallback max stack must be greater than zero.";
            return false;
        }

        resolver = new TunableDefinitionMaxStackResolver<TKey>(
            _attributeId,
            fallbackMaxStack);

        error = null;
        return true;
    }
}
```

Runtime tuning still goes through the inventory:

```csharp
var changed = inventory.TrySetStackResolverParameter(
    "fallbackMaxStack",
    10,
    InventoryParameterMutationActions.RepackLayout |
        InventoryParameterMutationActions.CompressCompatibleStacks,
    out var error);
```

The parameter id is a string; centralize constants in application code if desired. The replacement resolver is committed only if the inventory can validate the current contents and requested mutation actions. This example shows the parameterized contract; if only the fallback needs tuning, the built-in `AttributeMaxStackResolver<TKey>` already supports `missingAttributeMaxStack`.

## Parameterized Components

Stack resolvers, capacity policies, and layouts share the same runtime-parameter pattern: expose definitions for stable string ids, then create replacement component instances when a parameter changes.

| Contract | Meaning |
|---|---|
| `IParameterizedStackResolver<TKey>` | Resolver exposes parameters and creates replacement resolvers. |
| `IParameterizedCapacityPolicy<TKey>` | Capacity policy exposes parameters and creates replacement policies. |
| `IParameterizedInventoryLayout<TKey>` | Layout exposes parameters and creates replacement layouts. |
| `InventoryParameterDefinition` | Parameter id, value type, and description. |

Normal code should call the inventory-owned `TrySet...Parameter(...)` APIs. The parameterized interfaces are for custom component implementations.

## Custom Capacity Policy

Capacity policies should model inventory-wide, non-spatial capacity resources such as total amount, total weight, total bulk, volume, energy, or load. Item-specific limits such as "no more than 999 coins" are usually rules, not capacity policies, because they constrain a gameplay concept rather than the inventory's shared capacity.

Prefer `CanApply(...)` for real transaction validation because it receives the current inventory plus a normalized semantic transaction. `CanAdd(...)` remains available for one-off custom checks, but inventory mutation normally formulates transactions first. `NormalizedInventoryTransaction<TKey>` exposes `Added` and `Removed` entries grouped as `(definition, metadata, amount)`; amount deltas are normalized into added or removed semantic amounts, so custom policies should not look for a separate modified list or depend on storage order.

```csharp
using System;
using System.Collections.Generic;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;

public sealed class BulkCapacityPolicy<TKey> : ICapacityPolicy<TKey>
{
    private readonly string _bulkAttributeId;
    private readonly int _maxBulk;
    private readonly int _missingBulk;

    public BulkCapacityPolicy(string bulkAttributeId, int maxBulk, int missingBulk = 0)
    {
        if (string.IsNullOrWhiteSpace(bulkAttributeId))
            throw new ArgumentException("Bulk attribute id cannot be empty.", nameof(bulkAttributeId));
        if (maxBulk < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBulk));
        if (missingBulk < 0)
            throw new ArgumentOutOfRangeException(nameof(missingBulk));

        _bulkAttributeId = bulkAttributeId;
        _maxBulk = maxBulk;
        _missingBulk = missingBulk;
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        if (!TryCalculateCurrentBulk(inventory, out var currentBulk, out error))
            return false;
        if (!TryCalculateBulk(transaction.Added, out var addedBulk, out error))
            return false;
        if (!TryCalculateBulk(transaction.Removed, out var removedBulk, out error))
            return false;

        var projectedBulk = currentBulk + addedBulk - removedBulk;
        if (projectedBulk > _maxBulk)
        {
            error = $"Capacity exceeded: {projectedBulk}/{_maxBulk} bulk.";
            return false;
        }

        error = null;
        return true;
    }

    public bool CanAdd(
        Inventory<TKey> inventory,
        ItemInstance<TKey> instance,
        out string? error)
    {
        if (!TryCalculateCurrentBulk(inventory, out var currentBulk, out error))
            return false;
        if (!TryGetBulk(instance.Definition, out var itemBulk, out error))
            return false;

        var projectedBulk = currentBulk + itemBulk * instance.Amount;
        if (projectedBulk > _maxBulk)
        {
            error = $"Capacity exceeded: {projectedBulk}/{_maxBulk} bulk.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryCalculateCurrentBulk(
        Inventory<TKey> inventory,
        out int bulk,
        out string? error)
    {
        bulk = 0;

        foreach (var item in inventory.Items)
        {
            if (!TryGetBulk(item.Definition, out var itemBulk, out error))
                return false;

            bulk += itemBulk * item.Amount;
        }

        error = null;
        return true;
    }

    private bool TryCalculateBulk(
        IReadOnlyList<(ItemDefinition<TKey> definition, InstanceMetadata? metadata, int amount)> entries,
        out int bulk,
        out string? error)
    {
        bulk = 0;

        foreach (var (definition, _, amount) in entries)
        {
            if (!TryGetBulk(definition, out var itemBulk, out error))
                return false;

            bulk += itemBulk * amount;
        }

        error = null;
        return true;
    }

    private bool TryGetBulk(
        ItemDefinition<TKey> definition,
        out int bulk,
        out string? error)
    {
        if (definition.Attributes.TryGet<int>(_bulkAttributeId, out bulk))
        {
            if (bulk < 0)
            {
                error = $"Item definition '{definition.Id}' attribute '{_bulkAttributeId}' cannot be negative.";
                return false;
            }

            error = null;
            return true;
        }

        bulk = _missingBulk;
        error = null;
        return true;
    }
}
```

This is similar in shape to built-in attribute-driven capacity policies such as `WeightCapacityPolicy<TKey>`. Prefer built-ins when they fit. Custom policies should return consumer-facing errors, and invalid definition data can reject validation. Capacity policies participate in adds, transactions, transfers, metadata mutation, deserialization restore, layout/stack rebuild validation, and capacity parameter mutation.

Parameterized capacity policies use the same replacement-instance pattern described in the "Parameterized Components" section. Implement `IParameterizedCapacityPolicy<TKey>` when a custom capacity policy exposes runtime parameters: `Parameters` exposes stable string ids through `InventoryParameterDefinition`, and `TryCreateWithParameter(...)` validates a proposed value and returns a replacement `ICapacityPolicy<TKey>`. Do not mutate the current policy instance in place. Runtime tuning should go through `Inventory<TKey>.TrySetCapacityPolicyParameter(...)`; capacity policy parameter changes are validation-only and reject mutation actions other than `InventoryParameterMutationActions.None`.

## Custom Rule

Rules own semantic, structural, or final-state constraints that are not capacity resources. Pick the narrowest validation shape that expresses the rule:

| Rule shape | Use when | Validation input |
|---|---|---|
| `IRulePolicy<TKey>` | The rule can be checked from semantic added/removed amounts. | `NormalizedInventoryTransaction<TKey>` |
| `IInventoryStructuralRulePolicy<TKey>` | The rule depends on storage indices, removed storage positions, or item instance count. | `InventoryTransaction<TKey>` |
| `InventorySnapshotRulePolicy<TKey>` / `IInventorySnapshotRulePolicy<TKey>` | The final projected inventory state is easiest to validate. | `InventoryRuleSnapshot<TKey>` |

`RuleContainer<TKey>` evaluates enabled rules by descending priority and then insertion order. Snapshot-capable rules receive a lazy projected snapshot. Structural rules run when a structural transaction is available. Rule errors are wrapped with the rule id and type, and inventory-owned rule mutation methods validate current contents before committing changes.

Use `IdentifiedRulePolicy<TKey>` or `IdentifiedSnapshotRulePolicy<TKey>` when a reusable rule needs a stable runtime id different from its own `Id`, or when it is added through `RuleContainer<TKey>` APIs that wrap rules by id.

A limit for one item definition is a rule, not capacity:

```csharp
using System;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Rules;

public sealed class MaxDefinitionAmountRule<TKey> : InventorySnapshotRulePolicy<TKey>
{
    private readonly ItemDefinition<TKey> _definition;
    private readonly int _maxAmount;

    public MaxDefinitionAmountRule(
        ItemDefinition<TKey> definition,
        int maxAmount,
        string id)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (maxAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAmount));
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Rule id cannot be empty.", nameof(id));

        _maxAmount = maxAmount;
        Id = id;
    }

    protected override bool CanApplyWithSnapshot(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        InventoryRuleSnapshot<TKey> snapshot,
        out string? error)
    {
        var projectedAmount = snapshot.GetQuantity(_definition);
        if (projectedAmount > _maxAmount)
        {
            error = $"Cannot carry more than {_maxAmount} of '{_definition.Id}'.";
            return false;
        }

        error = null;
        return true;
    }
}
```

Use it through inventory-owned rule mutation:

```csharp
inventory.TrySetRule(
    "economy:max-coins",
    new MaxDefinitionAmountRule<string>(coin, 999, "economy:max-coins"),
    out var ruleError);
```

The snapshot handles adds, removals, and amount deltas as the final projected quantity. The same pattern works for constraints such as at most one quest key or no more than three equipped charms.

For semantic added-entry checks, use `IRulePolicy<TKey>` directly:

```csharp
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Rules;

public sealed class RequireQuestItemMetadataRule : IRulePolicy<string>
{
    private readonly string _requiredQuestId;

    public RequireQuestItemMetadataRule(string requiredQuestId)
    {
        _requiredQuestId = requiredQuestId;
    }

    public string Id => "RequireQuestItemMetadata";

    public bool CanApply(
        Inventory<string> inventory,
        NormalizedInventoryTransaction<string> transaction,
        out string? error)
    {
        foreach (var (_, metadata, amount) in transaction.Added)
        {
            if (amount <= 0)
                continue;

            if (metadata == null ||
                !metadata.TryGet<string>("questId", out var questId) ||
                questId != _requiredQuestId)
            {
                error = $"Added items must have questId '{_requiredQuestId}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
```

This checks semantic added groups, not storage positions. Metadata is grouped structurally in the normalized transaction. If the rule needs to validate existing inventory state too, use a snapshot rule.

Common rule choices:

| Constraint | Recommended rule shape |
|---|---|
| Added items must have metadata. | `IRulePolicy<TKey>` over `transaction.Added`. |
| Added items must satisfy a tag. | `IRulePolicy<TKey>` using `inventory.Catalog.Satisfies(definition, tagId)`. |
| Inventory may contain at most N of one definition. | `InventorySnapshotRulePolicy<TKey>`. |
| Inventory may contain at most N unique definitions. | Snapshot rule, similar to built-in `MaxUniqueItemsRule<TKey>`. |
| Rule depends on storage index or item-instance count. | `IInventoryStructuralRulePolicy<TKey>`. |

Prefer built-in tag, attribute, metadata, uniqueness, predicate, and composition rules when they fit.

## Custom Layout

A custom layout owns presentation state: how inventory storage indices are represented as layout positions. It does not own item lifetime, stack amounts, catalog registration, capacity, rules, or metadata. Normal application code should call `Inventory<TKey>` methods; layout methods are extension contracts the inventory calls while validating and applying operations.

| Concern | Owner |
|---|---|
| Item instances and storage order | `Inventory<TKey>` |
| Placement positions and UI-addressable contexts | layout |
| Stack merging limits | stack resolver |
| Inventory-wide limits | capacity policy |
| Semantic constraints | rules |
| Metadata values | `InstanceMetadata`, validated through inventory when owned |

### Layout Method Lifecycle

`IInventoryLayout<TKey>` methods fall into a few groups:

- Query/render methods: `GetPositionCount`, `GetAddressableContexts`, `GetItemAt`, `GetContextsForStorageIndex`, and `TryGetContextForStorageIndex`.
- Formulation helpers: `GetMergeCandidates`, `TryApplyPlacementContext`, and `CanAcceptNewItem`.
- Transaction validation: `CanSatisfyPlacement`.
- Layout operations: `TryMove`, `TrySwap`, and `TrySort`.
- Inventory mutation notifications: `OnItemAdded`, `OnItemRemoved`, and `OnInventoryCleared`.
- Persistence and cloning: `GetPersistentData`, `RestorePersistentData`, and `Clone`.

| Method group | Called when | Key responsibility |
|---|---|---|
| Query/render | UI, events, inventory helpers | Resolve layout positions without mutating state. |
| Formulation | Adds, builders, transfers | Select merge candidates and map placement instructions. |
| Validation | Before commit | Prove the full structural transaction can be represented. |
| Operations | Move, swap, sort | Mutate placement only. |
| Notifications | After storage mutation | Keep layout state aligned with storage indices. |
| Persistence/cloning | Save/restore/simulation | Preserve or copy layout-owned state. |

### Storage Indices And Layout State

Most layouts store storage indices, not item references. `Inventory<TKey>.Items` is ownership/storage order, not UI order. Layout state maps positions to storage indices or storage indices to positions.

A single-position layout usually has zero or one context per storage index. Multi-position layouts can return multiple contexts for one storage index. `GetItemAt(...)` resolves a context by looking up the storage index and returning `inventory.Items[index]`. `GetContextsForStorageIndex(...)` is the inverse mapping and is used heavily by event payloads and targeted UI refresh.

A fixed shelf layout might use this state model:

```csharp
private readonly List<int?> _shelves;
```

`null` means an empty shelf. A value means the shelf displays `inventory.Items[value]`. The layout must never reorder `Inventory<TKey>.Items` to change presentation order.

### Custom Layout Contexts

Custom layout contexts are layout-owned. `ILayoutContext<TKey>.IsMapped` is the shared signal used by inventory orchestration:

- `IsMapped == false`: direct context for one operation position, such as one shelf, slot, entry index, grid cell, equipment slot, or section slot.
- `IsMapped == true`: transaction-level context that maps `InventoryTransaction<TKey>.Added` indices to layout-owned placements.

Example context for a fixed shelf layout:

```csharp
public sealed class ShelfLayoutContext<TKey> : ILayoutContext<TKey>
{
    public int ShelfIndex { get; }
    public bool IsMapped { get; }
    public IReadOnlyDictionary<int, int> AddedEntryShelves { get; }

    private ShelfLayoutContext(int shelfIndex)
    {
        ShelfIndex = shelfIndex;
        AddedEntryShelves = new Dictionary<int, int>();
    }

    private ShelfLayoutContext(IReadOnlyDictionary<int, int> addedEntryShelves)
    {
        ShelfIndex = -1;
        IsMapped = true;
        AddedEntryShelves = addedEntryShelves;
    }

    public static ShelfLayoutContext<TKey> Single(int shelfIndex)
        => new ShelfLayoutContext<TKey>(shelfIndex);

    public static ShelfLayoutContext<TKey> Map(
        IReadOnlyDictionary<int, int> addedEntryShelves)
        => new ShelfLayoutContext<TKey>(
            new Dictionary<int, int>(addedEntryShelves));
}
```

`Single(...)` is a direct context for one shelf. `Map(...)` is a transaction-level context for multiple added entries. `AddedEntryShelves` keys are `InventoryTransaction<TKey>.Added` indices. `AddedEntryShelves` values are layout-owned shelf indices. `ShelfIndex = -1` is only a sentinel for mapped contexts and must not be used as a real shelf. Builders are often nicer than raw dictionaries for public APIs, as shown by built-in context builders.

Context validation should reject negative position indices, mapped added-entry indices outside `transaction.Added`, duplicate target positions when the layout cannot place two items there, and context instances from other layout types. Use clear errors such as `Invalid context type.`. When a transaction-level mapped context conflicts with an existing per-added-entry context, reject the mapping before validation.

### Direct And Mapped Context Flow

For a single add with direct context, the caller passes something like `ShelfLayoutContext<TKey>.Single(2)`. If the formulated transaction has one `Added` entry, `TryApplyPlacementContext(...)` copies that context onto added entry `0`.

For a merge delta with direct context, the add may become an amount delta into an existing stack instead of a new added entry. In that case, the direct context should verify that the delta index matches the item at that context. If it does not match, reject with a clear error.

For multi-add or mapped placement, caller code or builders provide a mapped context. `TryApplyPlacementContext(...)` copies mapped direct contexts to matching `transaction.Added` entries. Unmapped added entries keep their existing context or auto-place with `null`.

Mapping must preserve `AmountDeltas` and `Removed` exactly. It should return a new `InventoryTransaction<TKey>` with copied structural data and updated `Added` contexts, and it should not mutate live layout state.

### Placement Validation

For a fixed-position custom layout, `CanSatisfyPlacement(...)` should validate against a simulated final state:

1. Validate all `AmountDeltas` indices are in range.
2. Build the set of removed storage indices and validate each is in range.
3. Clone layout placement state into a local simulated map.
4. Apply removals to the simulated map before additions.
5. When applying a removal, clear positions equal to the removed index and decrement stored indices greater than the removed index.
6. Compute the first future storage index:

```csharp
var futureStorageIndex = inventory.Items.Count - removedIndices.Count;
```

7. For each `transaction.Added` entry, use a direct context when supplied or otherwise find the automatic placement target.
8. Reject invalid context type, out-of-range position, duplicate explicit target, occupied target, or no available automatic position.
9. Store `futureStorageIndex + addedIndex` into the simulated map.
10. Return success only when the full final state can be represented.

Amount deltas do not need new layout positions. Validation must not mutate live layout state. Error messages should name the failed placement concept, not internal implementation details.

### Merge Candidates

`GetMergeCandidates(...)` returns storage indices that inventory should consider for stack merging. With a direct context, return only the item at that context when present. With null context, return candidates in the layout's presentation order. With invalid context type or out-of-range context, return no candidates.

Candidate order can affect which compatible stack receives an add first, so keep it deterministic.

### Move And Swap

`TryMove(...)` and `TrySwap(...)` mutate layout placement only. They should validate context type and range. Move should reject empty source, occupied target if the layout does not support overwrite, and same-position moves if considered invalid. Swap should reject invalid or empty endpoints according to layout semantics.

These methods should not change item amounts, metadata, stack resolver state, capacity state, rules, or `Inventory<TKey>.Items`. Inventory-level APIs capture before/after contexts and emit events.

The remaining layout responsibilities are persistence, cloning, sorting, and event context reporting. They are described below because they determine whether a custom layout works correctly with save/restore, transaction simulations, and UI updates.

### Layout Persistence

`GetPersistentData()` returns layout-owned state. The returned object implements `ILayoutPersistentData`, whose `GetPersistentContext()` method exposes raw layout context data for persistence pipelines. Normal application code should usually use inventory persistence APIs rather than calling layout persistence methods directly.

Persistent data should contain enough shape metadata to verify compatibility on restore. Fixed-position layouts usually persist storage-index maps. Entry-style layouts persist presentation order. Grid-style layouts persist dimensions and cell maps. Named layouts such as equipment and sectioned layouts persist slot or section ids so incompatible layout definitions can be rejected. Prefer plain serializable values and collections where practical.

| Layout shape | Persistent state usually needed |
|---|---|
| Entry/order layout | Storage-index order list. |
| Fixed slot layout | Slot-to-storage-index map. |
| Grid layout | Width, height, placement order, and cell map. |
| Equipment layout | Slot ids and slot-to-storage-index map. |
| Sectioned layout | Section ids, slot counts, and flattened slot map. |
| Multi-cell grid layout | Width, height, placement order, default anchor, and cell map. |

`RestorePersistentData(...)` should accept only its own persistent data type and reject incompatible shape data with `InvalidOperationException`. Compatibility checks should include dimensions, slot ids, section ids, slot counts, placement order, anchors, or any other layout-defining value needed to interpret the stored map. Restore should replace layout-owned state atomically after validation. Persistent storage indices are meaningful only when restored alongside matching serialized inventory items.

Built-in layouts throw `InvalidOperationException("Invalid layout data")` for incompatible data. Custom layouts can use different messages, but they should fail clearly rather than silently misplacing items.

```csharp
public sealed class ShelfLayoutPersistentData : ILayoutPersistentData
{
    public int ShelfCount { get; set; }
    public List<int?> ShelfMap { get; set; } = new();

    public object? GetPersistentContext() => ShelfMap;
}
```

```csharp
public ILayoutPersistentData GetPersistentData()
{
    return new ShelfLayoutPersistentData
    {
        ShelfCount = _shelves.Count,
        ShelfMap = new List<int?>(_shelves)
    };
}

public void RestorePersistentData(ILayoutPersistentData? persistentData)
{
    if (persistentData is not ShelfLayoutPersistentData shelfData ||
        shelfData.ShelfCount != _shelves.Count ||
        shelfData.ShelfMap.Count != _shelves.Count)
    {
        throw new InvalidOperationException("Invalid layout data.");
    }

    _shelves.Clear();
    _shelves.AddRange(shelfData.ShelfMap);
}
```

The shape check prevents restoring a five-shelf save into a four-shelf layout. The map list is copied, not reused. A production layout may also validate that stored storage indices are unique and in range when inventory context is available through its restore flow.

### Layout Cloning

`Clone()` is used for transaction simulation and inventory creation. It must return a new layout instance with equivalent state and no shared mutable placement state. Do not share `List<>`, dictionary, set, array, or mutable persistent-data instances with the live layout.

A clone can reuse immutable layout definition data such as slot definitions, section definitions, footprint providers, or read-only configuration. If layout state is implemented with maps or lists, clone by copying those collections. If clone shares mutable placement state, validation simulations can mutate the live inventory layout before commit.

```csharp
public IInventoryLayout<TKey> Clone()
{
    var clone = new ShelfLayout<TKey>(_shelves.Count);
    clone.RestorePersistentData(new ShelfLayoutPersistentData
    {
        ShelfCount = _shelves.Count,
        ShelfMap = new List<int?>(_shelves)
    });
    return clone;
}
```

This pattern mirrors the built-in layouts: capture persistent state, copy mutable collections, and restore into a new instance.

### Layout Sorting

`TrySort(...)` is optional layout behavior. Sorting changes placement only; it does not change `Inventory<TKey>.Items`. Layouts decide which sort contexts they support. Simple position layouts should usually support `ItemSortContext<TKey>`. Complex layouts can define custom `IInventorySortContext<TKey>` implementations.

Unsupported layouts should return `false` and set a clear error, for example `Layout does not support sorting.`. Layouts receiving an unsupported context should return `false` with `Invalid sort context type.`. Sorting should be deterministic, and tie-breakers should preserve previous placement order when item comparison returns zero. Sorting should preserve item/context validity for the layout. Multi-position layouts must return all occupied contexts before and after sort so event payloads are accurate.

A simple fixed-position sort usually follows this algorithm:

1. Collect occupied placements as `(storageIndex, oldPosition)`.
2. Sort by the comparer from `ItemSortContext<TKey>`.
3. Break ties by old position.
4. Rewrite placement state with sorted storage indices.
5. Leave all empty positions where the layout's semantics expect them.
6. Return `true`.

Inventory wraps sorting through `TrySortLayout(...)`. Inventory captures before/after contexts. Sort-generated movement sets `ItemMoved<TKey>.IsSortResult == true`. The layout itself does not create event payloads.

```csharp
public bool TrySort(
    Inventory<TKey> inventory,
    IInventorySortContext<TKey> sortContext,
    out string? error)
{
    if (sortContext is not ItemSortContext<TKey> itemSortContext)
    {
        error = "Invalid sort context type.";
        return false;
    }

    var occupied = new List<(int storageIndex, int shelfIndex)>();
    for (var shelf = 0; shelf < _shelves.Count; shelf++)
    {
        if (_shelves[shelf].HasValue)
            occupied.Add((_shelves[shelf]!.Value, shelf));
    }

    occupied.Sort((a, b) =>
    {
        var result = itemSortContext.Comparer.Compare(
            inventory.Items[a.storageIndex],
            inventory.Items[b.storageIndex]);

        return result != 0
            ? result
            : a.shelfIndex.CompareTo(b.shelfIndex);
    });

    for (var shelf = 0; shelf < _shelves.Count; shelf++)
        _shelves[shelf] = shelf < occupied.Count ? occupied[shelf].storageIndex : null;

    error = null;
    return true;
}
```

This compacting sort is appropriate for a slot or shelf layout. Other layouts may preserve empty positions or use layout-specific strategies.

### Layout Events And Affected Contexts

Layouts do not normally fire `Inventory.Changed`; inventory-level APIs do. Layouts make event payloads correct by returning accurate contexts. `GetContextsForStorageIndex(...)` is used before and after changes, `TryGetContextForStorageIndex(...)` is the single-context convenience path, and `GetAddressableContexts(...)` supports full or broad UI refresh. Multi-cell or multi-position layouts should return every occupied context for a storage index.

Context equality matters for de-duplication in `AffectedLayoutContexts`; contexts should have stable value semantics if the layout creates new context objects often. If the context type does not override equality, consumers may still receive useful contexts, but de-duplication may be less precise. `RequiresFullRefresh` is controlled by inventory operations such as clear, configuration rebuild, or repack, not by layout methods directly.

| Event payload | Layout methods that make it accurate |
|---|---|
| `Added` | `OnItemAdded`, then `GetContextsForStorageIndex`. |
| `Removed` | `GetContextsForStorageIndex` before `OnItemRemoved`. |
| `Modified` | `GetContextsForStorageIndex` before/after amount changes. |
| `Moved` | `TryMove` plus before/after `GetContextsForStorageIndex`. |
| `Swapped` | `TrySwap` plus affected contexts. |
| `MetadataChanged` | `GetContextsForStorageIndex` for the changed storage index. |
| `AffectedLayoutContexts` | All relevant context lists from payloads plus explicit contexts. |

Sort movement appears in `Moved` with `IsSortResult == true`. Direct repack emits moved payloads and full refresh when contexts change. Layout methods should not try to classify events themselves.

## Custom Layout Implementation Checklist

| Step | Check |
|---|---|
| State model | Layout stores placement state using storage indices or an equivalent reversible mapping. |
| Query methods | Addressable contexts, item lookup, and storage-index reverse lookup are deterministic and side-effect free. |
| Context validation | Context type, range, mapped entry indices, and duplicate targets are rejected with clear errors. |
| Mapping | Transaction-level contexts map only `Added` entry indices and preserve deltas/removals. |
| Placement validation | Removals are simulated before additions, and live layout state is not mutated. |
| Auto-placement | Null context has deterministic meaning where supported. |
| Merge candidates | Candidate order is deterministic and respects direct contexts. |
| Notifications | Add/remove/clear callbacks keep stored storage indices aligned with `Inventory.Items`. |
| Move/swap | Movement APIs mutate placement state only. |
| Persistence | Persistent data captures placement state and enough shape data to reject incompatible restores. |
| Cloning | `Clone()` deep-copies mutable placement state and can safely support simulation. |
| Sorting | `TrySort(...)` either implements deterministic placement sorting or rejects unsupported contexts clearly. |
| Events | Context lookup methods return accurate before/after contexts for all storage indices. |
| Parameterization | Runtime parameters, if any, create replacement layouts rather than mutating shared instances. |

## Custom Grid Footprint Providers

Implement `IGridFootprintProvider<TKey>` when `AttributeGridFootprintProvider<TKey>` cannot express footprint rules. Prefer `AttributeGridFootprintProvider<TKey>` when footprint width and height are regular definition attributes.

A custom provider is appropriate when footprint depends on definition id conventions, derived definition classes, multiple attributes, tags/schema families, or application-owned lookup tables. Providers receive item definitions, not item instances and not inventory state. Keep providers deterministic and stable because `MultiCellGridLayout<TKey>` uses footprints during validation, add placement, movement, sorting, repack/rebuild flows, and save/restore-adjacent workflows.

Providers should return positive rectangular footprints through `GridFootprint`. If definition data is invalid, the provider can throw a clear exception or return a safe fallback, depending on project policy. Changing provider behavior for persisted inventories can make existing placement data inconsistent with future placement, sorting, or repack expectations.

The built-in attribute provider is usually enough when definitions already carry dimensions:

```csharp
catalog.Attributes.Define<int>("grid-width");
catalog.Attributes.Define<int>("grid-height");

var provider = new AttributeGridFootprintProvider<string>(
    "grid-width",
    "grid-height",
    defaultFootprint: new GridFootprint(1, 1));

var layout = new MultiCellGridLayout<string>(
    width: 8,
    height: 5,
    footprintProvider: provider);
```

Definitions with both attributes use those dimensions. Definitions missing either attribute use the default footprint. `GridFootprint` enforces positive width and height.

For application-authored footprint tables, use a custom provider:

```csharp
using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;

public sealed class DefinitionFootprintProvider<TKey> : IGridFootprintProvider<TKey>
{
    private readonly IReadOnlyDictionary<TKey, GridFootprint> _footprints;
    private readonly GridFootprint _defaultFootprint;
    private readonly IEqualityComparer<TKey> _comparer;

    public DefinitionFootprintProvider(
        IReadOnlyDictionary<TKey, GridFootprint> footprints,
        GridFootprint? defaultFootprint = null,
        IEqualityComparer<TKey>? comparer = null)
    {
        _footprints = footprints ?? throw new ArgumentNullException(nameof(footprints));
        _defaultFootprint = defaultFootprint ?? new GridFootprint(1, 1);
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
    }

    public GridFootprint GetFootprint(ItemDefinition<TKey> definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        foreach (var pair in _footprints)
        {
            if (_comparer.Equals(pair.Key, definition.Id))
                return pair.Value;
        }

        return _defaultFootprint;
    }
}
```

```csharp
var provider = new DefinitionFootprintProvider<string>(
    new Dictionary<string, GridFootprint>
    {
        ["iron_sword"] = new GridFootprint(1, 3),
        ["tower_shield"] = new GridFootprint(2, 3),
        ["bedroll"] = new GridFootprint(3, 2)
    });

var grid = new MultiCellGridLayout<string>(
    width: 8,
    height: 5,
    footprintProvider: provider,
    placementOrder: GridPlacementOrder.RowMajor,
    defaultAnchor: GridAnchor.TopLeft);
```

This is useful when footprints are application-authored outside definition attributes. The provider returns stable footprints for definition ids. The example intentionally uses `ItemDefinition<TKey>.Id`, not metadata, because footprints are definition-level layout data.

| Concept | Effect |
|---|---|
| `GridFootprint.Width` / `Height` | Number of cells occupied by an item. |
| `GridAnchor.TopLeft` | Context coordinate is the top-left footprint cell. |
| `GridAnchor.TopRight` | Context coordinate is the top-right footprint cell. |
| `GridAnchor.BottomLeft` | Context coordinate is the bottom-left footprint cell. |
| `GridAnchor.BottomRight` | Context coordinate is the bottom-right footprint cell. |
| `GridPlacementOrder` | Scan order for context-less placement and repack. |

Explicit placement resolves the context coordinate into a top-left placement using the selected anchor. Placement fails if the resolved footprint is out of range or overlaps occupied cells. Context-less placement finds the first anchor that can hold the footprint according to placement order. Mapped multi-cell contexts map `InventoryTransaction<TKey>.Added` indices to anchor coordinates.

`MultiCellGridSortContext<TKey>.Compact(...)` uses footprint dimensions to pack larger or more constrained items first using deterministic heuristics. The heuristic is not guaranteed optimal bin packing. Provider results should be stable during a sort operation; if provider output changes between calls for the same definition, placement and sort behavior can become unpredictable. Provider behavior should remain compatible with persisted inventories.

## Extension Example Scope

Extension snippets are intentionally compact. Some examples are full enough to copy into a project with normal using statements:

- `DefinitionMaxStackResolver<TKey>`
- `TunableDefinitionMaxStackResolver<TKey>`
- `BulkCapacityPolicy<TKey>`
- `MaxDefinitionAmountRule<TKey>`
- `DefinitionFootprintProvider<TKey>`

Layout examples are conceptual snippets because a production layout implementation is too large for README:

- `ShelfLayoutContext<TKey>`
- `ShelfLayoutPersistentData`
- shelf persistence, clone, and sort snippets

For real implementations, define constants for parameter ids and attribute ids, keep catalogs explicit, use registered canonical definitions, use class-owned schemas for custom definition classes, keep mutable layout state private, return consumer-facing errors, and prefer built-ins when they fit.

# Reference And Summary

## Feature Map

| Feature | Built-in support | Extension point |
|---|---|---|
| Shared item universe | `ItemCatalog<TKey>` | custom definition subclasses |
| Item schemas | `ItemSchema<TKey>` | custom schema hierarchies |
| Tags | `TagCatalog`, `TagDefinition`, `ResolvedTag` | custom tag taxonomies |
| Attributes | `AttributeCatalog`, `AttributeDefinition`, `AttributeContainer`, `IAttributeView` | string id conventions and custom definition authoring |
| Stacking | built-in resolvers | `IStackResolver<TKey>`, `IParameterizedStackResolver<TKey>` |
| Parameters | inventory-owned runtime mutation | `InventoryParameterDefinition`, parameterized resolver/policy/layout contracts |
| Capacity | built-in policies | `ICapacityPolicy<TKey>`, `IParameterizedCapacityPolicy<TKey>` |
| Rules | built-in rule classes | `IRulePolicy<TKey>` |
| Layout | six default layouts and attribute footprint provider | `IInventoryLayout<TKey>`, `ILayoutContext<TKey>`, `ILayoutPersistentData`, `IGridFootprintProvider<TKey>` |
| Transfers | `InventoryTransfer` | planned transfer contexts |
| Sorting | item and multi-cell sort contexts | `IInventorySortContext<TKey>` |
| Events | `Changed` payloads | UI-specific handlers |
| Persistence | serialized inventory/layout data | custom `ILayoutPersistentData` |

## Public API Quick Reference

| Group | Public APIs |
|---|---|
| Core | `ItemCatalog<TKey>`, `ItemRegistry<TKey>`, `ItemDefinition<TKey>`, `ItemSchema<TKey>`, `ItemSchemaRegistry<TKey>`, `SchemaAttribute`, `DefinitionValidationException`, `ItemInstance<TKey>`, `InstanceMetadata`, `InventoryManager<TKey>`, `Inventory<TKey>`, `InventoryTransaction<TKey>`, `InventoryTransactionBuilder<TKey>`, `NormalizedInventoryTransaction<TKey>`, `InventoryTransfer`, `InventoryTransferBuilder<TKey>`, `InventoryTransferEntry<TKey>`, `SerializedInventory<TKey>`, `SerializedItem<TKey>` |
| Attributes | `AttributeCatalog`, `AttributeDefinition`, `AttributeContainer`, `IAttributeView` |
| Tags | `TagCatalog`, `TagCatalogMode`, `TagDefinition`, `ResolvedTag`, `TagSource` |
| Capacity | `ICapacityPolicy<TKey>`, `IParameterizedCapacityPolicy<TKey>`, `UnlimitedCapacityPolicy<TKey>`, `MaxTotalItemAmountCapacityPolicy<TKey>`, `WeightCapacityPolicy<TKey>` |
| Rules | `IRulePolicy<TKey>`, `IInventoryStructuralRulePolicy<TKey>`, `IInventorySnapshotRulePolicy<TKey>`, `InventorySnapshotRulePolicy<TKey>`, `InventoryRuleSnapshot<TKey>`, `RuleContainer<TKey>`, `IdentifiedRulePolicy<TKey>`, `IdentifiedSnapshotRulePolicy<TKey>`, built-in tag/attribute/metadata/predicate/uniqueness/composition rules |
| Layout | `IInventoryLayout<TKey>`, `IParameterizedInventoryLayout<TKey>`, `ILayoutContext<TKey>`, `ILayoutPersistentData`, all default layouts, contexts, context builders, persistent data classes, `GridPlacementOrder`, `GridAnchor`, `GridFootprint`, `IGridFootprintProvider<TKey>`, `AttributeGridFootprintProvider<TKey>`, `EquipmentSlot<TKey>`, `EquipmentSlotOptions<TKey>`, `SectionDefinition<TKey>`, `SectionDefinitionOptions<TKey>` |
| Sorting | `IInventorySortContext<TKey>`, `ItemSortContext<TKey>`, `MultiCellGridSortContext<TKey>`, `MultiCellGridSortPriority` |
| Stacking | `IStackResolver<TKey>`, `IParameterizedStackResolver<TKey>`, `FixedSizeStackResolver<TKey>`, `ConditionalMaxStackResolver<TKey>`, `AttributeMaxStackResolver<TKey>`, `MultipliedAttributeStackResolver<TKey>` |
| Parameters | `InventoryParameterDefinition`, `InventoryParameterMutationActions` |
| Events | `InventoryChangedEventArgs<TKey>`, `ItemAdded<TKey>`, `ItemRemoved<TKey>`, `ItemModified<TKey>`, `ItemMoved<TKey>`, `ItemSwapped<TKey>`, `ItemMetadataChanged<TKey>`, `InventoryConfigurationChanged<TKey>`, `InventoryConfigurationChangeKind` |
| Persistence/utilities | `SerializedInventory<TKey>`, `SerializedItem<TKey>`, layout persistent data classes, `MetadataUtil.IfPresent<T>` |

## Extension Pitfalls And Caveats

### General Extension Boundaries

- Extension interfaces are public for custom implementations; normal application code should prefer inventory-owned APIs.
- Extension implementations should be deterministic and should not mutate inventory state during validation.
- Error messages should be consumer-facing enough to diagnose rejected operations.
- Preserve the registered-definition invariant: inventories should work with canonical registered definitions from their manager catalog.

### Definition And Resolver Extensions

- Custom definition classes should own schemas through protected constructor chaining.
- Custom stack resolvers should return positive max stack sizes and should not change stack compatibility rules.
- Stack resolver behavior affects add/merge, transactions, transfers, deserialization, repack, and runtime parameter validation.
- Attribute-driven extension code should use public string attribute ids through definition attribute views.

### Capacity And Rule Extensions

- Capacity policies should model inventory-wide capacity resources; item-specific gameplay limits usually belong in rules.
- `CanApply(...)` should use normalized added/removed amounts and should not rely on storage order.
- Custom rules should choose the narrowest validation phase: semantic transaction, structural transaction, or final snapshot.
- Rule ids should be stable when rules are managed at runtime.

### Layout Extensions

- Custom layouts must keep storage-index/context mappings accurate before and after mutations.
- Layout validation must simulate removals before additions and must not mutate live state.
- Mapped layout contexts target `InventoryTransaction<TKey>.Added` indices, not storage indices.
- `Clone()` must deep-copy mutable placement state so simulations cannot mutate live layout state.
- Persistent layout data should include enough shape data to reject incompatible restores.
- Sorting changes placement only and must not reorder `Inventory<TKey>.Items`.
- Context query methods drive event payloads and `AffectedLayoutContexts`; multi-position layouts should return all occupied contexts.
- Runtime layout parameters should create replacement layout instances rather than mutating shared instances in place.

### Multi-Cell Grid Extensions

- Footprint providers should be deterministic and return positive rectangular footprints.
- Provider behavior should remain compatible with persisted inventories.
- Compact multi-cell sorting is deterministic heuristic packing, not guaranteed optimal bin packing.

## Suggested Next Steps

1. Review README wording and examples.
2. Add NuGet metadata to `src/Workes.InventorySystem.csproj` after README wording is stable.
3. Discuss license before adding a `LICENSE` file.
4. Consider PolyForm Noncommercial plus a separate commercial permission path, but do not choose it as final without an explicit licensing decision.
5. Consider package readme wiring and SourceLink after metadata is finalized.
