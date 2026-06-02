using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryPolicyParameterMutationTests
{
    private const string Weight = "weight";

    private sealed class WeightedDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> WeightedSchema =
            ItemSchema<string>.Create("policy-mutation-weighted")
                .RequireAttribute<double>(Weight, inherited: true);

        public WeightedDefinition(string id, double weight)
            : base(id, WeightedSchema)
        {
            DefineAttribute(Weight, weight);
        }
    }

    [Test]
    public void TrySetStackResolverParameter_IncreasesMaxStackAndAllowsFutureMerge()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(5), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        inventory.Add(coin, amount: 5);

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 10, out var error), Is.True, error);
        inventory.Add(coin, amount: 5);

        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(10));
        Assert.That(((DefaultStackResolver<string>)inventory.StackResolver).DefaultMaxStack, Is.EqualTo(10));
    }

    [Test]
    public void TrySetStackResolverParameter_RejectsLoweringBelowExistingStackAmount()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        inventory.Add(coin, amount: 5);
        events = 0;

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 3, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("exceed max stack size"));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(((DefaultStackResolver<string>)inventory.StackResolver).DefaultMaxStack, Is.EqualTo(10));
    }

    [Test]
    public void SetStackResolverParameter_ThrowsWhenRejected()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);
        inventory.Add(coin, amount: 5);

        Assert.Throws<InvalidOperationException>(() => inventory.SetStackResolverParameter("maxStack", 3));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_RejectsLoweringBelowCurrentTotal()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(20), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);

        inventory.Add(coin, amount: 8);

        var accepted = inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 5, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("current inventory contents invalid"));
        Assert.That(((MaxTotalItemAmountCapacityPolicy<string>)inventory.CapacityPolicy).MaxTotalItemAmount, Is.EqualTo(10));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_AppliesLowerCapacityAfterContentsFit()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(20), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);

        inventory.Add(coin, amount: 8);
        inventory.RemoveByDefinition(coin, amount: 4, ignoreMetadata: true);

        Assert.That(inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 5, out var error), Is.True, error);
        Assert.That(((MaxTotalItemAmountCapacityPolicy<string>)inventory.CapacityPolicy).MaxTotalItemAmount, Is.EqualTo(5));
        Assert.That(inventory.TryAdd(coin, out var addError, amount: 2), Is.False);
        Assert.That(addError, Is.EqualTo("Capacity exceeded."));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_UpdatesWeightLimit()
    {
        var sword = new WeightedDefinition("sword", 4);
        var inventory = CreateInventory(
            new DefaultStackResolver<string>(10),
            new WeightCapacityPolicy<string>(Weight, maxWeight: 10),
            new EntryLayout<string>(),
            sword);

        inventory.Add(sword, amount: 2);

        Assert.That(inventory.TrySetCapacityPolicyParameter("maxWeight", 7d, out var rejectedError), Is.False);
        Assert.That(rejectedError, Does.Contain("current inventory contents invalid"));
        Assert.That(inventory.TrySetCapacityPolicyParameter("maxWeight", 8d, out var acceptedError), Is.True, acceptedError);
        Assert.That(((WeightCapacityPolicy<string>)inventory.CapacityPolicy).MaxWeight, Is.EqualTo(8d));
    }

    [Test]
    public void TrySetLayoutParameter_GrowsSlotLayoutAndAllowsPlacementInNewSlot()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);

        Assert.That(inventory.TrySetLayoutParameter("slotCount", 3, out var error), Is.True, error);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(3));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsShrinkingSlotLayoutWhenRemovedSlotOccupied()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
        int events = 0;
        inventory.Changed += (_, _) => events++;
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));
        events = 0;

        var accepted = inventory.TrySetLayoutParameter("slotCount", 2, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("removed slot is occupied"));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(3));
    }

    [Test]
    public void TrySetLayoutParameter_ShrinksSlotLayoutWhenRemovedSlotsAreEmpty()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(0));

        Assert.That(inventory.TrySetLayoutParameter("slotCount", 2, out var error), Is.True, error);

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsGridShrinkWhenOccupiedCellWouldBeOutsideBounds()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new GridLayout<string>(3, 1), coin);
        inventory.Add(coin, context: GridLayoutContext<string>.Single(2, 0));

        var accepted = inventory.TrySetLayoutParameter("width", 2, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("outside the new bounds"));
        Assert.That(((GridLayout<string>)inventory.Layout).Width, Is.EqualTo(3));
    }

    [Test]
    public void TrySetLayoutParameter_ChangesGridPlacementOrderWithoutMovingExistingItems()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new GridLayout<string>(2, 2), coin);
        inventory.Add(coin, context: GridLayoutContext<string>.Single(1, 0));

        Assert.That(inventory.TrySetLayoutParameter("placementOrder", GridPlacementOrder.ColumnMajor, out var error), Is.True, error);

        Assert.That(((GridLayout<string>)inventory.Layout).PlacementOrder, Is.EqualTo(GridPlacementOrder.ColumnMajor));
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(1, 0))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_UpdatesSectionSlotCount()
    {
        var coin = new ItemDefinition<string>("coin");
        var layout = new SectionedLayout<string>(new SectionDefinition<string>("bag", 1));
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), layout, coin);

        Assert.That(inventory.TrySetLayoutParameter("section:bag.slotCount", 2, out var error), Is.True, error);
        inventory.Add(coin, context: SectionedLayoutContext<string>.Single("bag", 1));

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, SectionedLayoutContext<string>.Single("bag", 1))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsShrinkingSectionWithOccupiedRemovedSlot()
    {
        var coin = new ItemDefinition<string>("coin");
        var layout = new SectionedLayout<string>(new SectionDefinition<string>("bag", 2));
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), layout, coin);
        inventory.Add(coin, context: SectionedLayoutContext<string>.Single("bag", 1));

        var accepted = inventory.TrySetLayoutParameter("section:bag.slotCount", 1, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("removed slot is occupied"));
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
    }

    [Test]
    public void PolicyParameterChange_FiresConfigurationChangedEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        Assert.That(inventory.TrySetCapacityPolicyParameter("maxTotalItemAmount", 12, out var error), Is.True, error);

        var change = captured!.ConfigurationChanged.Single();
        Assert.That(change.Kind, Is.EqualTo(InventoryConfigurationChangeKind.CapacityPolicy));
        Assert.That(change.ParameterId, Is.EqualTo("maxTotalItemAmount"));
        Assert.That(captured.RequiresFullRefresh, Is.False);
    }

    [Test]
    public void LayoutParameterChange_FiresFullRefreshConfigurationChangedEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        Assert.That(inventory.TrySetLayoutParameter("slotCount", 3, out var error), Is.True, error);

        var change = captured!.ConfigurationChanged.Single();
        Assert.That(change.Kind, Is.EqualTo(InventoryConfigurationChangeKind.Layout));
        Assert.That(change.RequiresFullRefresh, Is.True);
        Assert.That(captured.RequiresFullRefresh, Is.True);
    }

    [Test]
    public void RejectedParameterChange_FiresNoChangedEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);
        inventory.Add(coin, amount: 5);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 3, out _), Is.False);

        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TrySetLayoutParameter_ReturnsFalseForUnsupportedLayout()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetLayoutParameter("anything", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Current layout does not support runtime parameters."));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_ReturnsFalseForUnsupportedPolicy()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetCapacityPolicyParameter("anything", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Current capacity policy does not support runtime parameters."));
    }

    [Test]
    public void TrySetStackResolverParameter_ReturnsFalseForUnknownParameter()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new DefaultStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetStackResolverParameter("unknown", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Parameter 'unknown' is not supported by DefaultStackResolver."));
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        ICapacityPolicy<string> capacityPolicy,
        IInventoryLayout<string> layout,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(stackResolver, capacityPolicy, layout);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        if (definitions.Any(definition => definition is WeightedDefinition))
            manager.Catalog.Attributes.Define<double>(Weight);

        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }
}
