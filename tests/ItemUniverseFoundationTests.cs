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
    private static readonly TagKey KnifeTag = TagKey.Parse("core:equipment.tools.knife");
    private static readonly TagKey ObsidianTag = TagKey.Parse("c:materials.obsidian");
    private static readonly TagKey SteelTag = TagKey.Parse("c:materials.steel");

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
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            rules,
            catalog);
    }

    private static void DefineTags(ItemCatalog<string> catalog, params TagKey[] tags)
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
        public SchemaValidationDefinition(
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
    }

    private class EquipmentDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> EquipmentSchema =
            ItemSchema<string>.Create("test-equipment")
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
            ItemSchema<string>.Create("test-tool")
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
        public static readonly TagKey AxeTag = TagKey.Parse("core:equipment.tools.axe");

        public static readonly ItemSchema<string> AxeSchema =
            ItemSchema<string>.Create("test-axe")
                .WithParent(ToolSchema)
                .RequireAttribute<int>(ChopPower, inherited: true)
                .AddTag(AxeTag);

        public AxeDefinition(string id, int weight, int durability, int chopPower)
            : base(id, AxeSchema, weight, durability)
        {
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
            ItemSchema<string>.Create("repository-knife")
                .WithParent(ToolSchema)
                .RequireAttribute<int>(CutPower, inherited: true)
                .RequireAttribute<int>(Damage, inherited: true)
                .AddTag(KnifeTag);

        public RepositoryKnifeDefinition(string id, int weight, int durability, int cutPower, int damage, params TagKey[] tags)
            : base(id, KnifeSchema, weight, durability)
        {
            DefineTags(tags);
            DefineAttribute(CutPower, cutPower);
            DefineAttribute(Damage, damage);
        }
    }

    private sealed class SchemaTagDefinition : ItemDefinition<string>
    {
        public SchemaTagDefinition(string id, ItemSchema<string> schema)
            : base(id, schema)
        {
        }
    }

    private sealed class StringAttributeDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StringAttributeSchema =
            ItemSchema<string>.Create("string-attribute-equipment")
                .RequireAttribute<int>("stringWeight", inherited: true);

        public StringAttributeDefinition(string id, int weight)
            : base(id, StringAttributeSchema)
        {
            DefineAttribute("stringWeight", weight);
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
        var definition = new SchemaValidationDefinition("broken-tool", child, defineDurability: true);
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
        var definition = new SchemaValidationDefinition("broken-equipment", schema);
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
    public void AttributeKey_IsNotPublicApi()
    {
        var exportedTypes = typeof(AttributeCatalog).Assembly.GetExportedTypes();

        Assert.That(exportedTypes.Any(t => t.Name.StartsWith("AttributeKey", StringComparison.Ordinal)), Is.False);
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
        var definition = new SchemaValidationDefinition("equipment", schema, defineWeight: true, defineQuality: true);
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
        var definition = new SchemaValidationDefinition("broken-child", child, defineWeight: true);
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
        var definition = new SchemaValidationDefinition("child", child, defineWeight: true);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
    }

    [Test]
    public void SchemaMutation_AfterCatalogFreeze_Throws()
    {
        var schema = ItemSchema<string>.Create("frozen-schema");
        var definition = new SchemaValidationDefinition("simple", schema);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.Throws<InvalidOperationException>(() => schema.RequireAttribute<int>(Weight));
        Assert.Throws<InvalidOperationException>(() => schema.AddTag(TagKey.Parse("core:frozen")));
    }

    [Test]
    public void DefaultSchema_AllowsSimpleDefinitions()
    {
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(new ItemDefinition<string>("apple"));

        Assert.DoesNotThrow(() => catalog.Freeze());
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
        Assert.That(resolved.Any(t => t.Tag.Equals(KnifeTag) && t.Source == TagSource.Schema), Is.True);
        Assert.That(resolved.Any(t => t.Tag.Equals(ObsidianTag) && t.Source == TagSource.Definition), Is.True);
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
            new DefaultStackResolver<Guid>(10),
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
            new DefaultStackResolver<int>(10),
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
    public void RegisterAuto_AssignsIntIdsStartingAtOne()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement();
        var coin = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));
        var potion = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));
        catalog.Freeze();

        Assert.That(coin.Id, Is.EqualTo(1));
        Assert.That(potion.Id, Is.EqualTo(2));
        Assert.That(catalog.Registry.Resolve(1), Is.SameAs(coin));
        Assert.That(catalog.Registry.Resolve(2), Is.SameAs(potion));
    }

    [Test]
    public void RegisterAuto_AssignsLongIdsStartingAtOne()
    {
        var catalog = new ItemCatalog<long>();

        catalog.Registry.EnableAutoIncrement();
        var coin = catalog.Registry.RegisterAuto(id => new ItemDefinition<long>(id));
        var potion = catalog.Registry.RegisterAuto(id => new ItemDefinition<long>(id));
        catalog.Freeze();

        Assert.That(coin.Id, Is.EqualTo(1L));
        Assert.That(potion.Id, Is.EqualTo(2L));
    }

    [Test]
    public void EnableAutoIncrement_UsesConfiguredFirstId()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement(100);
        var coin = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(coin.Id, Is.EqualTo(100));
    }

    [Test]
    public void RegisterAuto_ThrowsWhenAutoIncrementIsNotEnabled()
    {
        var catalog = new ItemCatalog<int>();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id)));
    }

    [Test]
    public void EnableAutoIncrement_ThrowsForUnsupportedKeyType()
    {
        var catalog = new ItemCatalog<string>();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.EnableAutoIncrement());
    }

    [Test]
    public void RegisterAuto_ThrowsWhenFactoryReturnsWrongId()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(99)));

        var coin = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));
        Assert.That(coin.Id, Is.EqualTo(1));
    }

    [Test]
    public void RegisterAuto_ThrowsWhenFactoryReturnsNull()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterAuto(id => null!));
    }

    [Test]
    public void EnableAutoIncrement_ThrowsWhenRegistryIsFrozen()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Freeze();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.EnableAutoIncrement());
    }

    [Test]
    public void EnableAutoIncrement_ThrowsWhenAlreadyEnabled()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement();

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.EnableAutoIncrement());
    }

    [Test]
    public void RegisterAuto_ThrowsOnOverflow()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement(int.MaxValue);
        var max = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(max.Id, Is.EqualTo(int.MaxValue));
        Assert.Throws<InvalidOperationException>(() => catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id)));
    }

    [Test]
    public void FollowMode_ExplicitIdBeforeEnableAdvancesInitialCounter()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.Register(new ItemDefinition<int>(10));
        catalog.Registry.EnableAutoIncrement(AutoIncrementMode.FollowExplicitRegistrations);
        var generated = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(generated.Id, Is.EqualTo(11));
    }

    [Test]
    public void FollowMode_ExplicitIdAfterEnableAdvancesCounter()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement();
        catalog.Registry.Register(new ItemDefinition<int>(10));
        var generated = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(generated.Id, Is.EqualTo(11));
    }

    [Test]
    public void FollowMode_ExplicitLowerIdDoesNotMoveCounterBackwards()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement(10);
        catalog.Registry.Register(new ItemDefinition<int>(3));
        var generated = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(generated.Id, Is.EqualTo(10));
    }

    [Test]
    public void FollowMode_RegisterAutoSkipsAlreadyRegisteredIds()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.Register(new ItemDefinition<int>(1));
        catalog.Registry.Register(new ItemDefinition<int>(2));
        catalog.Registry.EnableAutoIncrement();
        var generated = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(generated.Id, Is.EqualTo(3));
    }

    [Test]
    public void StrictMode_ThrowsWhenDefinitionsAlreadyRegistered()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.Register(new ItemDefinition<int>(1));

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.EnableAutoIncrement(AutoIncrementMode.Strict));
    }

    [Test]
    public void StrictMode_RegisterThrowsAfterAutoIncrementEnabled()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement(AutoIncrementMode.Strict);

        Assert.Throws<InvalidOperationException>(() => catalog.Registry.Register(new ItemDefinition<int>(1)));
    }

    [Test]
    public void StrictMode_RegisterAutoSucceeds()
    {
        var catalog = new ItemCatalog<int>();

        catalog.Registry.EnableAutoIncrement(AutoIncrementMode.Strict);
        var generated = catalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));

        Assert.That(generated.Id, Is.EqualTo(1));
        Assert.That(catalog.Registry.Resolve(1), Is.SameAs(generated));
    }

    [Test]
    public void TagKey_Parse_RequiresValidNamespacedId()
    {
        var tag = TagKey.Parse("core:equipment.tools.axe");

        Assert.That(tag.Namespace, Is.EqualTo("core"));
        Assert.That(tag.Path, Is.EqualTo("equipment.tools.axe"));
        Assert.That(tag.Segments, Is.EqualTo(new[] { "equipment", "tools", "axe" }));
        Assert.That(tag.IsNamespaced, Is.True);
        Assert.That(TagKey.TryParse("Food", out _), Is.False);
        Assert.That(TagKey.TryParse("core:", out _), Is.False);
        Assert.That(TagKey.TryParse("core:equipment..axe", out _), Is.False);
    }

    [Test]
    public void TagKey_Constructor_AllowsFlatCompatibilityTag()
    {
        var tag = new TagKey("Food");

        Assert.That(tag.Id, Is.EqualTo("Food"));
        Assert.That(tag.IsNamespaced, Is.False);
    }

    [Test]
    public void TagKey_Parse_RejectsFlatTag()
    {
        Assert.That(TagKey.TryParse("Food", out _), Is.False);
        Assert.Throws<ArgumentException>(() => TagKey.Parse("Food"));
    }

    [Test]
    public void TagCatalog_Define_AddsGeneratedParents()
    {
        var catalog = new TagCatalog();
        var axe = TagKey.Parse("core:equipment.tools.axe");

        catalog.Define(axe);

        Assert.That(catalog.Contains(axe), Is.True);
        Assert.That(catalog.Contains(TagKey.Parse("core:equipment.tools")), Is.True);
        Assert.That(catalog.Contains(TagKey.Parse("core:equipment")), Is.True);
    }

    [Test]
    public void TagCatalog_DefineString_ReturnsCanonicalTag()
    {
        var catalog = new ItemCatalog<string>();
        var tag = catalog.Tags.Define("core:equipment.tools.knife");

        Assert.That(tag.Id, Is.EqualTo("core:equipment.tools.knife"));
        Assert.That(catalog.Tags.Contains(tag), Is.True);
        Assert.That(catalog.Tags.Contains(TagKey.Parse("core:equipment.tools")), Is.True);
        Assert.That(catalog.Tags.Contains(TagKey.Parse("core:equipment")), Is.True);
    }

    [Test]
    public void TagCatalog_Get_ReturnsDeclaredTag()
    {
        var catalog = new ItemCatalog<string>();
        var defined = catalog.Tags.Define("core:equipment.tools.knife");

        var resolved = catalog.Tags.Get("core:equipment.tools.knife");

        Assert.That(resolved, Is.SameAs(defined));
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

        Assert.Throws<ArgumentException>(() => catalog.Tags.Define(new TagKey("Food")));
    }

    [Test]
    public void CatalogFreeze_FailsWhenSchemaTagIsNotDeclared()
    {
        var schema = ItemSchema<string>.Create("undeclared-schema-tag")
            .AddTag(KnifeTag);
        var definition = new SchemaTagDefinition("knife", schema);
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
    public void CatalogFreeze_FailsWhenDefinitionUsesFlatUndeclaredTag()
    {
        var definition = new ItemDefinition<string>("food", new TagKey("Food"));
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
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
        var definition = new AxeDefinition("obsidian-axe", weight: 5, durability: 10, chopPower: 20);
        var material = TagKey.Parse("c:materials.obsidian");
        definition.Tags.Add(material);
        var catalog = new ItemCatalog<string>();

        DefineAttributes(catalog);
        DefineTags(catalog, AxeDefinition.AxeTag, material);
        catalog.Registry.Register(definition);
        catalog.Freeze();

        Assert.That(catalog.Satisfies(definition, TagKey.Parse("core:equipment.tools.axe")), Is.True);
        Assert.That(catalog.Satisfies(definition, TagKey.Parse("core:equipment.tools")), Is.True);
        Assert.That(catalog.Satisfies(definition, TagKey.Parse("core:equipment")), Is.True);
        Assert.That(catalog.Satisfies(definition, TagKey.Parse("core:equipment.tools.knife")), Is.False);
        Assert.That(catalog.Satisfies(definition, material), Is.True);
        Assert.That(catalog.Satisfies(definition, TagKey.Parse("c:materials")), Is.True);
        Assert.That(definition.HasTag(TagKey.Parse("core:equipment.tools")), Is.False);

        var resolved = catalog.ResolveTags(definition);
        Assert.That(resolved.Any(t => t.Tag.Equals(material) && t.Source == TagSource.Definition), Is.True);
        Assert.That(resolved.Any(t => t.Tag.Equals(TagKey.Parse("core:equipment.tools")) && t.Source == TagSource.GeneratedParent), Is.True);
    }

    [Test]
    public void TagRules_UseCatalogResolvedMembership()
    {
        var requiredTool = TagKey.Parse("core:equipment.tools");
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
            TagKey.Parse("core:equipment.tools.knife"),
            TagKey.Parse("core:equipment")));
        var manager = CreateManager(rules: rules);

        DefineAttributes(manager.Catalog);
        DefineTags(manager.Catalog, AxeDefinition.AxeTag);
        manager.Registry.Register(axe);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(axe, out var error), Is.True, error);
    }
}



