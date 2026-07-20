# Catalogs And Definitions

An `ItemCatalog<TKey>` is the authoritative item universe shared by a related group of inventories. It defines which item definitions are valid and provides the vocabularies used to describe them.

Read [Core Concepts](CONCEPTS.md) first for the distinction between definition classes, registered definitions, and runtime item instances.

## Catalog Overview

An item catalog exposes four related systems:

| Member | Responsibility |
|---|---|
| `Registry` | Registers canonical item definitions and definition-ID migrations. |
| `Schemas` | Tracks schemas referenced by registered definitions. |
| `Tags` | Declares the tag vocabulary and hierarchy. |
| `Attributes` | Declares typed definition-attribute IDs. |

The recommended setup order is:

```text
Create catalog
  Select tag mode
  Declare tags
  Declare definition attributes
  Create item definition objects
  Register definitions
  Register obsolete-ID migrations
  Freeze catalog
```

After freezing, managers can create inventories that use the catalog.

```csharp
var catalog = new ItemCatalog<string>();

var apple = new ItemDefinition<string>("apple");
var coin = new ItemDefinition<string>("coin");

catalog.Registry.Register(apple);
catalog.Registry.Register(coin);
catalog.Freeze();
```

Plain definitions use `ItemSchema<TKey>.Default` and do not require tags or attributes.

## Definition IDs

`TKey` is the definition-ID type used by the registry and all inventories connected to the catalog.

Choose IDs that remain stable across:

- registry lookup.
- saved data.
- definition migrations.
- transactions and transfers.
- content updates.

Strings are a practical default, but `Guid`, integer, enum, and custom key types are supported when they provide stable equality and hash behavior.

```csharp
var catalog = new ItemCatalog<int>();

var wood = new ItemDefinition<int>(1001);
var stone = new ItemDefinition<int>(1002);

catalog.Registry.Register(wood);
catalog.Registry.Register(stone);
catalog.Freeze();
```

Numeric IDs are still explicit identities. They are not storage indexes, UI positions, or implicit registration order.

## Registration And Canonical Definitions

Register each definition object once through `ItemCatalog<TKey>.Registry`.

```csharp
var potion = new ItemDefinition<string>("health_potion");

catalog.Registry.Register(potion);
```

Registration establishes `potion` as the canonical object for `"health_potion"` in this catalog.

The registry rejects duplicate IDs. Inventories also reject:

- definitions that are not registered in their catalog.
- detached definition objects whose IDs match registered definitions.

```csharp
catalog.Registry.Register(potion);
catalog.Freeze();

var canonical = catalog.Registry.Resolve("health_potion");
var detached = new ItemDefinition<string>("health_potion");

ReferenceEquals(canonical, potion); // true
ReferenceEquals(detached, potion);  // false
```

Always retain or resolve the registered object instead of reconstructing definitions by ID.

### Registry lookup APIs

| API | Behavior |
|---|---|
| `Definitions` | Enumerates directly registered definitions. |
| `Contains(id)` | Checks whether an ID is directly registered. |
| `TryGet(id, out definition)` | Attempts direct registered-definition lookup. |
| `Resolve(id)` | Resolves a directly registered ID or an obsolete ID migration. |
| `Register(definition)` | Registers a canonical definition before freeze. |
| `RegisterMigration(oldId, replacement)` | Maps an obsolete ID to a canonical registered replacement. |

`Contains` and `TryGet` concern current registered IDs. Use `Resolve` when persisted data may contain migrated IDs.

Inventory APIs that accept a definition ID, such as `Add(id)`, `TryAdd(id, ...)`, `Count(id)`, `Find(id)`, and
`TryRemoveByDefinition(id, ...)`, use the same migration-aware `Resolve` path before operating on the canonical
registered definition. Definition-object overloads remain valid when application code already holds the canonical
registered object.

## Tags

Tags classify definitions for rules, layouts, queries, and application behavior.

`TagCatalog` is the authority for valid tag IDs. Definitions and schemas store direct tag IDs, but catalog resolution determines full membership, including schema tags and generated parent tags.

## Tag Modes

Every tag catalog uses exactly one mode.

| Mode | Example | Selection |
|---|---|---|
| Namespaced | `core:equipment.tools.knife` | `new ItemCatalog<TKey>()` or `new ItemCatalog<TKey>(true)` |
| Non-namespaced | `equipment.tools.knife` | `new ItemCatalog<TKey>(false)` |

Namespaced tags separate a namespace from a dot-separated path:

```text
core:equipment.tools.knife
^^^^ ^^^^^^^^^^^^^^^^^^^^^
namespace      path
```

Non-namespaced tags retain the dot hierarchy without a namespace:

```text
equipment.tools.knife
```

Select the mode before declaring any tags.

```csharp
var namespacedCatalog = new ItemCatalog<string>();
var explicitNamespacedCatalog =
    new ItemCatalog<string>(areTagsNamespaced: true);
var simpleCatalog =
    new ItemCatalog<string>(areTagsNamespaced: false);
```

`TagCatalog` provides the same parameterless and `areTagsNamespaced` constructors when it is used directly.

The existing `UseNamespacedTagsOnly()` and `UseNonNamespacedTagsOnly()` methods remain compatibility shims for catalogs
created with the parameterless constructor. Call them before defining tags. Constructor-selected mode is explicit and
cannot be switched later.

## Declaring Tags

Declare tags with `TagCatalog.Define(...)`.

```csharp
catalog.Tags.Define("core:equipment.tools.knife");
```

Declaring a tag automatically declares its generated parents:

```text
core:equipment.tools.knife
core:equipment.tools
core:equipment
```

Useful lookup APIs include:

| API | Behavior |
|---|---|
| `Define(id)` | Declares a canonical tag and its parent hierarchy. |
| `Get(id)` | Returns a declared tag or throws. |
| `TryGet(id, out tag)` | Attempts to get a declared tag. |
| `Contains(id)` | Checks whether a valid tag ID is declared. |
| `GetHierarchy(id)` | Returns generated parent tags from most to least specific. |
| `All` | Enumerates direct and generated catalog tags. |

## Definition Tags And Schema Tags

Tags may come from two places:

- a schema applies tags to every definition using that schema.
- a registered definition supplies direct tags for that particular definition.

```csharp
var knife = new ItemDefinition<string>(
    "iron_knife",
    "core:equipment.tools.knife");

catalog.Tags.Define("core:equipment.tools.knife");
catalog.Registry.Register(knife);
catalog.Freeze();
```

`ItemDefinition<TKey>.Tags` exposes only tags declared directly on that definition. `ItemSchema<TKey>.DirectTags` exposes only tags declared directly on that schema.

Use catalog resolution when hierarchy and schema membership matter:

```csharp
catalog.Satisfies(knife, "core:equipment.tools.knife"); // true
catalog.Satisfies(knife, "core:equipment.tools");       // true
catalog.Satisfies(knife, "core:equipment");             // true

var resolvedTags = catalog.ResolveTags(knife);
```

`ResolveTags(...)` reports resolved tags with source information, distinguishing schema tags, direct definition tags, and generated parents.

## Definition Attributes

Definition attributes are typed values shared by every runtime instance of a registered definition.

Typical examples include:

- weight.
- damage.
- stackability.
- base stack size.
- grid footprint dimensions.

Declare each attribute ID and value type in the catalog:

```csharp
catalog.Attributes.Define<int>("weight");
catalog.Attributes.Define<int>("damage");
catalog.Attributes.Define<bool>("stackable");
```

Attribute IDs are strings in the public authoring API. The pair of ID and .NET value type forms the declaration.

Every attribute ID is declared exactly once. Declaring an existing ID again is rejected, whether the requested value
type is the same or different.

```csharp
catalog.Attributes.Define<int>("weight");
catalog.Attributes.Define<int>("weight");   // Rejected.
catalog.Attributes.Define<float>("weight"); // Rejected.
```

Useful APIs include:

| API | Behavior |
|---|---|
| `Define<T>(id)` | Declares an attribute ID with value type `T`. |
| `Get<T>(id)` | Returns the matching declaration or throws. |
| `TryGet<T>(id, out definition)` | Attempts typed lookup. |
| `Contains<T>(id)` | Checks the ID and expected value type. |
| `All` | Enumerates declared attributes. |

String constants can prevent spelling drift while keeping catalog declarations authoritative:

```csharp
private const string Weight = "weight";

catalog.Attributes.Define<int>(Weight);
```

Definition attributes are different from instance metadata:

| Definition attribute | Instance metadata |
|---|---|
| Describes an item type. | Describes one runtime stack. |
| Authored when constructing a definition. | Mutated during runtime. |
| Constrained by the definition schema. | Stored in `ItemInstance<TKey>.Metadata`. |
| Shared through the canonical definition. | Owned by an individual item instance. |

## Item Schemas

An `ItemSchema<TKey>` describes the tags and typed attributes required by an item-definition family.

Schemas have:

- a stable string `Id`.
- an optional parent schema.
- direct required attributes.
- direct tags.

Plain `ItemDefinition<TKey>` objects use `ItemSchema<TKey>.Default`.

## Class-Owned Schemas

Custom definition classes should normally own their schemas:

```csharp
public class EquipmentDefinition : ItemDefinition<string>
{
    public static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<EquipmentDefinition>("equipment")
            .RequireAttribute<int>("weight")
            .AddTag("core:equipment");

    public EquipmentDefinition(string id, int weight, params string[] tags)
        : this(id, Schema, weight, tags)
    {
    }

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
```

The public and protected constructors serve different callers:

- application code uses the public constructor and supplies ordinary definition data.
- derived definition classes use the protected constructor so they can pass their own class-owned child schema through
  the shared equipment authoring logic.

The `schema` parameter is therefore an inheritance mechanism, not application-level schema selection.

The final `tags` argument selects between two `ItemDefinition<TKey>` constructor overloads:

```csharp
base(id, schema)
base(id, schema, tags)
```

Both overloads assign the same schema. The second additionally declares `tags` directly on that individual definition.
It does not add them to the schema. Schema tags such as `core:equipment` apply to every definition using the schema,
whereas a direct tag such as `core:equipment.armor` can describe one particular definition.

Both forms are generally valid. A definition class should expose and forward a `tags` argument when its use case benefits
from caller-authored per-definition tags. It can use the tagless overload when its schema completely describes tag
membership or when the class provides more specific tag-authoring methods. This choice is independent of schema
ownership and schema inheritance.

`ItemSchema<TKey>.CreateFor<TDefinition>(id)` records the owning definition type. Catalog validation permits that schema on the owner type and derived definition types, but rejects its use by unrelated classes.

Advanced shared-schema workflows can use `ItemSchema<TKey>.Create(id)`, which creates an unowned schema. Prefer class ownership when a schema belongs to a specific definition family.

## Definition Authoring

Derived definition classes use protected authoring helpers:

- `DefineAttribute<T>(id, value)`.
- `DefineTag(id)`.
- `DefineTags(tags)`.

Callers then construct definitions using domain data:

```csharp
var helmet = new EquipmentDefinition(
    "iron_helmet",
    weight: 3,
    "core:equipment.armor");
```

Definition attributes and tags are externally read-only after construction.

Public constructors on registered concrete definition classes should not accept `ItemSchema<TKey>` parameters. Catalog freeze rejects that shape because callers should not select arbitrary schemas for otherwise identical definition classes.

## Schema Inheritance

Schema inheritance should mirror the C# definition-class hierarchy.

```csharp
public sealed class WeaponDefinition : EquipmentDefinition
{
    public new static readonly ItemSchema<string> Schema =
        ItemSchema<string>.CreateFor<WeaponDefinition>("weapon")
            .WithParent(EquipmentDefinition.Schema)
            .RequireAttribute<int>("damage")
            .AddTag("core:equipment.weapons");

    public WeaponDefinition(
        string id,
        int weight,
        int damage,
        params string[] tags)
        : base(id, Schema, weight, tags)
    {
        DefineAttribute("damage", damage);
    }
}
```

`WeaponDefinition` derives from `EquipmentDefinition`, so its schema may inherit from `EquipmentDefinition.Schema`.

The resulting definition must provide:

- `weight`, inherited from the equipment schema.
- `damage`, required directly by the weapon schema.

It also satisfies tags contributed by both schemas.

```csharp
catalog.Attributes.Define<int>("weight");
catalog.Attributes.Define<int>("damage");
catalog.Tags.Define("core:equipment.weapons.blades");

var sword = new WeaponDefinition(
    "iron_sword",
    weight: 5,
    damage: 12,
    "core:equipment.weapons.blades");

catalog.Registry.Register(sword);
catalog.Freeze();
```

Declaring the leaf tag also declares `core:equipment.weapons` and `core:equipment`, satisfying the schema tag declarations.

### Attribute inheritance

`RequireAttribute<T>(id, inherited: true)` controls whether child schemas inherit a requirement. The default is `true`.

```csharp
var parent = ItemSchema<string>.Create("parent")
    .RequireAttribute<int>("quality", inherited: false);

var child = ItemSchema<string>.Create("child")
    .WithParent(parent)
    .RequireAttribute<int>("quality");
```

The child may redeclare `quality` because the parent requirement is not inherited. Redefining an inherited parent attribute is rejected.

Schema tags always flow through the parent chain.

## Schema Discovery

Applications do not normally register schemas separately.

When a definition is registered, the catalog discovers:

- the definition’s schema.
- every parent schema in its chain.

The discovered schemas appear in `ItemCatalog<TKey>.Schemas`. A schema ID must refer to one schema object within the catalog; using separate schema objects with the same ID is rejected.

## Freezing And Validation

Call `ItemCatalog<TKey>.Freeze()` after setup.

Freeze:

- validates schema parent chains and detects cycles.
- rejects redefinition of inherited attributes.
- validates owned-schema use against definition-class relationships.
- rejects public schema-taking constructors on registered concrete definition classes.
- confirms that schema attributes are declared in `AttributeCatalog`.
- confirms that schema and direct definition tags are declared in `TagCatalog`.
- validates each definition against its resolved schema requirements.
- freezes registered schemas.
- freezes the definition registry.

After freeze:

- definitions and migrations cannot be added to the registry.
- referenced schemas cannot be modified.
- managers can create inventories using the catalog.

The current public `TagCatalog` and `AttributeCatalog` APIs do not expose a frozen state. Complete their declarations before catalog freeze so the validated item universe is coherent.

### Common freeze failures

Freeze rejects catalogs when:

- a required definition attribute is missing.
- a definition supplies an attribute its schema does not allow.
- a schema references an undeclared attribute ID or mismatched value type.
- a schema or definition references an undeclared tag.
- a tag ID does not match the selected tag mode.
- a schema inheritance chain contains a cycle.
- a child schema redefines an inherited attribute.
- an owned schema is used by an unrelated definition class.
- a registered definition class exposes schema selection as a public constructor parameter.

Treat freeze failures as setup errors. Resolve them before creating runtime inventories.

## Definition ID Migrations

Definition migrations preserve compatibility when saved data contains obsolete IDs.

Register the replacement definition first, then map old IDs to that canonical object:

```csharp
var catalog = new ItemCatalog<string>();

var healthPotion = new ItemDefinition<string>("health_potion");
catalog.Registry.Register(healthPotion);

catalog.Registry.RegisterMigration(
    "minor_healing_potion",
    healthPotion);

catalog.Registry.RegisterMigration(
    "major_health_potion",
    healthPotion);

catalog.Freeze();

var current = catalog.Registry.Resolve("health_potion");
var migrated = catalog.Registry.Resolve("minor_healing_potion");

ReferenceEquals(current, healthPotion);  // true
ReferenceEquals(migrated, healthPotion); // true
```

Multiple obsolete IDs may point directly to the same current definition.

A migration is rejected when:

- the obsolete ID is already a registered definition.
- the obsolete ID already has a migration.
- the replacement definition is not registered in the same registry.
- the replacement is a detached same-ID object rather than the canonical registered object.
- the registry is already frozen.

Migrations map IDs; they do not copy definitions, create aliases in `Definitions`, or bypass canonical-object validation.

Portable snapshots encode built-in key types directly. A custom `TKey` declares its one separate, stateless
`IInventorySnapshotKeyCodec<TKey>` through `InventorySnapshotKeyCodecAttribute`; there is no public registration or
per-inventory codec option.
Snapshot restoration first decodes that value and then resolves it through `ItemRegistry<TKey>.Resolve(...)`, so
restored instances use current canonical definitions and existing migration mappings.

## Complete Setup Example

```csharp
var catalog = new ItemCatalog<string>();

catalog.Attributes.Define<int>("weight");
catalog.Attributes.Define<int>("damage");
catalog.Tags.Define("core:equipment.weapons.blades");

var sword = new WeaponDefinition(
    "iron_sword",
    weight: 5,
    damage: 12,
    "core:equipment.weapons.blades");

var legacySword = "old_iron_sword";

catalog.Registry.Register(sword);
catalog.Registry.RegisterMigration(legacySword, sword);
catalog.Freeze();

var resolvedSword = catalog.Registry.Resolve("iron_sword");
var migratedSword = catalog.Registry.Resolve(legacySword);

ReferenceEquals(resolvedSword, sword); // true
ReferenceEquals(migratedSword, sword); // true
catalog.Satisfies(sword, "core:equipment"); // true
```

The frozen catalog can now be passed to one or more `InventoryManager<TKey>` instances.

## Practical Checklist

Before freezing:

- choose a stable `TKey`.
- select the tag mode.
- declare every referenced tag.
- declare every referenced definition attribute with the correct value type.
- construct definitions through their intended definition classes.
- register each canonical definition object once.
- register obsolete-ID migrations against already registered replacements.
- confirm schema inheritance mirrors definition-class inheritance.

At runtime:

- reuse retained canonical definitions or resolve them through the registry.
- never reconstruct definitions merely from their IDs.
- use `catalog.Satisfies(...)` for resolved tag membership.
- read definition attributes through `IAttributeView`.
- keep per-stack variation in item-instance metadata.

## Continue Reading

- [Core Concepts](CONCEPTS.md)
- [Inventory operations](INVENTORY_OPERATIONS.md)
- [Layouts](LAYOUTS.md)
- [Policies and rules](POLICIES_AND_RULES.md)
- [Persistence](PERSISTENCE.md)
- [Extending the system](EXTENDING.md)
