using System;
using System.Linq;
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
    private static readonly AttributeKey<int> Weight = new("weight");
    private static readonly AttributeKey<int> Durability = new("durability");
    private static readonly AttributeKey<int> ChopPower = new("chopPower");
    private static readonly AttributeKey<int> Quality = new("quality");
    private static readonly AttributeKey<int> CutPower = new("cutPower");
    private static readonly AttributeKey<int> Damage = new("damage");
    private static readonly TagKey KnifeTag = TagKey.Parse("core:equipment.tools.knife");
    private static readonly TagKey ObsidianTag = TagKey.Parse("c:materials.obsidian");
    private static readonly TagKey SteelTag = TagKey.Parse("c:materials.steel");

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
                .RequireAttribute(Weight, inherited: true);

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
                .RequireAttribute(Durability, inherited: true);

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
                .RequireAttribute(ChopPower, inherited: true)
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
                .RequireAttribute(CutPower, inherited: true)
                .RequireAttribute(Damage, inherited: true)
                .AddTag(KnifeTag);

        public RepositoryKnifeDefinition(string id, int weight, int durability, int cutPower, int damage, params TagKey[] tags)
            : base(id, KnifeSchema, weight, durability)
        {
            DefineTags(tags);
            DefineAttribute(CutPower, cutPower);
            DefineAttribute(Damage, damage);
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
    public void RegistryFreeze_RemainsCompatible()
    {
        var manager = CreateManager();
        manager.Registry.Register(new ItemDefinition<string>("apple"));

        manager.Registry.Freeze();

        Assert.That(manager.Catalog.Frozen, Is.True);
        Assert.DoesNotThrow(() => manager.CreateInventory());
    }

    [Test]
    public void SchemaConstructorPattern_RegistersParentChain_AndValidatesAttributes()
    {
        var catalog = new ItemCatalog<string>();
        var axe = new AxeDefinition("axe", weight: 5, durability: 10, chopPower: 20);

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
            .RequireAttribute(Weight, inherited: true);
        var child = ItemSchema<string>.Create("missing-child")
            .WithParent(parent)
            .RequireAttribute(Durability, inherited: true);
        var definition = new SchemaValidationDefinition("broken-tool", child, defineDurability: true);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDirectRequiredAttributeIsMissing()
    {
        var schema = ItemSchema<string>.Create("missing-direct")
            .RequireAttribute(Weight, inherited: true);
        var definition = new SchemaValidationDefinition("broken-equipment", schema);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDefinitionProvidesUndeclaredAttribute()
    {
        var definition = new PolishedEquipmentDefinition("equipment", weight: 5, quality: 4);
        var catalog = new ItemCatalog<string>();

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

        catalog.Registry.Register(definition);

        Assert.DoesNotThrow(() => catalog.Freeze());
        Assert.That(definition.Attributes.TryGet(Weight, out var weight), Is.True);
        Assert.That(weight, Is.EqualTo(5));
    }

    [Test]
    public void CatalogFreeze_Fails_WhenDefinitionContainsAttributeNotDeclaredBySchema()
    {
        var schema = ItemSchema<string>.Create("undeclared-quality")
            .RequireAttribute(Weight, inherited: true);
        var definition = new SchemaValidationDefinition("equipment", schema, defineWeight: true, defineQuality: true);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_Fails_WhenChildRedefinesInheritedAttribute()
    {
        var parent = ItemSchema<string>.Create("redefine-parent")
            .RequireAttribute(Weight, inherited: true);
        var child = ItemSchema<string>.Create("redefine-child")
            .WithParent(parent)
            .RequireAttribute(Weight, inherited: true);
        var definition = new SchemaValidationDefinition("broken-child", child, defineWeight: true);
        var catalog = new ItemCatalog<string>();

        catalog.Registry.Register(definition);

        Assert.Throws<InvalidOperationException>(() => catalog.Freeze());
    }

    [Test]
    public void CatalogFreeze_AllowsChildToRedefineNonInheritedParentAttribute()
    {
        var parent = ItemSchema<string>.Create("non-inherited-parent")
            .RequireAttribute(Weight, inherited: false);
        var child = ItemSchema<string>.Create("non-inherited-child")
            .WithParent(parent)
            .RequireAttribute(Weight, inherited: true);
        var definition = new SchemaValidationDefinition("child", child, defineWeight: true);
        var catalog = new ItemCatalog<string>();

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

        Assert.Throws<InvalidOperationException>(() => schema.RequireAttribute(Weight));
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
        var definition = new ItemDefinition<string>("knife", schema);
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
    public void CatalogFreeze_SucceedsWhenSchemaAndDefinitionTagsAreDeclared()
    {
        var catalog = new ItemCatalog<string>();
        var definition = new RepositoryKnifeDefinition("obsidian_knife", 2, 80, 9, 14, ObsidianTag);

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

        DefineTags(manager.Catalog, AxeDefinition.AxeTag);
        manager.Registry.Register(axe);
        manager.Registry.Freeze();
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

        DefineTags(manager.Catalog, AxeDefinition.AxeTag);
        manager.Registry.Register(axe);
        manager.Registry.Freeze();
        var inventory = manager.CreateInventory();

        Assert.That(inventory.TryAdd(axe, out var error), Is.True, error);
    }
}
