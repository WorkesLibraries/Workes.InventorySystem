using System;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryRuleMutationTests
{
    [Test]
    public void TrySetRule_RejectsRuleThatInvalidatesExistingContents()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);

        Assert.That(inventory.TryAdd(berry, out var addError), Is.True, addError);

        var accepted = inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
        Assert.That(inventory.TryAdd(berry, out var secondAddError), Is.True, secondAddError);
    }

    [Test]
    public void TrySetRule_AppliesRuleWhenCurrentContentsSatisfyIt()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True, addError);

        var accepted = inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.True);
        Assert.That(inventory.TryAdd(berry, out var rejectedError), Is.False);
        Assert.That(rejectedError, Does.Contain("only-apple"));
    }

    [Test]
    public void TrySetRuleEnabled_RejectsEnablingInvalidatingRule_AndLeavesItDisabled()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), enabled: false);
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(berry, out var addError), Is.True, addError);

        var accepted = inventory.TrySetRuleEnabled("only-apple", enabled: true, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.TryAdd(berry, out var secondAddError), Is.True, secondAddError);
    }

    [Test]
    public void TrySetRuleEnabled_EnablesValidRule()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), enabled: false);
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True, addError);

        var accepted = inventory.TrySetRuleEnabled("only-apple", enabled: true, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.TryAdd(berry, out var rejectedError), Is.False);
        Assert.That(rejectedError, Does.Contain("only-apple"));
    }

    [Test]
    public void TryRemoveRule_RemovesRuleAndAllowsFutureTransactions()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple));
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True, addError);

        var removed = inventory.TryRemoveRule("only-apple", out var error);

        Assert.That(removed, Is.True, error);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
        Assert.That(inventory.TryAdd(berry, out var berryError), Is.True, berryError);
    }

    [Test]
    public void CreateInventory_ClonesDefaultRulesForEachInventory()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var manager = CreateManager(null, apple, berry);
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();

        Assert.That(first.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var error), Is.True, error);

        Assert.That(first.TryAdd(berry, out _), Is.False);
        Assert.That(second.TryAdd(berry, out var secondError), Is.True, secondError);
    }

    [Test]
    public void SetRule_ThrowsWhenRuleInvalidatesExistingContents()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);

        inventory.Add(berry);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.SetRule("only-apple", new OnlyAllowItemsRule<string>(apple)));
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
    }

    private static Inventory<string> CreateInventory(
        RuleContainer<string>? rules,
        params ItemDefinition<string>[] definitions)
    {
        return CreateManager(rules, definitions).CreateInventory();
    }

    private static InventoryManager<string> CreateManager(
        RuleContainer<string>? rules,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            rules);

        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        manager.Catalog.Freeze();
        return manager;
    }
}
