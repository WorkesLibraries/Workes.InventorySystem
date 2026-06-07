using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.ItemUniverseFoundation;

[TestFixture]
[Category("Example")]
public class AttributeFocusedItemUniverseExampleTests
{
    private static class GameAttributes
    {
        public const string Weight = "weight";
        public const string Durability = "durability";
        public const string Damage = "damage";
        public const string CutPower = "cutPower";
        public const string ChopPower = "chopPower";
        public const string Armor = "armor";
        public const string CraftingValue = "craftingValue";
    }

    private class EquipmentDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> EquipmentSchema =
            ItemSchema<string>.CreateFor<EquipmentDefinition>("attribute_equipment")
                .RequireAttribute<int>("weight", inherited: true);

        protected EquipmentDefinition(string id, ItemSchema<string> schema, int weight)
            : base(id, schema)
        {
            DefineAttribute("weight", weight);
        }

        public EquipmentDefinition(string id, int weight)
            : this(id, EquipmentSchema, weight)
        {
        }
    }

    private class ToolDefinition : EquipmentDefinition
    {
        public static readonly ItemSchema<string> ToolSchema =
            ItemSchema<string>.CreateFor<ToolDefinition>("attribute_tool")
                .WithParent(EquipmentSchema)
                .RequireAttribute<int>("durability", inherited: true);

        protected ToolDefinition(string id, ItemSchema<string> schema, int weight, int durability)
            : base(id, schema, weight)
        {
            DefineAttribute("durability", durability);
        }

        public ToolDefinition(string id, int weight, int durability)
            : this(id, ToolSchema, weight, durability)
        {
        }
    }

    private class WeaponDefinition : EquipmentDefinition
    {
        public static readonly ItemSchema<string> WeaponSchema =
            ItemSchema<string>.CreateFor<WeaponDefinition>("attribute_weapon")
                .WithParent(EquipmentSchema)
                .RequireAttribute<int>("damage", inherited: true);

        protected WeaponDefinition(string id, ItemSchema<string> schema, int weight, int damage)
            : base(id, schema, weight)
        {
            DefineAttribute("damage", damage);
        }

        public WeaponDefinition(string id, int weight, int damage)
            : this(id, WeaponSchema, weight, damage)
        {
        }
    }

    private class KnifeDefinition : ToolDefinition
    {
        public static readonly ItemSchema<string> KnifeSchema =
            ItemSchema<string>.CreateFor<KnifeDefinition>("attribute_knife")
                .WithParent(ToolSchema)
                .RequireAttribute<int>("cutPower", inherited: true)
                .RequireAttribute<int>("damage", inherited: true);

        public KnifeDefinition(string id, int weight, int durability, int cutPower, int damage)
            : base(id, KnifeSchema, weight, durability)
        {
            DefineAttribute("cutPower", cutPower);
            DefineAttribute("damage", damage);
        }
    }

    private sealed class AxeDefinition : ToolDefinition
    {
        public static readonly ItemSchema<string> AxeSchema =
            ItemSchema<string>.CreateFor<AxeDefinition>("attribute_axe")
                .WithParent(ToolSchema)
                .RequireAttribute<int>("chopPower", inherited: true);

        public AxeDefinition(string id, int weight, int durability, int chopPower)
            : base(id, AxeSchema, weight, durability)
        {
            DefineAttribute("chopPower", chopPower);
        }
    }

    private sealed class ArmorDefinition : EquipmentDefinition
    {
        public static readonly ItemSchema<string> ArmorSchema =
            ItemSchema<string>.CreateFor<ArmorDefinition>("attribute_armor")
                .WithParent(EquipmentSchema)
                .RequireAttribute<int>("armor", inherited: true);

        public ArmorDefinition(string id, int weight, int armor)
            : base(id, ArmorSchema, weight)
        {
            DefineAttribute("armor", armor);
        }
    }

    private class MaterialDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> MaterialSchema =
            ItemSchema<string>.CreateFor<MaterialDefinition>("attribute_material")
                .RequireAttribute<int>("craftingValue", inherited: true);

        public MaterialDefinition(string id, int craftingValue)
            : base(id, MaterialSchema)
        {
            DefineAttribute("craftingValue", craftingValue);
        }
    }

    [Test]
    public void AttributeContracts_ArePracticalToAuthorAndInspect()
    {
        var catalog = new ItemCatalog<string>();
        DefineAttributes(catalog);
        var catalogWeight = catalog.Attributes.Get<int>("weight");
        var ironKnife = new KnifeDefinition("iron_knife", weight: 2, durability: 100, cutPower: 7, damage: 10);
        var obsidianKnife = new KnifeDefinition("obsidian_knife", weight: 2, durability: 80, cutPower: 9, damage: 14);
        var axe = new AxeDefinition("woodcutters_axe", weight: 6, durability: 90, chopPower: 16);
        var chestplate = new ArmorDefinition("iron_chestplate", weight: 8, armor: 18);
        var obsidianShard = new MaterialDefinition("obsidian_shard", craftingValue: 6);
        var definitions = new ItemDefinition<string>[] { ironKnife, obsidianKnife, axe, chestplate, obsidianShard };

        foreach (var definition in definitions)
            catalog.Registry.Register(definition);

        Assert.That(catalogWeight.Id, Is.EqualTo(GameAttributes.Weight));
        Assert.That(catalogWeight.ValueType, Is.EqualTo(typeof(int)));
        Assert.DoesNotThrow(() => catalog.Freeze());

        AssertHasAttributes(ironKnife, GameAttributes.Weight, GameAttributes.Durability, GameAttributes.CutPower, GameAttributes.Damage);
        AssertHasAttributes(axe, GameAttributes.Weight, GameAttributes.Durability, GameAttributes.ChopPower);
        AssertHasAttributes(chestplate, GameAttributes.Weight, GameAttributes.Armor);
        AssertHasAttributes(obsidianShard, GameAttributes.CraftingValue);

        var totalEquipmentWeight = definitions.Sum(GetWeightOrZero);
        var highestDamage = definitions
            .Select(d => new { Definition = d, Damage = d.Attributes.GetOrDefault(GameAttributes.Damage, 0) })
            .OrderByDescending(x => x.Damage)
            .ThenBy(x => x.Definition.Id, StringComparer.Ordinal)
            .First();
        var totalMaterialCraftingValue = definitions.Sum(d => d.Attributes.GetOrDefault(GameAttributes.CraftingValue, 0));

        Assert.That(totalEquipmentWeight, Is.EqualTo(18));
        Assert.That(highestDamage.Definition, Is.SameAs(obsidianKnife));
        Assert.That(highestDamage.Damage, Is.EqualTo(14));
        Assert.That(totalMaterialCraftingValue, Is.EqualTo(6));

        var lightweightRules = new RuleContainer<string>();
        lightweightRules.Add(
            "lightweight",
            new AttributePredicateRule<string, int>(
                GameAttributes.Weight,
                weight => weight <= 6,
                "Expected weight to be 6 or less"));
        var lightweightInventory = CreateManager(catalog, lightweightRules).CreateInventory();
        var lightweightResults = new List<(ItemDefinition<string> definition, bool accepted, string? error)>
        {
            EvaluateAdd(lightweightInventory, ironKnife),
            EvaluateAdd(lightweightInventory, axe),
            EvaluateAdd(lightweightInventory, chestplate),
            EvaluateAdd(lightweightInventory, obsidianShard)
        };

        Assert.That(lightweightResults[0].accepted, Is.True);
        Assert.That(lightweightResults[1].accepted, Is.True);
        Assert.That(lightweightResults[2].accepted, Is.False);
        Assert.That(lightweightResults[3].accepted, Is.False);

        var damageRules = new RuleContainer<string>();
        damageRules.Add(
            "known-damage-values",
            new AttributeOneOfValuesRule<string, int>(
                GameAttributes.Damage,
                10,
                14));
        var damageInventory = CreateManager(catalog, damageRules).CreateInventory();
        var damageResults = new List<(ItemDefinition<string> definition, bool accepted, string? error)>
        {
            EvaluateAdd(damageInventory, ironKnife),
            EvaluateAdd(damageInventory, obsidianKnife),
            EvaluateAdd(damageInventory, axe)
        };

        Assert.That(damageResults[0].accepted, Is.True);
        Assert.That(damageResults[1].accepted, Is.True);
        Assert.That(damageResults[2].accepted, Is.False);

        var artifactPath = WriteExampleOutput(
            catalog,
            definitions,
            totalEquipmentWeight,
            highestDamage.Definition,
            highestDamage.Damage,
            totalMaterialCraftingValue,
            lightweightResults,
            damageResults);
        TestContext.Out.WriteLine("Item universe foundation attributes example output: " + artifactPath);
    }

    private static void DefineAttributes(ItemCatalog<string> catalog)
    {
        catalog.Attributes.Define<int>("weight");
        catalog.Attributes.Define<int>("durability");
        catalog.Attributes.Define<int>("damage");
        catalog.Attributes.Define<int>("cutPower");
        catalog.Attributes.Define<int>("chopPower");
        catalog.Attributes.Define<int>("armor");
        catalog.Attributes.Define<int>("craftingValue");
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog, RuleContainer<string>? rules = null)
    {
        return new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog,
            rules
            );
    }

    private static (ItemDefinition<string> definition, bool accepted, string? error) EvaluateAdd(
        Inventory<string> inventory,
        ItemDefinition<string> definition)
    {
        var accepted = inventory.TryAdd(definition, out var error);
        return (definition, accepted, error);
    }

    private static void AssertHasAttributes(ItemDefinition<string> definition, params string[] attributes)
    {
        foreach (var attribute in attributes)
            Assert.That(definition.Attributes.Contains<int>(attribute), Is.True, $"{definition.Id} should have attribute {attribute}.");
    }

    private static int GetWeightOrZero(ItemDefinition<string> definition)
    {
        return definition.Attributes.GetOrDefault(GameAttributes.Weight, 0);
    }

    private static string WriteExampleOutput(
        ItemCatalog<string> catalog,
        IReadOnlyCollection<ItemDefinition<string>> definitions,
        int totalEquipmentWeight,
        ItemDefinition<string> highestDamageDefinition,
        int highestDamage,
        int totalMaterialCraftingValue,
        IReadOnlyCollection<(ItemDefinition<string> definition, bool accepted, string? error)> lightweightResults,
        IReadOnlyCollection<(ItemDefinition<string> definition, bool accepted, string? error)> damageResults)
    {
        var outputDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "ItemUniverseFoundation");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "AttributesExample.txt");
        var builder = new StringBuilder();

        builder.AppendLine("Item Universe Foundation - Attributes Example");
        builder.AppendLine("=============================================");
        builder.AppendLine();
        builder.AppendLine("Declared Attributes");
        builder.AppendLine("-------------------");
        foreach (var attribute in catalog.Attributes.All.OrderBy(a => a.Id, StringComparer.Ordinal))
            builder.AppendLine(attribute.Id + " (" + attribute.ValueType.Name + ")");

        builder.AppendLine();
        builder.AppendLine("Relevant Schemas");
        builder.AppendLine("----------------");

        foreach (var schema in catalog.Schemas.Schemas.OrderBy(s => s.Id, StringComparer.Ordinal))
            AppendSchema(builder, schema);

        builder.AppendLine("Registered Definitions");
        builder.AppendLine("----------------------");

        foreach (var definition in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
            AppendDefinition(builder, definition);

        builder.AppendLine("Derived Gameplay Views");
        builder.AppendLine("----------------------");
        builder.AppendLine("Total equipment weight: " + totalEquipmentWeight);
        builder.AppendLine("Highest damage item: " + highestDamageDefinition.Id + " (" + highestDamage + ")");
        builder.AppendLine("Total material crafting value: " + totalMaterialCraftingValue);
        builder.AppendLine();
        builder.AppendLine("Attribute Rule Checks");
        builder.AppendLine("---------------------");
        builder.AppendLine("Lightweight slot:");
        foreach (var result in lightweightResults.OrderBy(r => r.definition.Id, StringComparer.Ordinal))
            AppendRuleResult(builder, result, indent: "  ");

        builder.AppendLine();
        builder.AppendLine("Known damage slot:");
        foreach (var result in damageResults.OrderBy(r => r.definition.Id, StringComparer.Ordinal))
            AppendRuleResult(builder, result, indent: "  ");

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }

    private static void AppendRuleResult(
        StringBuilder builder,
        (ItemDefinition<string> definition, bool accepted, string? error) result,
        string indent)
    {
        builder.Append(indent);
        builder.Append(result.definition.Id);
        builder.Append(": ");
        builder.Append(result.accepted ? "accepted" : "rejected");
        if (!result.accepted && !string.IsNullOrWhiteSpace(result.error))
        {
            builder.Append(" (");
            builder.Append(result.error);
            builder.Append(')');
        }
        builder.AppendLine();
    }

    private static void AppendSchema(StringBuilder builder, ItemSchema<string> schema)
    {
        builder.AppendLine(schema.Id);
        builder.AppendLine("  Parent: " + (schema.Parent != null ? schema.Parent.Id : "none"));
        builder.AppendLine("  Required attributes:");
        foreach (var requirement in GetDirectRequirements(schema))
            builder.AppendLine("    " + requirement);

        var inherited = GetInheritedRequirements(schema).ToList();
        if (inherited.Count > 0)
        {
            builder.AppendLine("  Inherited requirements:");
            foreach (var requirement in inherited)
                builder.AppendLine("    " + requirement);
        }

        builder.AppendLine();
    }

    private static IEnumerable<string> GetDirectRequirements(ItemSchema<string> schema)
    {
        if (schema == EquipmentDefinition.EquipmentSchema)
            return new[] { "weight" };
        if (schema == ToolDefinition.ToolSchema)
            return new[] { "durability" };
        if (schema == WeaponDefinition.WeaponSchema)
            return new[] { "damage" };
        if (schema == KnifeDefinition.KnifeSchema)
            return new[] { "cutPower", "damage" };
        if (schema == AxeDefinition.AxeSchema)
            return new[] { "chopPower" };
        if (schema == ArmorDefinition.ArmorSchema)
            return new[] { "armor" };
        if (schema == MaterialDefinition.MaterialSchema)
            return new[] { "craftingValue" };

        return Array.Empty<string>();
    }

    private static IEnumerable<string> GetInheritedRequirements(ItemSchema<string> schema)
    {
        if (schema == ToolDefinition.ToolSchema)
            return new[] { "weight" };
        if (schema == WeaponDefinition.WeaponSchema)
            return new[] { "weight" };
        if (schema == KnifeDefinition.KnifeSchema)
            return new[] { "weight", "durability" };
        if (schema == AxeDefinition.AxeSchema)
            return new[] { "weight", "durability" };
        if (schema == ArmorDefinition.ArmorSchema)
            return new[] { "weight" };

        return Array.Empty<string>();
    }

    private static void AppendDefinition(StringBuilder builder, ItemDefinition<string> definition)
    {
        builder.AppendLine(definition.Id);
        builder.AppendLine("  Schema: " + definition.Schema.Id);
        builder.AppendLine("  Attributes:");

        AppendAttribute(builder, definition, GameAttributes.Weight);
        AppendAttribute(builder, definition, GameAttributes.Durability);
        AppendAttribute(builder, definition, GameAttributes.CutPower);
        AppendAttribute(builder, definition, GameAttributes.ChopPower);
        AppendAttribute(builder, definition, GameAttributes.Damage);
        AppendAttribute(builder, definition, GameAttributes.Armor);
        AppendAttribute(builder, definition, GameAttributes.CraftingValue);

        builder.AppendLine();
    }

    private static void AppendAttribute(StringBuilder builder, ItemDefinition<string> definition, string attribute)
    {
        if (!definition.Attributes.TryGet<int>(attribute, out var value))
            return;

        builder.AppendLine("    " + attribute + " = " + value);
    }
}


