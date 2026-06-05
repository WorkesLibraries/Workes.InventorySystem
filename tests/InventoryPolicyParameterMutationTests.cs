using System;
using System.Collections.Generic;
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
    private const string FootprintWidth = "policy-mutation-footprint-width";
    private const string FootprintHeight = "policy-mutation-footprint-height";

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

    private sealed class FootprintDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> FootprintSchema =
            ItemSchema<string>.Create("policy-mutation-footprint")
                .RequireAttribute<int>(FootprintWidth, inherited: true)
                .RequireAttribute<int>(FootprintHeight, inherited: true);

        public FootprintDefinition(string id, int width, int height)
            : base(id, FootprintSchema)
        {
            DefineAttribute(FootprintWidth, width);
            DefineAttribute(FootprintHeight, height);
        }
    }

    [Test]
    public void InventoryParameterMutationOptions_BooleanPropertiesMapToActions()
    {
        var options = new InventoryParameterMutationOptions();

        options.RepackLayout = true;
        options.SplitOversizedStacks = true;
        options.CompressCompatibleStacks = true;

        Assert.That(options.Actions, Is.EqualTo(
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.CompressCompatibleStacks));

        options.SplitOversizedStacks = false;

        Assert.That(options.Actions, Is.EqualTo(
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.CompressCompatibleStacks));
    }

    [Test]
    public void InventoryParameterMutationOptions_CompressStacksAliasesSplitOversizedStacks()
    {
        var options = new InventoryParameterMutationOptions { CompressStacks = true };

        Assert.That(options.SplitOversizedStacks, Is.True);
        Assert.That(options.Actions.HasFlag(InventoryParameterMutationActions.SplitOversizedStacks), Is.True);

        options.CompressStacks = false;

        Assert.That(options.SplitOversizedStacks, Is.False);
    }

    [Test]
    public void InventoryParameterMutationOptions_CompactCompatibleStacksAliasesCompressCompatibleStacks()
    {
        var options = new InventoryParameterMutationOptions { CompactCompatibleStacks = true };

        Assert.That(options.CompressCompatibleStacks, Is.True);
        Assert.That(options.Actions.HasFlag(InventoryParameterMutationActions.CompressCompatibleStacks), Is.True);

        options.CompactCompatibleStacks = false;

        Assert.That(options.CompressCompatibleStacks, Is.False);
    }

    [Test]
    public void InventoryParameterMutationOptions_PresetsUseExpectedActions()
    {
        Assert.That(InventoryParameterMutationOptions.PreserveOnly.Actions, Is.EqualTo(InventoryParameterMutationActions.None));
        Assert.That(InventoryParameterMutationOptions.RepackOnly.Actions, Is.EqualTo(InventoryParameterMutationActions.RepackLayout));
        Assert.That(InventoryParameterMutationOptions.RepackAndCompress.Actions, Is.EqualTo(
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks));
        Assert.That(InventoryParameterMutationOptions.RepackAndCompact.Actions, Is.EqualTo(
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.CompressCompatibleStacks));
        Assert.That(InventoryParameterMutationOptions.RepackCompressAndCompact.Actions, Is.EqualTo(
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.CompressCompatibleStacks));
    }

    [Test]
    public void TrySetStackResolverParameter_IncreasesMaxStackAndAllowsFutureMerge()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(5), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        inventory.Add(coin, amount: 5);

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 10, out var error), Is.True, error);
        inventory.Add(coin, amount: 5);

        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(10));
        Assert.That(((FixedSizeStackResolver<string>)inventory.StackResolver).MaxStack, Is.EqualTo(10));
    }

    [Test]
    public void TrySetStackResolverParameter_IncreaseDoesNotCompactByDefault()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, args) => events.Add(args);

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3, 3 }));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events.Single().RequiresFullRefresh, Is.False);
    }

    [Test]
    public void TrySetStackResolverParameter_CompactsCompatibleStacksWhenEnabled()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, args) => events.Add(args);

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompact, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 5, 4 }));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events.Single().RequiresFullRefresh, Is.True);
        Assert.That(events.Single().ConfigurationChanged.Single().Kind, Is.EqualTo(InventoryConfigurationChangeKind.StackResolver));
    }

    [Test]
    public void TrySetStackResolverParameter_CompressesCompatibleStacksWithoutRepack()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), coin);
        inventory.Add(coin, amount: 40);
        int events = 0;
        inventory.Changed += (_, _) => events++;
        var options = new InventoryParameterMutationOptions { CompressCompatibleStacks = true };

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 25, options, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 25, 15 }));
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void TrySetStackResolverParameter_CompressionPreservesInterleavedDifferentDefinitions()
    {
        var coin = new ItemDefinition<string>("coin");
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(5), coin, gem);
        inventory.Add(coin, amount: 10);
        inventory.Add(gem, amount: 3);
        inventory.Add(coin, amount: 30);

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            25,
            new InventoryParameterMutationOptions { CompressCompatibleStacks = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "coin", "gem", "coin" }));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 25, 3, 15 }));
    }

    [Test]
    public void TrySetStackResolverParameter_RepackAndCompressDoesNotCompactOnUpgrade()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompress, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3, 3 }));
    }

    [Test]
    public void TrySetStackResolverParameter_SplitsAndCompactsWhenBothModesEnabled()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(5), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 3, InventoryParameterMutationOptions.RepackCompressAndCompact, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3, 3 }));
    }

    [Test]
    public void TrySetStackResolverParameter_CompactionDoesNotMergeDifferentMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        var fresh = new InstanceMetadata();
        fresh.Set("quality", "fresh");
        var bruised = new InstanceMetadata();
        bruised.Set("quality", "bruised");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, 3, null, fresh, out var error), Is.True, error);
        Assert.That(builder.TryAdd(apple, 3, null, bruised, out error), Is.True, error);
        inventory.CommitTransaction(builder.Build());

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            6,
            new InventoryParameterMutationOptions { CompressCompatibleStacks = true },
            out error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3 }));
        Assert.That(inventory.Items.Select(item =>
        {
            item.Metadata.TryGet<string>("quality", out var quality);
            return quality;
        }), Is.EquivalentTo(new[] { "fresh", "bruised" }));
    }

    [Test]
    public void TrySetStackResolverParameter_CompactionPreservesMergedMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        var left = new InstanceMetadata();
        left.Set("quality", "fresh");
        var right = new InstanceMetadata();
        right.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, 3, null, left, out var error), Is.True, error);
        Assert.That(builder.TryAdd(apple, 3, null, right, out error), Is.True, error);
        inventory.CommitTransaction(builder.Build());

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            6,
            new InventoryParameterMutationOptions { CompressCompatibleStacks = true },
            out error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(inventory.Items.Single().Amount, Is.EqualTo(6));
        Assert.That(inventory.Items.Single().Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void TrySetStackResolverParameter_CompactionUsesNormalRepackPlacement()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompact, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(5));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(4));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2)), Is.Null);
    }

    [Test]
    public void StackCompactionParameterChange_FiresSingleFullRefreshConfigurationEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, args) => events.Add(args);

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompact, out var error), Is.True, error);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events.Single().RequiresFullRefresh, Is.True);
        Assert.That(events.Single().ConfigurationChanged, Has.Count.EqualTo(1));
        Assert.That(events.Single().ConfigurationChanged.Single().Kind, Is.EqualTo(InventoryConfigurationChangeKind.StackResolver));
    }

    [Test]
    public void CompressWithoutRepack_FiresSingleConfigurationEventWithoutFullRefresh()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(3), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), apple);
        inventory.Add(apple, amount: 9);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            5,
            new InventoryParameterMutationOptions { CompressCompatibleStacks = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void TrySetStackResolverParameter_RejectsLoweringBelowExistingStackAmount()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        inventory.Add(coin, amount: 5);
        events = 0;

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 3, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("exceed max stack size"));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(((FixedSizeStackResolver<string>)inventory.StackResolver).MaxStack, Is.EqualTo(10));
    }

    [Test]
    public void TrySetStackResolverParameter_SplitsOversizedStacksWithCompressionAndRepack()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "mint");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(coin, 10, null, metadata, out var buildError), Is.True, buildError);
        inventory.CommitTransaction(builder.Build());
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompress, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items, Has.Count.EqualTo(2));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EquivalentTo(new[] { 5, 5 }));
        Assert.That(inventory.Items.All(item => item.Metadata.TryGet<string>("quality", out var quality) && quality == "mint"), Is.True);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.ConfigurationChanged.Single().Kind, Is.EqualTo(InventoryConfigurationChangeKind.StackResolver));
        Assert.That(captured.RequiresFullRefresh, Is.True);
    }

    [Test]
    public void TrySetStackResolverParameter_SplitsOversizedStacksWithoutRepack()
    {
        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(4), coin, potion);
        inventory.Add(coin, amount: 10);
        inventory.Add(potion, amount: 2);
        var options = new InventoryParameterMutationOptions { SplitOversizedStacks = true };

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 4, options, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Definition.Id), Is.EqualTo(new[] { "coin", "potion", "coin", "coin" }));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 4, 2, 4, 2 }));
    }

    [Test]
    public void TrySetStackResolverParameter_SplitWithoutRepackRejectsWhenLayoutCannotPlaceChunks()
    {
        var coin = new ItemDefinition<string>("coin");
        var potion = new ItemDefinition<string>("potion");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin, potion);
        inventory.Add(coin, amount: 10);
        inventory.Add(potion, amount: 2);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetStackResolverParameter(
            "maxStack",
            4,
            new InventoryParameterMutationOptions { SplitOversizedStacks = true },
            out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("Not enough empty slots"));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 10, 2 }));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TrySetStackResolverParameter_RejectsCompressionWhenLayoutCannotFitSplitStacks()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(1), coin);
        int events = 0;
        inventory.Add(coin, amount: 10);
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompress, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("Not enough empty slots"));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(10));
    }

    [Test]
    public void SetStackResolverParameter_ThrowsWhenRejected()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);
        inventory.Add(coin, amount: 5);

        Assert.Throws<InvalidOperationException>(() => inventory.SetStackResolverParameter("maxStack", 3));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_RejectsLoweringBelowCurrentTotal()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(20), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);

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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(20), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);

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
            new FixedSizeStackResolver<string>(10),
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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);

        Assert.That(inventory.TrySetLayoutParameter("slotCount", 3, out var error), Is.True, error);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(3));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(2))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsShrinkingSlotLayoutWhenRemovedSlotOccupied()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
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
    public void TrySetLayoutParameter_ReflowsSlotLayoutWhenRepackEnabled()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(2));
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.TrySetLayoutParameter(
            "slotCount",
            2,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(coin));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.ConfigurationChanged.Single().Kind, Is.EqualTo(InventoryConfigurationChangeKind.Layout));
        Assert.That(captured.RequiresFullRefresh, Is.True);
    }

    [Test]
    public void TrySetLayoutParameter_RejectsRepackWhenShrunkSlotLayoutCannotFitAllItems()
    {
        var coin = new ItemDefinition<string>("coin");
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(1), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin, gem);
        int events = 0;
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(0));
        inventory.Add(gem, context: SlotLayoutContext<string>.Single(1));
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.TrySetLayoutParameter(
            "slotCount",
            1,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("Not enough empty slots"));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
    }

    [Test]
    public void TrySetLayoutParameter_ShrinksSlotLayoutWhenRemovedSlotsAreEmpty()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(3), coin);
        inventory.Add(coin, context: SlotLayoutContext<string>.Single(0));

        Assert.That(inventory.TrySetLayoutParameter("slotCount", 2, out var error), Is.True, error);

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsGridShrinkWhenOccupiedCellWouldBeOutsideBounds()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new GridLayout<string>(3, 1), coin);
        inventory.Add(coin, context: GridLayoutContext<string>.Single(2, 0));

        var accepted = inventory.TrySetLayoutParameter("width", 2, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("outside the new bounds"));
        Assert.That(((GridLayout<string>)inventory.Layout).Width, Is.EqualTo(3));
    }

    [Test]
    public void TrySetLayoutParameter_ReflowsGridLayoutWhenRepackEnabled()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new GridLayout<string>(3, 1), coin);
        inventory.Add(coin, context: GridLayoutContext<string>.Single(2, 0));

        var accepted = inventory.TrySetLayoutParameter(
            "width",
            2,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((GridLayout<string>)inventory.Layout).Width, Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(0, 0))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void TrySetLayoutParameter_ReflowsMultiCellGridLayoutFootprintsWhenRepackEnabled()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new MultiCellGridLayout<string>(3, 1, new AttributeGridFootprintProvider<string>(FootprintWidth, FootprintHeight)),
            table);
        inventory.Add(table, context: MultiCellGridLayoutContext<string>.Single(1, 0));

        var accepted = inventory.TrySetLayoutParameter(
            "width",
            2,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((MultiCellGridLayout<string>)inventory.Layout).Width, Is.EqualTo(2));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(0, 0))!.Definition, Is.SameAs(table));
        Assert.That(inventory.Layout.GetItemAt(inventory, MultiCellGridLayoutContext<string>.Single(1, 0))!.Definition, Is.SameAs(table));
    }

    [Test]
    public void TrySetLayoutParameter_RejectsMultiCellRepackWhenFootprintsCannotFit()
    {
        var table = new FootprintDefinition("table", 2, 1);
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new MultiCellGridLayout<string>(3, 1, new AttributeGridFootprintProvider<string>(FootprintWidth, FootprintHeight)),
            table);
        inventory.Add(table, context: MultiCellGridLayoutContext<string>.Single(1, 0));

        var accepted = inventory.TrySetLayoutParameter(
            "width",
            1,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("Not enough empty grid space"));
        Assert.That(((MultiCellGridLayout<string>)inventory.Layout).Width, Is.EqualTo(3));
    }

    [Test]
    public void TrySetLayoutParameter_ChangesGridPlacementOrderWithoutMovingExistingItems()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new GridLayout<string>(2, 2), coin);
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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), layout, coin);

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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), layout, coin);
        inventory.Add(coin, context: SectionedLayoutContext<string>.Single("bag", 1));

        var accepted = inventory.TrySetLayoutParameter("section:bag.slotCount", 1, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("removed slot is occupied"));
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(2));
    }

    [Test]
    public void TrySetLayoutParameter_ReflowsSectionedLayoutIntoCompatibleSlots()
    {
        var coin = new ItemDefinition<string>("coin");
        var layout = new SectionedLayout<string>(new SectionDefinition<string>("bag", 2));
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), layout, coin);
        inventory.Add(coin, context: SectionedLayoutContext<string>.Single("bag", 1));

        var accepted = inventory.TrySetLayoutParameter(
            "section:bag.slotCount",
            1,
            new InventoryParameterMutationOptions { RepackLayout = true },
            out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(1));
        Assert.That(inventory.Layout.GetItemAt(inventory, SectionedLayoutContext<string>.Single("bag", 0))!.Definition, Is.SameAs(coin));
    }

    [Test]
    public void PolicyParameterChange_FiresConfigurationChangedEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new MaxTotalItemAmountCapacityPolicy<string>(10), new EntryLayout<string>(), coin);
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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);
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
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), coin);
        inventory.Add(coin, amount: 5);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 3, out _), Is.False);

        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void RejectedRepackOrCompression_FiresNoChangedEvent()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(1), coin);
        inventory.Add(coin, amount: 10);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TrySetStackResolverParameter("maxStack", 5, InventoryParameterMutationOptions.RepackAndCompress, out _), Is.False);

        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void TrySetLayoutParameter_ReturnsFalseForUnsupportedLayout()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetLayoutParameter("anything", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Current layout does not support runtime parameters."));
    }

    [Test]
    public void TrySetCapacityPolicyParameter_ReturnsFalseForUnsupportedPolicy()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetCapacityPolicyParameter("anything", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Current capacity policy does not support runtime parameters."));
    }

    [Test]
    public void TrySetStackResolverParameter_ReturnsFalseForUnknownParameter()
    {
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), coin);

        Assert.That(inventory.TrySetStackResolverParameter("unknown", 1, out var error), Is.False);
        Assert.That(error, Is.EqualTo("Parameter 'unknown' is not supported by FixedSizeStackResolver."));
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
        if (definitions.Any(definition => definition is FootprintDefinition))
        {
            manager.Catalog.Attributes.Define<int>(FootprintWidth);
            manager.Catalog.Attributes.Define<int>(FootprintHeight);
        }

        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }
}
