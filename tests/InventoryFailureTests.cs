using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryFailureTests
{
    [Test]
    public void InventoryFailure_PreservesStructuredDataAndCause()
    {
        var cause = InventoryFailure.Create(
            InventoryFailureKind.Layout,
            InventoryFailureCodes.LayoutInvalidContext,
            "Slot already occupied.",
            component: "SlotLayout",
            source: "slot:0");

        var failure = InventoryFailure.Wrap(
            InventoryFailureKind.Transaction,
            InventoryFailureCodes.TransactionRejected,
            "Transaction placement failed.",
            cause,
            component: "Inventory",
            source: "commit");

        Assert.That(failure.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(failure.Code, Is.EqualTo(InventoryFailureCodes.TransactionRejected));
        Assert.That(failure.Component, Is.EqualTo("Inventory"));
        Assert.That(failure.Source, Is.EqualTo("commit"));
        Assert.That(failure.Cause, Is.SameAs(cause));
        Assert.That(failure.ToString(), Does.Contain("Transaction placement failed."));
        Assert.That(failure.ToString(), Does.Contain("Slot already occupied."));
    }

    [Test]
    public void TryAddFailure_ReturnsStructuredFailure()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new MaxTotalItemAmountCapacityPolicy<string>(0), apple);
        var inventory = manager.CreateInventory();

        var accepted = inventory.TryAdd(apple, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.Kind, Is.EqualTo(InventoryFailureKind.Capacity));
        Assert.That(failure.Code, Is.EqualTo(InventoryFailureCodes.CapacityRejected));
        Assert.That(failure.Message, Is.EqualTo("Capacity exceeded."));
    }

    [Test]
    public void ThrowingWrapper_ThrowsInventoryOperationExceptionWithFailure()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new MaxTotalItemAmountCapacityPolicy<string>(0), apple);
        var inventory = manager.CreateInventory();

        var exception = Assert.Throws<InventoryOperationException>(() => inventory.Add(apple));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Failure.Kind, Is.EqualTo(InventoryFailureKind.Capacity));
        Assert.That(exception.Failure.Code, Is.EqualTo(InventoryFailureCodes.CapacityRejected));
        Assert.That(exception.Message, Is.EqualTo(exception.Failure.Message));
    }

    [Test]
    public void RuleContainer_WrapsRuleFailureWithRuleIdentity()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var rules = new RuleContainer<string>();
        rules.Add("food-only", new OnlyAllowItemsRule<string>(apple));
        var manager = CreateManager(new UnlimitedCapacityPolicy<string>(), apple, berry, rules);
        var inventory = manager.CreateInventory();

        var accepted = inventory.TryAdd(berry, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.Kind, Is.EqualTo(InventoryFailureKind.Rules));
        Assert.That(failure.Code, Is.EqualTo(InventoryFailureCodes.RulesRejected));
        Assert.That(failure.Source, Is.EqualTo("food-only"));
        Assert.That(failure.Component, Is.EqualTo(nameof(OnlyAllowItemsRule<string>)));
        Assert.That(failure.Cause, Is.Not.Null);
    }

    [Test]
    public void DefinitionValidationException_PreservesFailure()
    {
        var failure = InventoryFailure.Create(
            InventoryFailureKind.Definition,
            InventoryFailureCodes.DefinitionInvalid,
            "Definition is missing a required attribute.");

        var exception = new DefinitionValidationException(failure);

        Assert.That(exception.Failure, Is.SameAs(failure));
        Assert.That(exception.Message, Is.EqualTo(failure.Message));
    }

    private static InventoryManager<string> CreateManager(
        ICapacityPolicy<string> capacityPolicy,
        ItemDefinition<string> definition,
        RuleContainer<string>? rules = null) =>
        CreateManager(capacityPolicy, new[] { definition }, rules);

    private static InventoryManager<string> CreateManager(
        ICapacityPolicy<string> capacityPolicy,
        ItemDefinition<string> firstDefinition,
        ItemDefinition<string> secondDefinition,
        RuleContainer<string>? rules = null) =>
        CreateManager(capacityPolicy, new[] { firstDefinition, secondDefinition }, rules);

    private static InventoryManager<string> CreateManager(
        ICapacityPolicy<string> capacityPolicy,
        ItemDefinition<string>[] definitions,
        RuleContainer<string>? rules)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            capacityPolicy,
            new EntryLayout<string>(),
            new ItemCatalog<string>(),
            rules);

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }
}
