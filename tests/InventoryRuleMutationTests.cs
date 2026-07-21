using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
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

        Assert.That(inventory.TryAdd(berry, out var addError), Is.True);

        var accepted = inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure, Is.Not.Null);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
        Assert.That(inventory.TryAdd(berry, out var secondAddError), Is.True, secondAddError?.Message);
    }

    [Test]
    public void TrySetRule_AppliesRuleWhenCurrentContentsSatisfyIt()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True);

        var accepted = inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.True);
        Assert.That(inventory.TryAdd(berry, out var rejectedError), Is.False);
        Assert.That(rejectedError?.Message, Does.Contain("only-apple"));
    }

    [Test]
    public void TrySetRuleEnabled_RejectsEnablingInvalidatingRule_AndLeavesItDisabled()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), enabled: false);
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(berry, out var addError), Is.True);

        var accepted = inventory.TrySetRuleEnabled("only-apple", enabled: true, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure, Is.Not.Null);
        Assert.That(inventory.TryAdd(berry, out var secondAddError), Is.True, secondAddError?.Message);
    }

    [Test]
    public void TrySetRuleEnabled_EnablesValidRule()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), enabled: false);
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True);

        var accepted = inventory.TrySetRuleEnabled("only-apple", enabled: true, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.TryAdd(berry, out var rejectedError), Is.False);
        Assert.That(rejectedError?.Message, Does.Contain("only-apple"));
    }

    [Test]
    public void TryRemoveRule_RemovesRuleAndAllowsFutureTransactions()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple));
        var inventory = CreateInventory(rules, apple, berry);

        Assert.That(inventory.TryAdd(apple, out var addError), Is.True);

        var removed = inventory.TryRemoveRule("only-apple", out var failure);

        Assert.That(removed, Is.True);
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
        Assert.That(inventory.TryAdd(berry, out var berryError), Is.True, berryError?.Message);
    }

    [Test]
    public void CreateInventory_ClonesDefaultRulesForEachInventory()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var manager = CreateManager(null, apple, berry);
        var first = manager.CreateInventory();
        var second = manager.CreateInventory();

        Assert.That(first.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out var failure), Is.True);

        Assert.That(first.TryAdd(berry, out _), Is.False);
        Assert.That(second.TryAdd(berry, out var secondError), Is.True, secondError?.Message);
    }

    [Test]
    public void SetRule_ThrowsWhenRuleInvalidatesExistingContents()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);

        inventory.Add(berry);

        Assert.Throws<InventoryOperationException>(() =>
            inventory.SetRule("only-apple", new OnlyAllowItemsRule<string>(apple)));
        Assert.That(inventory.Rules.ContainsKey("only-apple"), Is.False);
    }

    [Test]
    public void TrySetRule_AddsRuleAndEmitsConfigurationChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(null, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), priority: 5, enabled: true, out var failure), Is.True);

        var change = SingleRuleChange(events);
        Assert.That(change.Kind, Is.EqualTo(InventoryConfigurationChangeKind.Rules));
        Assert.That(change.ConfigurationId, Is.EqualTo("only-apple"));
#pragma warning disable CS0618
        Assert.That(change.ParameterId, Is.EqualTo("only-apple"));
#pragma warning restore CS0618
        Assert.That(change.Value, Is.Null);
        Assert.That(change.RequiresFullRefresh, Is.False);
        Assert.That(events.Single().RequiresFullRefresh, Is.False);
        Assert.That(events.Single().AffectedLayoutContexts, Is.Empty);
        Assert.That(change.RuleChange!.ChangeKind, Is.EqualTo(InventoryRuleConfigurationChangeKind.Added));
        Assert.That(change.RuleChange.PreviousState, Is.Null);
        Assert.That(change.RuleChange.CurrentState!.Id, Is.EqualTo("only-apple"));
        Assert.That(change.RuleChange.CurrentState.Priority, Is.EqualTo(5));
        Assert.That(change.RuleChange.CurrentState.Enabled, Is.True);
    }

    [Test]
    public void TrySetRule_ReplacesRuleAndReportsBeforeAndAfterState()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        var previousRule = new OnlyAllowItemsRule<string>(apple);
        rules.Add("active", previousRule, priority: 2, enabled: false);
        var inventory = CreateInventory(rules, apple, berry);
        var replacementRule = new OnlyAllowItemsRule<string>(berry);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRule("active", replacementRule, out var failure), Is.True);

        var ruleChange = SingleRuleChange(events).RuleChange!;
        Assert.That(ruleChange.ChangeKind, Is.EqualTo(InventoryRuleConfigurationChangeKind.Replaced));
        Assert.That(ruleChange.PreviousState!.Priority, Is.EqualTo(2));
        Assert.That(ruleChange.PreviousState.Enabled, Is.False);
        Assert.That(ruleChange.CurrentState!.Priority, Is.EqualTo(2));
        Assert.That(ruleChange.CurrentState.Enabled, Is.False);
        Assert.That(ruleChange.PreviousState.Policy, Is.Not.SameAs(ruleChange.CurrentState.Policy));
    }

    [Test]
    public void TryRemoveRule_RemovesRuleAndEmitsConfigurationChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), priority: 3, enabled: true);
        var inventory = CreateInventory(rules, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TryRemoveRule("only-apple", out var failure), Is.True);

        var ruleChange = SingleRuleChange(events).RuleChange!;
        Assert.That(ruleChange.ChangeKind, Is.EqualTo(InventoryRuleConfigurationChangeKind.Removed));
        Assert.That(ruleChange.PreviousState!.Id, Is.EqualTo("only-apple"));
        Assert.That(ruleChange.PreviousState.Priority, Is.EqualTo(3));
        Assert.That(ruleChange.PreviousState.Enabled, Is.True);
        Assert.That(ruleChange.CurrentState, Is.Null);
    }

    [Test]
    public void TrySetRuleEnabled_ChangesEnabledStateAndEmitsConfigurationChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), enabled: false);
        var inventory = CreateInventory(rules, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRuleEnabled("only-apple", enabled: true, out var failure), Is.True);

        var ruleChange = SingleRuleChange(events).RuleChange!;
        Assert.That(ruleChange.ChangeKind, Is.EqualTo(InventoryRuleConfigurationChangeKind.EnabledChanged));
        Assert.That(ruleChange.PreviousState!.Enabled, Is.False);
        Assert.That(ruleChange.CurrentState!.Enabled, Is.True);
        Assert.That(ruleChange.CurrentState.Priority, Is.EqualTo(ruleChange.PreviousState.Priority));
    }

    [Test]
    public void TrySetRulePriority_ChangesPriorityAndEmitsConfigurationChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), priority: 1);
        var inventory = CreateInventory(rules, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRulePriority("only-apple", priority: 9, out var failure), Is.True);

        var ruleChange = SingleRuleChange(events).RuleChange!;
        Assert.That(ruleChange.ChangeKind, Is.EqualTo(InventoryRuleConfigurationChangeKind.PriorityChanged));
        Assert.That(ruleChange.PreviousState!.Priority, Is.EqualTo(1));
        Assert.That(ruleChange.CurrentState!.Priority, Is.EqualTo(9));
        Assert.That(ruleChange.CurrentState.Enabled, Is.EqualTo(ruleChange.PreviousState.Enabled));
    }

    [Test]
    public void RejectedRuleMutation_EmitsNoConfigurationChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(null, apple, berry);
        inventory.Add(berry);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRule("only-apple", new OnlyAllowItemsRule<string>(apple), out _), Is.False);

        Assert.That(events, Is.Empty);
    }

    [Test]
    public void RuleMutationNoOps_SucceedWithoutEmittingEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), priority: 4, enabled: true);
        var inventory = CreateInventory(rules, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRuleEnabled("only-apple", enabled: true, out var enabledError), Is.True, enabledError?.Message);
        Assert.That(inventory.TrySetRulePriority("only-apple", priority: 4, out var priorityError), Is.True, priorityError?.Message);

        Assert.That(events, Is.Empty);
    }

    [Test]
    public void RuleConfigurationChangedSnapshots_AreNotRewrittenByLaterMutations()
    {
        var apple = new ItemDefinition<string>("apple");
        var rules = new RuleContainer<string>();
        rules.Add("only-apple", new OnlyAllowItemsRule<string>(apple), priority: 1, enabled: false);
        var inventory = CreateInventory(rules, apple);
        var events = CaptureEvents(inventory);

        Assert.That(inventory.TrySetRuleEnabled("only-apple", enabled: true, out var firstError), Is.True, firstError?.Message);
        var firstChange = events.Single().ConfigurationChanged.Single().RuleChange!;
        events.Clear();

        Assert.That(inventory.TrySetRulePriority("only-apple", priority: 7, out var secondError), Is.True, secondError?.Message);

        Assert.That(firstChange.PreviousState!.Enabled, Is.False);
        Assert.That(firstChange.CurrentState!.Enabled, Is.True);
        Assert.That(firstChange.CurrentState.Priority, Is.EqualTo(1));
        Assert.That(events.Single().ConfigurationChanged.Single().RuleChange!.CurrentState!.Priority, Is.EqualTo(7));
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
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>(),
            rules
            );

        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        manager.Catalog.Freeze();
        return manager;
    }

    private static List<InventoryChangedEventArgs<string>> CaptureEvents(Inventory<string> inventory)
    {
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, args) => events.Add(args);
        return events;
    }

    private static InventoryConfigurationChanged<string> SingleRuleChange(
        IReadOnlyList<InventoryChangedEventArgs<string>> events)
    {
        Assert.That(events, Has.Count.EqualTo(1));
        var change = events.Single().ConfigurationChanged.Single();
        Assert.That(change.RuleChange, Is.Not.Null);
        return change;
    }
}
