using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class ItemUniverseFoundationTests
{
    private const string Weight = "weight";
    private const string Durability = "durability";
    private const string ChopPower = "chopPower";
    private const string Quality = "quality";
    private const string CutPower = "cutPower";
    private const string Damage = "damage";
    private static readonly string KnifeTag = "core:equipment.tools.knife";
    private static readonly string ObsidianTag = "c:materials.obsidian";
    private static readonly string SteelTag = "c:materials.steel";

    private enum TestItemKind
    {
        Coin = 1,
        Potion = 2
    }

    private sealed class TestItemId
    {
        public string Value { get; }

        public TestItemId(string value)
        {
            Value = value;
        }

        public override bool Equals(object? obj)
        {
            return obj is TestItemId other && Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string>? catalog = null, RuleContainer<string>? rules = null)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            rules,
            catalog);
    }

    private static void DefineTags(ItemCatalog<string> catalog, params string[] tags)
    {
        foreach (var tag in tags)
            catalog.Tags.Define(tag);
    }

    private static void DefineAttributes(ItemCatalog<string> catalog)
    {
        catalog.Attributes.Define<int>(Weight);
        catalog.Attributes.Define<int>(Durability);
        catalog.Attributes.Define<int>(ChopPower);
        catalog.Attributes.Define<int>(Quality);
        catalog.Attributes.Define<int>(CutPower);
        catalog.Attributes.Define<int>(Damage);
    }

    // Test-only helper for invalid schema edge cases. Normal usage should colocate
    // schemas on concrete definition classes, as Equipment/Tool/Axe do below.
    private sealed class SchemaValidationDefinition : ItemDefinition<string>
    {
        private SchemaValidationDefinition(
            string id,
            ItemSchema<string> schema,
            bool defineWeight = false,
            bool defineDurability = false,
            bool defineChopPower = false,
            bool defineQuality = false)
            : base(id, schema)
        {
            if (defineWeight)
                DefineAttribute(Weight, 1);
            if (defineDurability)
                DefineAttribute(Durability, 2);
            if (defineChopPower)
                DefineAttribute(ChopPower, 3);
            if (defineQuality)
                DefineAttribute(Quality, 4);
        }

        public static SchemaValidationDefinition Create(
            string id,
            ItemSchema<string> schema,
            bool defineWeight = false,
            bool defineDurability = false,
            bool defineChopPower = false,
            bool defineQuality = false)
        {
            return new SchemaValidationDefinition(id, schema, defineWeight, defineDurability, defineChopPower, defineQuality);
        }
    }

    private class EquipmentDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> EquipmentSchema =
            ItemSchema<string>.CreateFor<EquipmentDefinition>("test-equipment")
                .RequireAttribute<int>(Weight, inherited: true);

        protected EquipmentDefinition(string id, ItemSchema<string> schema, int weight)
            : base(id, schema)
        {
            DefineAttribute(Weight, weight);
        }

        public EquipmentDefinition(string id, int weight)
            : this(id, EquipmentSchema, weight)
        {
        }
    }

    private class ToolDefinition : EquipmentDefinition
    {
        public static readonly ItemSchema<string> ToolSchema =
            ItemSchema<string>.CreateFor<ToolDefinition>("test-tool")
                .WithParent(EquipmentSchema)
                .RequireAttribute<int>(Durability, inherited: true);

        protected ToolDefinition(string id, ItemSchema<string> schema, int weight, int durability)
            : base(id, schema, weight)
        {
            DefineAttribute(Durability, durability);
        }

        public ToolDefinition(string id, int weight, int durability)
            : this(id, ToolSchema, weight, durability)
        {
        }
    }

    private sealed class AxeDefinition : ToolDefinition
    {
        public static readonly string AxeTag = "core:equipment.tools.axe";

        public static readonly ItemSchema<string> AxeSchema =
            ItemSchema<string>.CreateFor<AxeDefinition>("test-axe")
                .WithParent(ToolSchema)
                .RequireAttribute<int>(ChopPower, inherited: true)
                .AddTag(AxeTag);

        public AxeDefinition(string id, int weight, int durability, int chopPower, params string[] tags)
            : base(id, AxeSchema, weight, durability)
        {
            DefineTags(tags);
            DefineAttribute(ChopPower, chopPower);
        }
    }

    private sealed class HammerDefinition : ToolDefinition
    {
        public HammerDefinition(string id, int weight, int durability)
            : base(id, weight, durability)
        {
        }
    }

    private sealed class PolishedEquipmentDefinition : EquipmentDefinition
    {
        public PolishedEquipmentDefinition(string id, int weight, int quality)
            : base(id, weight)
        {
            DefineAttribute(Quality, quality);
        }
    }

    private sealed class RepositoryKnifeDefinition : ToolDefinition
    {
        public static readonly ItemSchema<string> KnifeSchema =
            ItemSchema<string>.CreateFor<RepositoryKnifeDefinition>("repository-knife")
                .WithParent(ToolSchema)
                .RequireAttribute<int>(CutPower, inherited: true)
                .RequireAttribute<int>(Damage, inherited: true)
                .AddTag(KnifeTag);

        public RepositoryKnifeDefinition(string id, int weight, int durability, int cutPower, int damage, params string[] tags)
            : base(id, KnifeSchema, weight, durability)
        {
            DefineTags(tags);
            DefineAttribute(CutPower, cutPower);
            DefineAttribute(Damage, damage);
        }
    }

    private sealed class SchemaTagDefinition : ItemDefinition<string>
    {
        private SchemaTagDefinition(string id, ItemSchema<string> schema)
            : base(id, schema)
        {
        }

        public static SchemaTagDefinition Create(string id, ItemSchema<string> schema)
        {
            return new SchemaTagDefinition(id, schema);
        }
    }

    private sealed class StringAttributeDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StringAttributeSchema =
            ItemSchema<string>.CreateFor<StringAttributeDefinition>("string-attribute-equipment")
                .RequireAttribute<int>("stringWeight", inherited: true);

        public StringAttributeDefinition(string id, int weight)
            : base(id, StringAttributeSchema)
        {
            DefineAttribute("stringWeight", weight);
        }
    }

    private sealed class PotionDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> PotionSchema =
            ItemSchema<string>.CreateFor<PotionDefinition>("test-potion");

        public PotionDefinition(string id)
            : base(id, PotionSchema)
        {
        }
    }

    private sealed class UnrelatedOwnedSchemaDefinition : ItemDefinition<string>
    {
        public UnrelatedOwnedSchemaDefinition(string id)
            : base(id, RepositoryKnifeDefinition.KnifeSchema)
        {
            DefineAttribute(Weight, 1);
            DefineAttribute(Durability, 1);
            DefineAttribute(CutPower, 1);
            DefineAttribute(Damage, 1);
        }
    }

    private sealed class PublicSchemaForwardingDefinition : ItemDefinition<string>
    {
        public PublicSchemaForwardingDefinition(string id, ItemSchema<string> schema)
            : base(id, schema)
        {
        }
    }

    [Test]
    public void InventoryManager_CreatesDefaultCatalog_AndRegistryForwardsToCatalog()
    {
        var manager = CreateManager();

        Assert.That(manager.Catalog, Is.Not.Null);
        Assert.That(manager.Registry, Is.SameAs(manager.Catalog.Registry));
    }

    [Test]
    public void Managers_CanShareExternalCatalog()
    {
        var catalog = new ItemCatalog<string>();
        var managerA = CreateManager(catalog);
        var managerB = CreateManager(catalog);
        var apple = new ItemDefinition<string>("apple");

        catalog.Registry.Register(apple);

        Assert.Throws<InvalidOperationException>(() => managerA.CreateInventory());

        catalog.Freeze();

        Assert.That(managerA.Catalog, Is.SameAs(catalog));
        Assert.That(managerB.Catalog, Is.SameAs(catalog));
        Assert.DoesNotThrow(() => managerA.CreateInventory());
        Assert.DoesNotThrow(() => managerB.CreateInventory());
    }

    [Test]
    public void CatalogFreeze_FreezesRegistry()
    {
        var manager = CreateManager();
        manager.Registry.Register(new ItemDefinition<string>("apple"));

        manager.Catalog.Freeze();

        Assert.That(manager.Catalog.Frozen, Is.True);
        Assert.DoesNotThrow(() => manager.CreateInventory());
    }

    [Test]
    public void ManagerWithoutCatalog_StillRequiresFreezeBeforeInventoryCreation()
    {
        var manager = CreateManager();

        manager.Registry.Register(new ItemDefinition<string>("apple"));

        Assert.That(manager.Catalog, Is.Not.Null);
        Assert.Throws<InvalidOperationException>(() => manager.CreateInventory());

        manager.Catalog.Freeze();

        Assert.DoesNotThrow(() => manager.CreateInventory());
    }

    [Test]
    public void ItemDefinition_PublicConstructors_DoNotExposeSchemaParameters()
    {
        var publicConstructors = typeof(ItemDefinition<string>).GetConstructors();
        var protectedConstructors = typeof(ItemDefinition<string>).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(publicConstructors.SelectMany(c => c.GetParameters()).Any(p => p.ParameterType == typeof(ItemSchema<string>)), Is.False);
        Assert.That(protectedConstructors.SelectMany(c => c.GetParameters()).Any(p => p.ParameterType == typeof(ItemSchema<string>)), Is.True);
    }

    [Test]
    public void SchemaConstructorPattern_RegistersParentChain_AndValidatesAttributes()
    {
        var catalog = new ItemCatalog<string>();
        var axe = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);

        DefineAttributes(catalog);
        DefineTags(catalog, AxeDefinition.AxeTag);
        catalog.Registry.Register(axe);
        catalog.Freeze();

        Assert.That(axe.Schema, Is.SameAs(AxeDefinition.AxeSchema));
        Assert.That(catalog.Schemas.Contains(AxeDefinition.AxeSchema.Id), Is.True);
        Assert.That(catalog.Schemas.Contains(ToolDefinition.ToolSchema.Id), Is.True);
        Assert.That(catalog.Schemas.Contains(EquipmentDefinition.EquipmentSchema.Id), Is.True);
    }

    [Test]
    public void ItemSchema_CreateFor_RecordsOwnerDefinitionType()
    {
        var schema = ItemSchema<string>.CreateFor<PotionDefinition>("owned-potion");
        var shared = ItemSchema<string>.Create("shared");

        Assert.That(schema.OwnerDefinitionType, Is.EqualTo(typeof(PotionDefinition)));
        Assert.That(shared.OwnerDefinitionType, Is.Null);
        Assert.That(ItemSchema<string>.Default.OwnerDefinitionType, Is.Null);
    }

    [Test]
    public void CatalogFreeze_AllowsDefinitionUsingItsOwnedSchema()
    {
        var catalog = new ItemCatalog<string>();
        var potion = new PotionDefinition("potion");

        catalog.Registry.Register(potion);

        Assert.DoesNotThrow(() => catalog.Freeze());
        Assert.That(potion.Schema.OwnerDefinitionType, Is.EqualTo(typeof(PotionDefinition)));
    }

    [Test]
    public void CatalogFreeze_AllowsDefinitionUsingSchemaOwnedByBaseDefinitionType()
    {
        var catalog = new ItemCatalog<string>();
        var hammer = new HammerDefinition("hammer", weight: 5, durability: 10);

        DefineAttributes(catalog);
        catalog.Registry.Register(hammer);

        Assert.DoesNotThrow(() => catalog.Freeze());
        Assert.That(hammer.Schema.OwnerDefinitionType, Is.EqualTo(typeof(ToolDefinition)));
    }

    [Test]
    public void CatalogFreeze_RejectsDefinitionUsingSchemaOwnedByUnrelatedDefinitionType()
    {
        var catalog = new ItemCatalog<string>();
        var definition = new UnrelatedOwnedSchemaDefinition("fake-knife");

        DefineAttributes(catalog);
        DefineTags(catalog, KnifeTag);
        catalog.Registry.Register(definition);

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
        Assert.That(ex!.Message, Does.Contain("owned by definition type"));
        Assert.That(ex.Message, Does.Contain(nameof(RepositoryKnifeDefinition)));
        Assert.That(ex.Message, Does.Contain(nameof(UnrelatedOwnedSchemaDefinition)));
    }

    [Test]
    public void ItemSchema_WithParent_AllowsParentOwnedByBaseDefinitionType()
    {
        var schema = ItemSchema<string>.CreateFor<AxeDefinition>("owned-axe-parent-check")
            .WithParent(ToolDefinition.ToolSchema);

        Assert.That(schema.Parent, Is.SameAs(ToolDefinition.ToolSchema));
    }

    [Test]
    public void ItemSchema_WithParent_RejectsParentOwnedByUnrelatedDefinitionType()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ItemSchema<string>.CreateFor<PotionDefinition>("invalid-owned-parent")
                .WithParent(ToolDefinition.ToolSchema));
    }

    [Test]
    public void CatalogFreeze_RejectsPublicSchemaTakingDefinitionConstructor()
    {
        var catalog = new ItemCatalog<string>();
        var schema = ItemSchema<string>.Create("public-forwarded");
        var definition = new PublicSchemaForwardingDefinition("forwarded", schema);

        catalog.Registry.Register(definition);

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
        Assert.That(ex!.Message, Does.Contain(nameof(PublicSchemaForwardingDefinition)));
        Assert.That(ex.Message, Does.Contain("public ItemSchema constructor parameter"));
    }

    [Test]
    public void CatalogFreeze_AllowsProtectedSchemaTakingDefinitionConstructor()
    {
        var catalog = new ItemCatalog<string>();
        var axe = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);

        DefineAttributes(catalog);
        DefineTags(catalog, AxeDefinition.AxeTag);
        catalog.Registry.Register(axe);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void ChildDefinition_CanIntentionallyReuseParentSchema()
    {
        var catalog = new ItemCatalog<string>();
        var hammer = new HammerDefinition("hammer", weight: 6, durability: 12);

        DefineAttributes(catalog);
        catalog.Registry.Register(hammer);
        catalog.Freeze();

        Assert.That(hammer.Schema, Is.SameAs(ToolDefinition.ToolSchema));
        Assert.That(catalog.Schemas.Contains(ToolDefinition.ToolSchema.Id), Is.True);
        Assert.That(catalog.Schemas.Contains(EquipmentDefinition.EquipmentSchema.Id), Is.True);
    }

    [Test]
    public void CatalogFreeze_Fails_WhenInheritedRequiredAttributeIsMissing()
    {
        var parent = ItemSchema<string>.Create("missing-parent")
            .RequireAttribute<int>(Weight, inherited: true);
        var child = ItemSchema<string>.Create("missing-child")
            .WithParent(parent)
            .RequireAttribute<int>(Durability, inherited: true);
        var definition = SchemaValidationDefinition.Create("broken-tool", child, defineDurability: true);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDirectRequiredAttributeIsMissing()
    {
        var schema = ItemSchema<string>.Create("missing-direct")
            .RequireAttribute<int>(Weight, inherited: true);
        var definition = SchemaValidationDefinition.Create("broken-equipment", schema);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDefinitionProvidesUndeclaredAttribute()
    {
        var definition = new PolishedEquipmentDefinition("equipment", weight: 5, quality: 4);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void ItemDefinition_ExposesReadOnlyAttributeView()
    {
        var property = typeof(ItemDefinition<string>).GetProperty(nameof(ItemDefinition<string>.Attributes));

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.PropertyType, Is.EqualTo(typeof(IAttributeView)));
        Assert.That(typeof(IAttributeView).GetMethod("Set"), Is.Null);
    }

    [Test]
    public void DefinitionClass_CanDefineSchemaAttributesThroughProtectedHelper()
    {
        var definition = new EquipmentDefinition("equipment", weight: 5);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
        Assert.That(definition.Attributes.TryGet<int>(Weight, out var weight), Is.True);
        Assert.That(weight, Is.EqualTo(5));
    }

    [Test]
    public void DefinitionClass_CanDefineSchemaAttributesByStringId()
    {
        var definition = new StringAttributeDefinition("equipment", weight: 5);
        var catalog = new ItemCatalog<string>();
        catalog.Attributes.Define<int>("stringWeight");

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(definition.Attributes.TryGet<int>("stringWeight", out var resolvedWeight), Is.True);
        Assert.That(resolvedWeight, Is.EqualTo(5));
    }

    [Test]
    public void AttributeCatalog_DefineString_ReturnsCanonicalDefinition()
    {
        var catalog = new ItemCatalog<string>();

        var defined = catalog.Attributes.Define<int>("weight");
        var definedAgain = catalog.Attributes.Define<int>("weight");

        Assert.That(defined.Id, Is.EqualTo("weight"));
        Assert.That(defined.ValueType, Is.EqualTo(typeof(int)));
        Assert.That(definedAgain, Is.SameAs(defined));
        Assert.That(catalog.Attributes.Contains<int>("weight"), Is.True);
    }

    [Test]
    public void AttributeCatalog_Get_ReturnsDeclaredDefinition()
    {
        var catalog = new ItemCatalog<string>();
        var defined = catalog.Attributes.Define<int>(Weight);

        var resolved = catalog.Attributes.Get<int>("weight");

        Assert.That(resolved, Is.SameAs(defined));
    }

    [Test]
    public void AttributeCatalog_Get_ThrowsForUnknownAttribute()
    {
        var catalog = new ItemCatalog<string>();

        Assert.Throws<InvalidOperationException>(() => catalog.Attributes.Get<int>("missing"));
    }

    [Test]
    public void AttributeCatalog_DefineRejectsSameIdWithDifferentType()
    {
        var catalog = new ItemCatalog<string>();

        catalog.Attributes.Define<int>("weight");

        Assert.Throws<InvalidOperationException>(() => catalog.Attributes.Define<float>("weight"));
        Assert.That(catalog.Attributes.TryGet<float>("weight", out _), Is.False);
    }

    [Test]
    public void CatalogFreeze_FailsWhenSchemaAttributeIsNotDeclared()
    {
        var definition = new EquipmentDefinition("equipment", weight: 5);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_SucceedsWhenSchemaAttributesAreDeclared()
    {
        var definition = new EquipmentDefinition("equipment", weight: 5);
        var catalog = new ItemCatalog<string>();

        catalog.Attributes.Define<int>(Weight);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_RegistersAndValidatesInheritedSchemaAttributes()
    {
        var definition = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        DefineTags(catalog, AxeDefinition.AxeTag);
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Schemas.Contains(EquipmentDefinition.EquipmentSchema.Id), Is.True);
        Assert.That(catalog.Schemas.Contains(ToolDefinition.ToolSchema.Id), Is.True);
        Assert.That(catalog.Schemas.Contains(AxeDefinition.AxeSchema.Id), Is.True);
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDefinitionContainsAttributeNotDeclaredBySchema()
    {
        var schema = ItemSchema<string>.Create("undeclared-quality")
            .RequireAttribute<int>(Weight, inherited: true);
        var definition = SchemaValidationDefinition.Create("equipment", schema, defineWeight: true, defineQuality: true);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenChildRedefinesInheritedAttribute()
    {
        var parent = ItemSchema<string>.Create("redefine-parent")
            .RequireAttribute<int>(Weight, inherited: true);
        var child = ItemSchema<string>.Create("redefine-child")
            .WithParent(parent)
            .RequireAttribute<int>(Weight, inherited: true);
        var definition = SchemaValidationDefinition.Create("broken-child", child, defineWeight: true);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_AllowsChildToRedefineNonInheritedParentAttribute()
    {
        var parent = ItemSchema<string>.Create("non-inherited-parent")
            .RequireAttribute<int>(Weight, inherited: false);
        var child = ItemSchema<string>.Create("non-inherited-child")
            .WithParent(parent)
            .RequireAttribute<int>(Weight, inherited: true);
        var definition = SchemaValidationDefinition.Create("child", child, defineWeight: true);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void SchemaMutation_AfterCatalogFreeze_Throws()
    {
        var schema = ItemSchema<string>.Create("frozen-schema");
        var definition = SchemaValidationDefinition.Create("simple", schema);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.Throws<InvalidOperationException>(() => schema.RequireAttribute<int>(Weight));
        Assert.Throws<InvalidOperationException>(() => schema.AddTag("core:frozen"));
    }

    [Test]
    public void DefaultSchema_AllowsSimpleDefinitions()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(new ItemDefinition<string>("apple"));

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void DefaultSchema_AllowsSimpleDefinitionsWithTags()
    {
        var catalog = new ItemCatalog<string>();
        var definition = new ItemDefinition<string>("obsidian", ObsidianTag);

        DefineTags(catalog, ObsidianTag);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
        Assert.That(definition.Schema, Is.SameAs(ItemSchema<string>.Default));
    }

    [Test]
    public void DefinitionConstructor_AcceptsRepositoryTags()
    {
        var definition = new ItemDefinition<string>("obsidian", ObsidianTag);

        Assert.That(definition.HasTag(ObsidianTag), Is.True);
    }

    [Test]
    public void DerivedDefinition_ForwardsRepositoryTags()
    {
        var steelKnife = new RepositoryKnifeDefinition("steel_knife", 2, 120, 7, 10, SteelTag);
        var obsidianKnife = new RepositoryKnifeDefinition("obsidian_knife", 2, 80, 9, 14, ObsidianTag);

        Assert.That(steelKnife.Schema, Is.SameAs(RepositoryKnifeDefinition.KnifeSchema));
        Assert.That(obsidianKnife.Schema, Is.SameAs(RepositoryKnifeDefinition.KnifeSchema));
        Assert.That(steelKnife.HasTag(SteelTag), Is.True);
        Assert.That(steelKnife.HasTag(ObsidianTag), Is.False);
        Assert.That(obsidianKnife.HasTag(ObsidianTag), Is.True);
        Assert.That(obsidianKnife.HasTag(SteelTag), Is.False);
    }

    [Test]
    public void CatalogResolveTags_DistinguishesSchemaAndDefinitionSources()
    {
        var catalog = new ItemCatalog<string>();
        var obsidianKnife = new RepositoryKnifeDefinition("obsidian_knife", 2, 80, 9, 14, ObsidianTag);

        DefineAttributes(catalog);
        DefineTags(catalog, KnifeTag, ObsidianTag);
        catalog.Registry.Register(obsidianKnife);
        catalog.Freeze();

        var resolved = catalog.ResolveTags(obsidianKnife);
        Assert.That(resolved.Any(t => t.Id.Equals(KnifeTag) && t.Source == TagSource.Schema), Is.True);
        Assert.That(resolved.Any(t => t.Id.Equals(ObsidianTag) && t.Source == TagSource.Definition), Is.True);
    }

    [Test]
    public void RepositoryTags_DoNotRequireSubclasses()
    {
        var catalog = new ItemCatalog<string>();
        var steelKnife = new RepositoryKnifeDefinition("steel_knife", 2, 120, 7, 10, SteelTag);
        var obsidianKnife = new RepositoryKnifeDefinition("obsidian_knife", 2, 80, 9, 14, ObsidianTag);

        DefineAttributes(catalog);
        DefineTags(catalog, KnifeTag, SteelTag, ObsidianTag);
        catalog.Registry.Register(steelKnife);
        catalog.Registry.Register(obsidianKnife);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(steelKnife, KnifeTag), Is.True);
        Assert.That(catalog.Satisfies(obsidianKnife, KnifeTag), Is.True);
        Assert.That(catalog.Satisfies(steelKnife, SteelTag), Is.True);
        Assert.That(catalog.Satisfies(steelKnife, ObsidianTag), Is.False);
        Assert.That(catalog.Satisfies(obsidianKnife, ObsidianTag), Is.True);
        Assert.That(catalog.Satisfies(obsidianKnife, SteelTag), Is.False);
    }

    [Test]
    public void ItemCatalog_SupportsGuidDefinitionIds()
    {
        var id = Guid.NewGuid();
        var definition = new ItemDefinition<Guid>(id);
        var catalog = new ItemCatalog<Guid>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(id), Is.SameAs(definition));
    }

    [Test]
    public void InventoryManager_SupportsGuidDefinitionIds()
    {
        var id = Guid.NewGuid();
        var definition = new ItemDefinition<Guid>(id);
        var catalog = new ItemCatalog<Guid>();
        var manager = new InventoryManager<Guid>(
            new FixedSizeStackResolver<Guid>(10),
            new UnlimitedCapacityPolicy<Guid>(),
            new EntryLayout<Guid>(),
            catalog: catalog);

        catalog.Registry.Register(definition);
        catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(definition, out var error), Is.True, error);
        Assert.That(inventory.Items.Single().Definition.Id, Is.EqualTo(id));
    }

    [Test]
    public void ItemCatalog_SupportsIntDefinitionIds()
    {
        var definition = new ItemDefinition<int>(42);
        var catalog = new ItemCatalog<int>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(42), Is.SameAs(definition));
    }

    [Test]
    public void InventoryManager_SupportsIntDefinitionIds()
    {
        var definition = new ItemDefinition<int>(42);
        var catalog = new ItemCatalog<int>();
        var manager = new InventoryManager<int>(
            new FixedSizeStackResolver<int>(10),
            new UnlimitedCapacityPolicy<int>(),
            new EntryLayout<int>(),
            catalog: catalog);

        catalog.Registry.Register(definition);
        catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(definition, out var error), Is.True, error);
        Assert.That(inventory.Items.Single().Definition.Id, Is.EqualTo(42));
    }

    [Test]
    public void ItemCatalog_SupportsLongDefinitionIds()
    {
        var definition = new ItemDefinition<long>(42L);
        var catalog = new ItemCatalog<long>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(42L), Is.SameAs(definition));
    }

    [Test]
    public void ItemCatalog_SupportsEnumDefinitionIds()
    {
        var definition = new ItemDefinition<TestItemKind>(TestItemKind.Coin);
        var catalog = new ItemCatalog<TestItemKind>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(TestItemKind.Coin), Is.SameAs(definition));
    }

    [Test]
    public void ItemCatalog_SupportsCustomDefinitionIds()
    {
        var id = new TestItemId("coin");
        var equivalentId = new TestItemId("coin");
        var definition = new ItemDefinition<TestItemId>(id);
        var catalog = new ItemCatalog<TestItemId>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(equivalentId), Is.SameAs(definition));
    }

    [Test]
    public void ItemCatalog_SupportsFloatDefinitionIds()
    {
        var definition = new ItemDefinition<float>(1.5f);
        var catalog = new ItemCatalog<float>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(1.5f), Is.SameAs(definition));
    }

    [Test]
    public void ItemCatalog_SupportsDoubleDefinitionIds()
    {
        var definition = new ItemDefinition<double>(2.5d);
        var catalog = new ItemCatalog<double>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(2.5d), Is.SameAs(definition));
    }

    [Test]
    public void RegisterMigration_ResolvesOldIdToRegisteredDefinition()
    {
        var catalog = new ItemCatalog<string>();
        var coin = new ItemDefinition<string>("coin:copper");

        catalog.Registry.Register(coin);
        catalog.Registry.RegisterMigration("coin:old_copper", coin);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve("coin:old_copper"), Is.SameAs(coin));
    }

    [Test]
    public void RegisterMigration_RejectsMigrationFromRegisteredDefinition()
    {
        var catalog = new ItemCatalog<string>();
        var coin = new ItemDefinition<string>("coin:copper");
        catalog.Registry.Register(coin);

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterMigration("coin:copper", coin));
    }

    [Test]
    public void RegisterMigration_RejectsDuplicateSourceId()
    {
        var catalog = new ItemCatalog<string>();
        var copper = new ItemDefinition<string>("coin:copper");
        var silver = new ItemDefinition<string>("coin:silver");
        catalog.Registry.Register(copper);
        catalog.Registry.Register(silver);

        catalog.Registry.RegisterMigration("coin:old", copper);

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterMigration("coin:old", silver));
    }

    [Test]
    public void RegisterMigration_RejectsUnregisteredReplacementDefinition()
    {
        var catalog = new ItemCatalog<string>();
        var coin = new ItemDefinition<string>("coin:copper");

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterMigration("coin:old", coin));
    }

    [Test]
    public void RegisterMigration_RejectsDetachedReplacementDefinitionWithRegisteredId()
    {
        var catalog = new ItemCatalog<string>();
        var registeredCoin = new ItemDefinition<string>("coin:copper");
        var detachedCoin = new ItemDefinition<string>("coin:copper");
        catalog.Registry.Register(registeredCoin);

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterMigration("coin:old", detachedCoin));
    }

    [Test]
    public void RegisterMigration_AllowsMultipleOldIdsToSameReplacementDefinition()
    {
        var catalog = new ItemCatalog<string>();
        var coin = new ItemDefinition<string>("coin:copper");
        catalog.Registry.Register(coin);

        catalog.Registry.RegisterMigration("coin:v1", coin);
        catalog.Registry.RegisterMigration("coin:v2", coin);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve("coin:v1"), Is.SameAs(coin));
        Assert.That(catalog.Registry.Resolve("coin:v2"), Is.SameAs(coin));
    }

    [Test]
    public void RegisterMigration_RejectsNullReplacementDefinition()
    {
        var catalog = new ItemCatalog<string>();

        Assert.Throws<ArgumentNullException>(() => catalog.Registry.RegisterMigration("coin:old", null!));
    }

    [Test]
    public void RegisterMigration_RejectsWhenRegistryIsFrozen()
    {
        var catalog = new ItemCatalog<string>();
        var coin = new ItemDefinition<string>("coin:copper");
        catalog.Registry.Register(coin);
        catalog.Freeze();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterMigration("coin:old", coin));
    }

    [Test]
    public void RegisterMigration_ResolvesExplicitIntIds()
    {
        var catalog = new ItemCatalog<int>();
        var coin = new ItemDefinition<int>(1001);

        catalog.Registry.Register(coin);
        catalog.Registry.RegisterMigration(1, coin);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(1), Is.SameAs(coin));
    }

    [Test]
    public void RegisterMigration_ResolvesExplicitLongIds()
    {
        var catalog = new ItemCatalog<long>();
        var coin = new ItemDefinition<long>(1001L);

        catalog.Registry.Register(coin);
        catalog.Registry.RegisterMigration(1L, coin);
        catalog.Freeze();

        Assert.That(catalog.Registry.Resolve(1L), Is.SameAs(coin));
    }

    [Test]
    public void TagCatalog_DefaultMode_IsNamespaced()
    {
        var catalog = new TagCatalog();

        Assert.That(catalog.Mode, Is.EqualTo(TagCatalogMode.Namespaced));
    }

    [Test]
    public void TagCatalog_UseNamespacedTagsOnly_IsIdempotentBeforeTags()
    {
        var catalog = new TagCatalog();

        catalog.UseNamespacedTagsOnly();
        catalog.UseNamespacedTagsOnly();

        Assert.That(catalog.Mode, Is.EqualTo(TagCatalogMode.Namespaced));
    }

    [Test]
    public void TagCatalog_UseNonNamespacedTagsOnly_SwitchesModeBeforeTags()
    {
        var catalog = new TagCatalog();

        catalog.UseNonNamespacedTagsOnly();

        Assert.That(catalog.Mode, Is.EqualTo(TagCatalogMode.NonNamespaced));
    }

    [Test]
    public void TagCatalog_UseOppositeModeAfterExplicitMode_Throws()
    {
        var namespaced = new TagCatalog();
        namespaced.UseNamespacedTagsOnly();

        var nonNamespaced = new TagCatalog();
        nonNamespaced.UseNonNamespacedTagsOnly();

        Assert.Throws<InvalidOperationException>(() => namespaced.UseNonNamespacedTagsOnly());
        Assert.Throws<InvalidOperationException>(() => nonNamespaced.UseNamespacedTagsOnly());
    }

    [Test]
    public void TagCatalog_UseModeAfterTagsDefined_Throws()
    {
        var catalog = new TagCatalog();
        catalog.Define("core:food");

        Assert.Throws<InvalidOperationException>(() => catalog.UseNamespacedTagsOnly());
        Assert.Throws<InvalidOperationException>(() => catalog.UseNonNamespacedTagsOnly());
    }

    [Test]
    public void TagCatalog_Define_ReturnsNamespacedTagDefinition()
    {
        var catalog = new TagCatalog();

        var tag = catalog.Define("core:equipment.tools.axe");

        Assert.That(tag.Id, Is.EqualTo("core:equipment.tools.axe"));
        Assert.That(tag.Mode, Is.EqualTo(TagCatalogMode.Namespaced));
        Assert.That(tag.Namespace, Is.EqualTo("core"));
        Assert.That(tag.Path, Is.EqualTo("equipment.tools.axe"));
        Assert.That(tag.Segments, Is.EqualTo(new[] { "equipment", "tools", "axe" }));
        Assert.Throws<ArgumentException>(() => catalog.Define("Food"));
        Assert.Throws<ArgumentException>(() => catalog.Define("core:"));
        Assert.Throws<ArgumentException>(() => catalog.Define("core:equipment..axe"));
    }

    [Test]
    public void TagCatalog_NonNamespacedMode_DefinesFlatTag()
    {
        var catalog = new TagCatalog();
        catalog.UseNonNamespacedTagsOnly();

        var tag = catalog.Define("food");

        Assert.That(tag.Id, Is.EqualTo("food"));
        Assert.That(tag.Mode, Is.EqualTo(TagCatalogMode.NonNamespaced));
        Assert.That(tag.Namespace, Is.Null);
        Assert.That(tag.Path, Is.EqualTo("food"));
        Assert.That(tag.Segments, Is.EqualTo(new[] { "food" }));
    }

    [Test]
    public void TagCatalog_NonNamespacedMode_DefinesDotHierarchyParents()
    {
        var catalog = new TagCatalog();
        catalog.UseNonNamespacedTagsOnly();

        catalog.Define("equipment.tools.axe");

        Assert.That(catalog.Contains("equipment.tools.axe"), Is.True);
        Assert.That(catalog.Contains("equipment.tools"), Is.True);
        Assert.That(catalog.Contains("equipment"), Is.True);
    }

    [Test]
    public void TagCatalog_NonNamespacedMode_RejectsNamespacedTags()
    {
        var catalog = new TagCatalog();
        catalog.UseNonNamespacedTagsOnly();

        Assert.Throws<ArgumentException>(() => catalog.Define("core:food"));
    }

    [Test]
    public void TagCatalog_NamespacedMode_RejectsNonNamespacedTags()
    {
        var catalog = new TagCatalog();

        Assert.Throws<ArgumentException>(() => catalog.Define("consumables.food"));
    }

    [Test]
    public void TagCatalog_NonNamespacedMode_RejectsInvalidFlatTags()
    {
        var catalog = new TagCatalog();
        catalog.UseNonNamespacedTagsOnly();

        Assert.Throws<ArgumentException>(() => catalog.Define("equipment..tools"));
        Assert.Throws<ArgumentException>(() => catalog.Define("equipment tools"));
        Assert.Throws<ArgumentException>(() => catalog.Define("equipment/tools"));
    }

    [Test]
    public void TagCatalog_NonNamespacedMode_GetHierarchy_ReturnsDotParents()
    {
        var catalog = new TagCatalog();
        catalog.UseNonNamespacedTagsOnly();
        catalog.Define("equipment.tools.axe");

        var hierarchy = catalog.GetHierarchy("equipment.tools.axe").Select(t => t.Id).ToArray();

        Assert.That(hierarchy, Is.EqualTo(new[] { "equipment.tools", "equipment" }));
    }

    [Test]
    public void TagCatalog_Define_AddsGeneratedParents()
    {
        var catalog = new TagCatalog();
        var axe = "core:equipment.tools.axe";

        catalog.Define(axe);

        Assert.That(catalog.Contains(axe), Is.True);
        Assert.That(catalog.Contains("core:equipment.tools"), Is.True);
        Assert.That(catalog.Contains("core:equipment"), Is.True);
    }

    [Test]
    public void TagCatalog_DefineString_ReturnsCanonicalTag()
    {
        var catalog = new ItemCatalog<string>();
        var tag = catalog.Tags.Define("core:equipment.tools.knife");

        Assert.That(tag.Id, Is.EqualTo("core:equipment.tools.knife"));
        Assert.That(catalog.Tags.Contains(tag), Is.True);
        Assert.That(catalog.Tags.Contains("core:equipment.tools"), Is.True);
        Assert.That(catalog.Tags.Contains("core:equipment"), Is.True);
    }

    [Test]
    public void TagCatalog_Get_ReturnsDeclaredTag()
    {
        var catalog = new ItemCatalog<string>();
        var defined = catalog.Tags.Define("core:equipment.tools.knife");

        var resolved = catalog.Tags.Get("core:equipment.tools.knife");

        Assert.That(resolved.Id, Is.EqualTo(defined.Id));
    }

    [Test]
    public void TagCatalog_Get_ThrowsForUnknownTag()
    {
        var catalog = new ItemCatalog<string>();

        Assert.Throws<InvalidOperationException>(() => catalog.Tags.Get("core:missing"));
    }

    [Test]
    public void TagCatalog_DefineRejectsFlatTag()
    {
        var catalog = new ItemCatalog<string>();

        Assert.Throws<ArgumentException>(() => catalog.Tags.Define("Food"));
    }

    [Test]
    public void CatalogFreeze_NamespacedMode_RejectsNonNamespacedSchemaTag()
    {
        var schema = ItemSchema<string>.Create("flat-schema-tag").AddTag("food");
        var definition = SchemaTagDefinition.Create("apple", schema);
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define("core:food");

        catalog.Registry.Register(definition);

        Assert.Throws<ArgumentException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_NonNamespacedMode_AllowsNonNamespacedSchemaTag()
    {
        var schema = ItemSchema<string>.Create("flat-schema-tag").AddTag("food.fruit");
        var definition = SchemaTagDefinition.Create("apple", schema);
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("food.fruit");

        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_NonNamespacedMode_RejectsNamespacedSchemaTag()
    {
        var schema = ItemSchema<string>.Create("namespaced-schema-tag").AddTag("core:food");
        var definition = SchemaTagDefinition.Create("apple", schema);
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("food");

        catalog.Registry.Register(definition);

        Assert.Throws<ArgumentException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_NonNamespacedMode_AllowsNonNamespacedDefinitionTag()
    {
        var definition = new ItemDefinition<string>("apple", "food.fruit");
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("food.fruit");

        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_NonNamespacedMode_RejectsUndeclaredDefinitionTag()
    {
        var definition = new ItemDefinition<string>("apple", "food.fruit");
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_FailsWhenSchemaTagIsNotDeclared()
    {
        var schema = ItemSchema<string>.Create("undeclared-schema-tag")
            .AddTag(KnifeTag);
        var definition = SchemaTagDefinition.Create("knife", schema);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_FailsWhenDefinitionTagIsNotDeclared()
    {
        var definition = new ItemDefinition<string>("obsidian", ObsidianTag);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void DefinitionTagAuthoring_DefersModeValidationToCatalogFreeze()
    {
        var definition = new ItemDefinition<string>("food", "Food");

        Assert.That(definition.Tags, Is.EqualTo(new[] { "Food" }));
    }

    [Test]
    public void CatalogFreeze_SucceedsWhenSchemaAndDefinitionTagsAreDeclared()
    {
        var catalog = new ItemCatalog<string>();
        var definition = new RepositoryKnifeDefinition("obsidian_knife", 2, 80, 9, 14, ObsidianTag);

        DefineAttributes(catalog);
        DefineTags(catalog, KnifeTag, ObsidianTag);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void ModdedRepositoryDefinition_CanUseTagDeclaredAtRuntime()
    {
        var catalog = new ItemCatalog<string>();
        var modTag = catalog.Tags.Define("mymod:ritual.sacrificial_blade");
        var definition = new RepositoryKnifeDefinition("mymod_obsidian_ritual_knife", 2, 80, 9, 14, modTag);

        DefineAttributes(catalog);
        DefineTags(catalog, KnifeTag);
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(definition, modTag), Is.True);
    }

    [Test]
    public void CatalogResolvedTags_IncludeSchemaDefinitionAndGeneratedParentTags()
    {
        var material = "c:materials.obsidian";
        var definition = new AxeDefinition("obsidian-axe", weight: 5, durability: 10, chopPower: 20, material);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        DefineTags(catalog, AxeDefinition.AxeTag, material);
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(definition, "core:equipment.tools.axe"), Is.True);
        Assert.That(catalog.Satisfies(definition, "core:equipment.tools"), Is.True);
        Assert.That(catalog.Satisfies(definition, "core:equipment"), Is.True);
        Assert.That(catalog.Satisfies(definition, "core:equipment.tools.knife"), Is.False);
        Assert.That(catalog.Satisfies(definition, material), Is.True);
        Assert.That(catalog.Satisfies(definition, "c:materials"), Is.True);
        Assert.That(definition.HasTag("core:equipment.tools"), Is.False);

        var resolved = catalog.ResolveTags(definition);
        Assert.That(resolved.Any(t => t.Id.Equals(material) && t.Source == TagSource.Definition), Is.True);
        Assert.That(resolved.Any(t => t.Id.Equals("core:equipment.tools") && t.Source == TagSource.GeneratedParent), Is.True);
    }

    [Test]
    public void CatalogResolveTags_NonNamespacedMode_ReturnsDirectAndGeneratedParentTags()
    {
        var definition = new ItemDefinition<string>("axe", "equipment.tools.axe");
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("equipment.tools.axe");
        catalog.Registry.Register(definition);
        catalog.Freeze();

        var resolved = catalog.ResolveTags(definition);

        Assert.That(resolved.Any(t => t.Id == "equipment.tools.axe" && t.Source == TagSource.Definition), Is.True);
        Assert.That(resolved.Any(t => t.Id == "equipment.tools" && t.Source == TagSource.GeneratedParent), Is.True);
        Assert.That(resolved.Any(t => t.Id == "equipment" && t.Source == TagSource.GeneratedParent), Is.True);
    }

    [Test]
    public void CatalogSatisfies_NonNamespacedMode_UsesGeneratedParents()
    {
        var definition = new ItemDefinition<string>("axe", "equipment.tools.axe");
        var catalog = new ItemCatalog<string>();
        catalog.Tags.UseNonNamespacedTagsOnly();
        catalog.Tags.Define("equipment.tools.axe");
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(definition, "equipment.tools.axe"), Is.True);
        Assert.That(catalog.Satisfies(definition, "equipment.tools"), Is.True);
        Assert.That(catalog.Satisfies(definition, "equipment"), Is.True);
    }

    [Test]
    public void CatalogSatisfies_NamespacedMode_DoesNotAcceptFlatIds()
    {
        var definition = new ItemDefinition<string>("axe", "core:equipment.tools.axe");
        var catalog = new ItemCatalog<string>();
        catalog.Tags.Define("core:equipment.tools.axe");
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(definition, "equipment.tools.axe"), Is.False);
    }

    [Test]
    public void TagRules_UseCatalogResolvedMembership()
    {
        var requiredTool = "core:equipment.tools";
        var axe = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);
        var rules = new RuleContainer<string>();
        rules.Add("require-tool", new RequireAllTagsRule<string>(requiredTool));
        var manager = CreateManager(rules: rules);

        DefineAttributes(manager.Catalog);
        DefineTags(manager.Catalog, AxeDefinition.AxeTag);
        manager.Registry.Register(axe);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(axe, out var error), Is.True, error);
    }

    [Test]
    public void RequireAnyTagRule_UsesCatalogResolvedMembership()
    {
        var axe = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);
        var rules = new RuleContainer<string>();
        rules.Add("require-any", new RequireAnyTagRule<string>(
            "core:equipment.tools.knife",
            "core:equipment"));
        var manager = CreateManager(rules: rules);

        DefineAttributes(manager.Catalog);
        DefineTags(manager.Catalog, AxeDefinition.AxeTag);
        manager.Registry.Register(axe);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(axe, out var error), Is.True, error);
    }
}

