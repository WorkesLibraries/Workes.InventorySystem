using System.Collections.Generic;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;
namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryRuleTests
{
    private static Inventory<string> CreateInventoryWithRules(
        IRulePolicy<string> rule,
        params ItemDefinition<string>[] definitions)
    {
        var ruleContainer = new RuleContainer<string>();
        ruleContainer.Add("main", rule);

        var manager = new InventoryManager<string>
        (
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            ruleContainer
        );

        foreach (var def in definitions)
            manager.Registry.Register(def);
        manager.Registry.Freeze();

        return manager.CreateInventory();
    }

    private static Inventory<string> CreateInventoryWithRules(
        params IRulePolicy<string>[] rules)
    {
        var ruleContainer = new RuleContainer<string>();
        for (int i = 0; i < rules.Length; i++)
            ruleContainer.Add($"r{i}", rules[i]);

        var manager = new InventoryManager<string>
        (
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            ruleContainer
        );
        manager.Registry.Freeze();

        return manager.CreateInventory();
    }

    private static bool TryApplyAdd(
        Inventory<string> inventory,
        ItemDefinition<string> definition,
        InstanceMetadata? metadata,
        int amount,
        out string? error)
    {
        error = null;
        var added = new List<(ItemDefinition<string> definition, InstanceMetadata? metadata, int amount)>
        {
            (definition, metadata, amount)
        };
        var removed = new List<(ItemDefinition<string> definition, InstanceMetadata? metadata, int amount)>();

        var normalized = new NormalizedInventoryTransaction<string>(added, removed);
        if (!inventory.TryFormulateFromNormalized(normalized, out var tx, out error) || tx == null)
            return false;

        inventory.CommitTransaction(tx);
        return true;
    }

    [Test]
    public void RequireTagRule_PassesWhenAllRequiredTagsExist_AndFailsWhenMissing()
    {
        var food = new TagKey("Food");
        var sweet = new TagKey("Sweet");

        var apple = new ItemDefinition<string>("apple");
        apple.Tags.Add(food);
        apple.Tags.Add(sweet);

        var berry = new ItemDefinition<string>("berry");
        berry.Tags.Add(food);

        var rule = new RequireTagRule<string>(food, sweet);
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        // Succeeds: has both tags.
        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        // Fails: missing 'Sweet'.
        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void RequireAnyTagRule_PassesWhenAtLeastOneRequiredTagExists_AndFailsWhenNone()
    {
        var food = new TagKey("Food");
        var healing = new TagKey("Healing");

        var apple = new ItemDefinition<string>("apple");
        apple.Tags.Add(healing);

        var berry = new ItemDefinition<string>("berry");
        // no tags

        var rule = new RequireAnyTagRule<string>(food, healing);
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void RequireMetadataRule_PassesWhenValueMatches_AndFailsWhenMissingOrWrong()
    {
        var rule = new RequireMetadataRule<string>("damageMultModifier", 5);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");

        var inventory = CreateInventoryWithRules(rule, apple, berry);

        var okMeta = new InstanceMetadata();
        okMeta.Set("damageMultModifier", 5);

        var wrongMeta = new InstanceMetadata();
        wrongMeta.Set("damageMultModifier", 6);

        Assert.That(TryApplyAdd(inventory, apple, okMeta, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, wrongMeta, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error3), Is.False);
        Assert.That(error3, Is.Not.Null);
    }

    [Test]
    public void RequireMetadataKeyRule_PassesWhenKeyExists_AndFailsWhenMissing()
    {
        var key = "damageMultModifier";
        var rule = new RequireMetadataKeyRule<string>(key);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        var okMeta = new InstanceMetadata();
        okMeta.Set(key, 10);

        Assert.That(TryApplyAdd(inventory, apple, okMeta, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);

        var wrongMeta = new InstanceMetadata();
        wrongMeta.Set("otherKey", 123);
        Assert.That(TryApplyAdd(inventory, berry, wrongMeta, amount: 1, out var error3), Is.False);
        Assert.That(error3, Is.Not.Null);
    }

    [Test]
    public void MetadataRangeRule_PassesWhenInRange_AndFailsWhenOutOfRangeOrMissing()
    {
        var rule = new MetadataRangeRule<string, int>("level", min: 10, max: 20);
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        var okMeta = new InstanceMetadata();
        okMeta.Set("level", 15);

        var tooHighMeta = new InstanceMetadata();
        tooHighMeta.Set("level", 25);

        Assert.That(TryApplyAdd(inventory, apple, okMeta, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, tooHighMeta, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error3), Is.False);
        Assert.That(error3, Is.Not.Null);
    }

    [Test]
    public void RequireMetadataOneOfValuesRule_PassesWhenValueIsAllowed_AndFailsWhenNotAllowedOrMissing()
    {
        var rule = new RequireMetadataOneOfValuesRule<string>("rarity", "Common", "Rare");
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        var okMeta = new InstanceMetadata();
        okMeta.Set("rarity", "Rare");

        var wrongMeta = new InstanceMetadata();
        wrongMeta.Set("rarity", "Epic");

        Assert.That(TryApplyAdd(inventory, apple, okMeta, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, wrongMeta, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error3), Is.False);
        Assert.That(error3, Is.Not.Null);
    }

    [Test]
    public void ItemPredicateRule_PassesWhenPredicateIsSatisfied_AndFailsWhenNot()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");

        var rule = new ItemPredicateRule<string>(
            predicate: (def, meta) => def.Id == "apple",
            errorMessage: "Expected only apples");

        var inventory = CreateInventoryWithRules(rule, apple, berry);

        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void OnlyAllowItemsRule_PassesForWhitelistedItems_AndFailsForOtherItems()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var carrot = new ItemDefinition<string>("carrot");

        var rule = new OnlyAllowItemsRule<string>(apple, berry);
        var inventory = CreateInventoryWithRules(rule, apple, berry, carrot);

        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, carrot, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void NotRule_PassesWhenWrappedRuleFails_AndFailsWhenWrappedRuleSucceeds()
    {
        var key = "damageMultModifier";
        var inner = new RequireMetadataKeyRule<string>(key);
        var rule = new NotRule<string>(inner);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        var okMeta = new InstanceMetadata();
        okMeta.Set(key, 10);

        // Wrapped rule fails (missing key) => NotRule passes.
        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        // Wrapped rule succeeds (key exists) => NotRule fails.
        Assert.That(TryApplyAdd(inventory, berry, okMeta, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void OrRule_PassesWhenAnyNestedRuleSucceeds_AndFailsWhenAllFail()
    {
        var food = new TagKey("Food");

        var requireFood = new RequireTagRule<string>(food);
        var unique = new UniqueItemRule<string>(maxInstancesPerItem: 1);
        var rule = new OrRule<string>(requireFood, unique);

        var apple = new ItemDefinition<string>("apple"); // missing Food tag
        var berry = new ItemDefinition<string>("berry"); // also missing Food tag

        var inventory = CreateInventoryWithRules(rule, apple, berry);

        // requireFood fails, but unique succeeds (inventory empty) => allow.
        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        var secondAppleMetadata = new InstanceMetadata();
        secondAppleMetadata.Set("quality", "different");

        // requireFood still fails, unique fails because metadata forces a second apple stack => reject.
        Assert.That(TryApplyAdd(inventory, apple, secondAppleMetadata, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);

        // Different item definition: unique would be violated only if maxUnique logic was different.
        // Here unique is per-definition, so adding berry should still be allowed.
        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error3), Is.True);
        Assert.That(error3, Is.Null);
    }

    [Test]
    public void UniqueItemRule_PassesUpToLimit_AndFailsWhenExceeding()
    {
        var rule = new UniqueItemRule<string>(maxInstancesPerItem: 1);

        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventoryWithRules(rule, apple);

        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 10, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        var secondStackMetadata = new InstanceMetadata();
        secondStackMetadata.Set("quality", "different");

        Assert.That(TryApplyAdd(inventory, apple, secondStackMetadata, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }

    [Test]
    public void MaxUniqueItemsRule_PassesWhenWithinTypeLimit_AndFailsWhenExceeded()
    {
        var rule = new MaxUniqueItemsRule<string>(maxUniqueDefinitions: 1);

        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventoryWithRules(rule, apple, berry);

        Assert.That(TryApplyAdd(inventory, apple, metadata: null, amount: 1, out var error1), Is.True);
        Assert.That(error1, Is.Null);

        Assert.That(TryApplyAdd(inventory, berry, metadata: null, amount: 1, out var error2), Is.False);
        Assert.That(error2, Is.Not.Null);
    }
}
