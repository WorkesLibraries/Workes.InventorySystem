using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests.Examples.ItemUniverseFoundation;

[TestFixture]
[Category("Example")]
public class TagFocusedItemUniverseExampleTests
{
    private static class GameAttributes
    {
        public static readonly AttributeKey<int> Weight = new("weight");
        public static readonly AttributeKey<int> Durability = new("durability");
        public static readonly AttributeKey<int> ChopPower = new("chopPower");
        public static readonly AttributeKey<int> CutPower = new("cutPower");
        public static readonly AttributeKey<int> Damage = new("damage");
        public static readonly AttributeKey<int> CraftingValue = new("craftingValue");
    }

    private static class GameTags
    {
        public static class Equipment
        {
            public static class Tools
            {
                public static readonly TagKey Axe = TagKey.Parse("c:equipment.tools.axe");
                public static readonly TagKey Knife = TagKey.Parse("c:equipment.tools.knife");
            }

            public static class Weapons
            {
                public static readonly TagKey Blade = TagKey.Parse("c:equipment.weapons.blade");
            }
        }

        public static class Materials
        {
            public static readonly TagKey Raw = TagKey.Parse("c:materials.raw");
            public static readonly TagKey Obsidian = TagKey.Parse("c:materials.obsidian");
            public static readonly TagKey Steel = TagKey.Parse("c:materials.steel");
        }
    }

    private class EquipmentDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> EquipmentSchema =
            ItemSchema<string>.Create("equipment")
                .RequireAttribute(GameAttributes.Weight, inherited: true);

        protected EquipmentDefinition(string id, ItemSchema<string> schema, int weight, params TagKey[] tags)
            : base(id, schema, tags)
        {
            DefineAttribute(GameAttributes.Weight, weight);
        }

        public EquipmentDefinition(string id, int weight, params TagKey[] tags)
            : this(id, EquipmentSchema, weight, tags)
        {
        }
    }

    private class ToolDefinition : EquipmentDefinition
    {
        public static readonly ItemSchema<string> ToolSchema =
            ItemSchema<string>.Create("tool")
                .WithParent(EquipmentSchema)
                .RequireAttribute(GameAttributes.Durability, inherited: true);

        protected ToolDefinition(string id, ItemSchema<string> schema, int weight, int durability, params TagKey[] tags)
            : base(id, schema, weight, tags)
        {
            DefineAttribute(GameAttributes.Durability, durability);
        }

        public ToolDefinition(string id, int weight, int durability, params TagKey[] tags)
            : this(id, ToolSchema, weight, durability, tags)
        {
        }
    }

    private sealed class AxeDefinition : ToolDefinition
    {
        public static readonly ItemSchema<string> AxeSchema =
            ItemSchema<string>.Create("axe")
                .WithParent(ToolSchema)
                .RequireAttribute(GameAttributes.ChopPower, inherited: true)
                .AddTag(GameTags.Equipment.Tools.Axe);

        public AxeDefinition(string id, int weight, int durability, int chopPower, params TagKey[] tags)
            : base(id, AxeSchema, weight, durability, tags)
        {
            DefineAttribute(GameAttributes.ChopPower, chopPower);
        }
    }

    private class KnifeDefinition : ToolDefinition
    {
        public static readonly ItemSchema<string> KnifeSchema =
            ItemSchema<string>.Create("knife")
                .WithParent(ToolSchema)
                .RequireAttribute(GameAttributes.CutPower, inherited: true)
                .RequireAttribute(GameAttributes.Damage, inherited: true)
                .AddTag(GameTags.Equipment.Tools.Knife)
                .AddTag(GameTags.Equipment.Weapons.Blade);

        protected KnifeDefinition(string id, ItemSchema<string> schema, int weight, int durability, int cutPower, int damage, params TagKey[] tags)
            : base(id, schema, weight, durability, tags)
        {
            DefineAttribute(GameAttributes.CutPower, cutPower);
            DefineAttribute(GameAttributes.Damage, damage);
        }

        public KnifeDefinition(string id, int weight, int durability, int cutPower, int damage, params TagKey[] tags)
            : this(id, KnifeSchema, weight, durability, cutPower, damage, tags)
        {
        }
    }

    private class RawMaterialDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> RawMaterialSchema =
            ItemSchema<string>.Create("raw_material")
                .RequireAttribute(GameAttributes.CraftingValue, inherited: true)
                .AddTag(GameTags.Materials.Raw);

        protected RawMaterialDefinition(string id, ItemSchema<string> schema, int craftingValue, params TagKey[] tags)
            : base(id, schema, tags)
        {
            DefineAttribute(GameAttributes.CraftingValue, craftingValue);
        }

        public RawMaterialDefinition(string id, int craftingValue, params TagKey[] tags)
            : this(id, RawMaterialSchema, craftingValue, tags)
        {
        }
    }

    [Test]
    public void TagHeavyItemUniverse_IsPracticalToAuthorAndInspect()
    {
        var catalog = new ItemCatalog<string>();
        DefineCoreTags(catalog);
        var moddedRitualSacrificial = catalog.Tags.Define("mymod:ritual.sacrificial");
        var moddedRitualComponent = catalog.Tags.Define("mymod:ritual.component");
        var obsidianKnife = new KnifeDefinition(
            "obsidian_ritual_knife",
            weight: 2,
            durability: 80,
            cutPower: 9,
            damage: 14,
            GameTags.Materials.Obsidian,
            moddedRitualSacrificial);
        var steelKnife = new KnifeDefinition(
            "steel_knife",
            weight: 2,
            durability: 120,
            cutPower: 7,
            damage: 10,
            GameTags.Materials.Steel);
        var axe = new AxeDefinition("woodcutters_axe", weight: 7, durability: 90, chopPower: 16);
        var obsidianShard = new RawMaterialDefinition(
            "obsidian_shard",
            craftingValue: 6,
            GameTags.Materials.Obsidian,
            moddedRitualComponent);
        var definitions = new ItemDefinition<string>[] { obsidianKnife, steelKnife, axe, obsidianShard };

        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();

        AssertSatisfies(catalog, obsidianKnife,
            GameTags.Equipment.Tools.Knife,
            TagKey.Parse("c:equipment.tools"),
            TagKey.Parse("c:equipment"),
            GameTags.Materials.Obsidian,
            moddedRitualSacrificial);

        AssertSatisfies(catalog, steelKnife,
            GameTags.Equipment.Tools.Knife,
            TagKey.Parse("c:equipment.tools"),
            TagKey.Parse("c:equipment"),
            GameTags.Materials.Steel);
        Assert.That(catalog.Satisfies(steelKnife, GameTags.Materials.Obsidian), Is.False);

        AssertSatisfies(catalog, obsidianShard,
            GameTags.Materials.Raw,
            GameTags.Materials.Obsidian,
            moddedRitualComponent);
        Assert.That(catalog.Satisfies(obsidianShard, GameTags.Equipment.Tools.Knife), Is.False);

        var backpack = CreateManager(catalog).CreateInventory();
        foreach (var definition in definitions)
            Assert.That(backpack.TryAdd(definition, out var backpackError), Is.True, backpackError);

        var ritualRules = new RuleContainer<string>();
        ritualRules.Add("ritual-knife", new RequireAllTagsRule<string>(GameTags.Equipment.Tools.Knife, GameTags.Materials.Obsidian));
        var ritualInput = CreateManager(catalog, ritualRules).CreateInventory();

        var ritualResults = new List<(ItemDefinition<string> definition, bool accepted, string? error)>
        {
            EvaluateAdd(ritualInput, obsidianKnife),
            EvaluateAdd(ritualInput, steelKnife),
            EvaluateAdd(ritualInput, obsidianShard)
        };

        Assert.That(ritualResults[0].accepted, Is.True);
        Assert.That(ritualResults[1].accepted, Is.False);
        Assert.That(ritualResults[2].accepted, Is.False);

        var toolBeltRules = new RuleContainer<string>();
        toolBeltRules.Add("tool", new RequireAnyTagRule<string>(GameTags.Equipment.Tools.Knife, GameTags.Equipment.Tools.Axe));
        var toolBelt = CreateManager(catalog, toolBeltRules).CreateInventory();

        var toolBeltResults = new List<(ItemDefinition<string> definition, bool accepted, string? error)>
        {
            EvaluateAdd(toolBelt, obsidianKnife),
            EvaluateAdd(toolBelt, axe),
            EvaluateAdd(toolBelt, obsidianShard)
        };

        Assert.That(toolBeltResults[0].accepted, Is.True);
        Assert.That(toolBeltResults[1].accepted, Is.True);
        Assert.That(toolBeltResults[2].accepted, Is.False);

        var artifactPath = WriteExampleOutput(catalog, definitions, ritualResults, toolBeltResults);
        TestContext.Out.WriteLine("Item universe foundation tags example output: " + artifactPath);
    }

    private static InventoryManager<string> CreateManager(ItemCatalog<string> catalog, RuleContainer<string>? rules = null)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            rules,
            catalog);
    }

    private static void DefineCoreTags(ItemCatalog<string> catalog)
    {
        catalog.Tags.Define(GameTags.Equipment.Tools.Axe);
        catalog.Tags.Define(GameTags.Equipment.Tools.Knife);
        catalog.Tags.Define(GameTags.Equipment.Weapons.Blade);
        catalog.Tags.Define(GameTags.Materials.Raw);
        catalog.Tags.Define(GameTags.Materials.Obsidian);
        catalog.Tags.Define(GameTags.Materials.Steel);
    }

    private static void AssertSatisfies(ItemCatalog<string> catalog, ItemDefinition<string> definition, params TagKey[] tags)
    {
        foreach (var tag in tags)
            Assert.That(catalog.Satisfies(definition, tag), Is.True, $"{definition.Id} should satisfy {tag}.");
    }

    private static (ItemDefinition<string> definition, bool accepted, string? error) EvaluateAdd(
        Inventory<string> inventory,
        ItemDefinition<string> definition)
    {
        var accepted = inventory.TryAdd(definition, out var error);
        return (definition, accepted, error);
    }

    private static string WriteExampleOutput(
        ItemCatalog<string> catalog,
        IReadOnlyCollection<ItemDefinition<string>> definitions,
        IReadOnlyCollection<(ItemDefinition<string> definition, bool accepted, string? error)> ritualResults,
        IReadOnlyCollection<(ItemDefinition<string> definition, bool accepted, string? error)> toolBeltResults)
    {
        var outputDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "ItemUniverseFoundation");
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "TagsExample.txt");
        var builder = new StringBuilder();

        builder.AppendLine("Item Universe Foundation Example");
        builder.AppendLine("================================");
        builder.AppendLine();
        builder.AppendLine("Declared Tags");
        builder.AppendLine("-------------");

        foreach (var tag in catalog.Tags.All.OrderBy(t => t.Id, StringComparer.Ordinal))
            builder.AppendLine(tag.Id);

        builder.AppendLine();
        builder.AppendLine("Relevant Schemas");
        builder.AppendLine("----------------");

        foreach (var schema in catalog.Schemas.Schemas.OrderBy(s => s.Id, StringComparer.Ordinal))
            AppendSchema(builder, schema);

        builder.AppendLine("Registered Definitions");
        builder.AppendLine("----------------------");

        foreach (var definition in definitions.OrderBy(d => d.Id, StringComparer.Ordinal))
            AppendDefinition(builder, catalog, definition);

        builder.AppendLine("Ritual Input Checks");
        builder.AppendLine("-------------------");
        foreach (var result in ritualResults.OrderBy(r => r.definition.Id, StringComparer.Ordinal))
        {
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

        builder.AppendLine();
        builder.AppendLine("Tool Belt Checks");
        builder.AppendLine("----------------");
        foreach (var result in toolBeltResults.OrderBy(r => r.definition.Id, StringComparer.Ordinal))
        {
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

        File.WriteAllText(outputPath, builder.ToString());
        return outputPath;
    }

    private static void AppendSchema(StringBuilder builder, ItemSchema<string> schema)
    {
        builder.AppendLine(schema.Id);
        builder.AppendLine("  Parent: " + (schema.Parent != null ? schema.Parent.Id : "none"));
        builder.AppendLine("  Direct schema tags:");

        var directTags = schema.DirectTags.OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (directTags.Count == 0)
        {
            builder.AppendLine("    none");
        }
        else
        {
            foreach (var tag in directTags)
                builder.AppendLine("    " + tag.Id);
        }

        builder.AppendLine();
    }

    private static void AppendDefinition(StringBuilder builder, ItemCatalog<string> catalog, ItemDefinition<string> definition)
    {
        builder.AppendLine(definition.Id);
        builder.AppendLine("  Schema: " + definition.Schema.Id);
        builder.AppendLine("  Direct definition tags:");

        var directTags = definition.Tags.All().OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (directTags.Count == 0)
        {
            builder.AppendLine("    none");
        }
        else
        {
            foreach (var tag in directTags)
                builder.AppendLine("    " + tag.Id);
        }

        builder.AppendLine("  Resolved tags:");
        foreach (var resolved in catalog.ResolveTags(definition).OrderBy(t => t.Tag.Id, StringComparer.Ordinal))
        {
            builder.Append("    ");
            builder.Append(resolved.Tag.Id);
            builder.Append(" [");
            builder.Append(resolved.Source);
            if (resolved.Origin != null && !resolved.Origin.Equals(resolved.Tag))
            {
                builder.Append(", origin=");
                builder.Append(resolved.Origin.Id);
            }
            builder.AppendLine("]");
        }

        builder.AppendLine();
    }
}
