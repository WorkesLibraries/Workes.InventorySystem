using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class AttributeDrivenStackResolverTests
{
    private const string Stackable = "stackable";
    private const string MaxStack = "maxStack";
    private const string StackRatio = "stackRatio";

    private sealed class StackConfiguredDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StackConfiguredSchema =
            ItemSchema<string>.Create("stack-configured")
                .RequireAttribute<bool>(Stackable, inherited: true)
                .RequireAttribute<int>(MaxStack, inherited: true);

        public StackConfiguredDefinition(string id, bool stackable, int maxStack)
            : base(id, StackConfiguredSchema)
        {
            DefineAttribute(Stackable, stackable);
            DefineAttribute(MaxStack, maxStack);
        }
    }

    private sealed class StackableOnlyDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StackableOnlySchema =
            ItemSchema<string>.Create("stackable-only")
                .RequireAttribute<bool>(Stackable, inherited: true);

        public StackableOnlyDefinition(string id, bool stackable)
            : base(id, StackableOnlySchema)
        {
            DefineAttribute(Stackable, stackable);
        }
    }

    private sealed class MaxStackOnlyDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> MaxStackOnlySchema =
            ItemSchema<string>.Create("max-stack-only")
                .RequireAttribute<int>(MaxStack, inherited: true);

        public MaxStackOnlyDefinition(string id, int maxStack)
            : base(id, MaxStackOnlySchema)
        {
            DefineAttribute(MaxStack, maxStack);
        }
    }

    private sealed class StackRatioDefinition : ItemDefinition<string>
    {
        public static readonly ItemSchema<string> StackRatioSchema =
            ItemSchema<string>.CreateFor<StackRatioDefinition>("stack-ratio")
                .RequireAttribute<int>(StackRatio, inherited: true);

        public StackRatioDefinition(string id, int stackRatio)
            : base(id, StackRatioSchema)
        {
            DefineAttribute(StackRatio, stackRatio);
        }
    }

    [Test]
    public void ConditionalMaxStackResolver_ReturnsMaxStack_WhenAttributeTrue()
    {
        var coin = new StackableOnlyDefinition("coin", stackable: true);
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin));

        Assert.That(maxStack, Is.EqualTo(10));
    }

    [Test]
    public void ConditionalMaxStackResolver_ReturnsOne_WhenAttributeFalse()
    {
        var sword = new StackableOnlyDefinition("sword", stackable: false);
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), sword);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(sword));

        Assert.That(maxStack, Is.EqualTo(1));
    }

    [Test]
    public void ConditionalMaxStackResolver_ReturnsOne_WhenAttributeMissingByDefault()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note));

        Assert.That(maxStack, Is.EqualTo(1));
    }

    [Test]
    public void ConditionalMaxStackResolver_CanTreatMissingAttributeAsStackable()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10, missingAttributeIsStackable: true), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note));

        Assert.That(maxStack, Is.EqualTo(10));
    }

    [Test]
    public void ConditionalMaxStackResolver_RejectsInvalidConstructorArguments()
    {
        Assert.Throws<ArgumentException>(() => new ConditionalMaxStackResolver<string>("", 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConditionalMaxStackResolver<string>(Stackable, 0));
    }

    [Test]
    public void ConditionalMaxStackResolver_TryCreateWithParameter_UpdatesMaxStack()
    {
        var resolver = new ConditionalMaxStackResolver<string>(Stackable, 10);

        var accepted = resolver.TryCreateWithParameter(null!, "maxStack", 20, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((ConditionalMaxStackResolver<string>)replacement!).MaxStack, Is.EqualTo(20));
    }

    [Test]
    public void ConditionalMaxStackResolver_TryCreateWithParameter_UpdatesMissingAttributeBehavior()
    {
        var resolver = new ConditionalMaxStackResolver<string>(Stackable, 10);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeIsStackable", true, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((ConditionalMaxStackResolver<string>)replacement!).MissingAttributeIsStackable, Is.True);
    }

    [Test]
    public void AttributeMaxStackResolver_UsesDefinitionAttribute()
    {
        var coin = new MaxStackOnlyDefinition("coin", 25);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin));

        Assert.That(maxStack, Is.EqualTo(25));
    }

    [Test]
    public void AttributeMaxStackResolver_UsesConfiguredFallback_WhenAttributeMissing()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack, missingAttributeMaxStack: 3), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note));

        Assert.That(maxStack, Is.EqualTo(3));
    }

    [Test]
    public void AttributeMaxStackResolver_Throws_WhenAttributeMissingAndFallbackIsNull()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note)));
    }

    [Test]
    public void AttributeMaxStackResolver_Throws_WhenAttributeValueIsInvalid()
    {
        var broken = new MaxStackOnlyDefinition("broken", 0);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), broken);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(broken)));
    }

    [Test]
    public void AttributeMaxStackResolver_RejectsInvalidConstructorArguments()
    {
        Assert.Throws<ArgumentException>(() => new AttributeMaxStackResolver<string>(""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttributeMaxStackResolver<string>(MaxStack, 0));
    }

    [Test]
    public void AttributeMaxStackResolver_TryCreateWithParameter_UpdatesFallback()
    {
        var resolver = new AttributeMaxStackResolver<string>(MaxStack);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeMaxStack", 4, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((AttributeMaxStackResolver<string>)replacement!).MissingAttributeMaxStack, Is.EqualTo(4));
    }

    [Test]
    public void AttributeMaxStackResolver_TryCreateWithParameter_AllowsStrictNullFallback()
    {
        var resolver = new AttributeMaxStackResolver<string>(MaxStack, 4);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeMaxStack", null, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((AttributeMaxStackResolver<string>)replacement!).MissingAttributeMaxStack, Is.Null);
    }

    [Test]
    public void TryAdd_ReturnsFalse_WhenStrictAttributeMaxResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        var accepted = inventory.TryAdd(note, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("missing stack-size attribute"));
    }

    [Test]
    public void Add_Throws_WhenStrictAttributeMaxResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        Assert.Throws<InventoryOperationException>(() => inventory.Add(note));
    }

    [Test]
    public void TryAdd_SplitsByAttributeDefinedMaxStack()
    {
        var coin = new MaxStackOnlyDefinition("coin", 3);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), coin);

        var accepted = inventory.TryAdd(coin, out var failure, amount: 8);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3, 2 }));
    }

    [Test]
    public void TrySetStackResolverParameter_ReturnsFalse_WhenStrictFallbackInvalidatesCurrentContents()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack, missingAttributeMaxStack: 10), note);
        inventory.Add(note, amount: 5);

        var accepted = inventory.TrySetStackResolverParameter("missingAttributeMaxStack", null, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("missing stack-size attribute"));
        Assert.That(((AttributeMaxStackResolver<string>)inventory.StackResolver).MissingAttributeMaxStack, Is.EqualTo(10));
    }

    [Test]
    public void MultipliedAttributeStackResolver_UsesDefinitionAttributeAndMultiplier()
    {
        var coin = new StackRatioDefinition("coin", 4);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 2.5), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin));

        Assert.That(maxStack, Is.EqualTo(10));
    }

    [Test]
    public void MultipliedAttributeStackResolver_FloorsFractionalResult()
    {
        var coin = new StackRatioDefinition("coin", 3);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 1.5), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin));

        Assert.That(maxStack, Is.EqualTo(4));
    }

    [Test]
    public void MultipliedAttributeStackResolver_ClampsPositiveFractionalResultToOne()
    {
        var coin = new StackRatioDefinition("coin", 1);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 0.5), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin));

        Assert.That(maxStack, Is.EqualTo(1));
    }

    [Test]
    public void MultipliedAttributeStackResolver_UsesConfiguredFallback_WhenAttributeMissing()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 3, missingAttributeBaseStack: 2), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note));

        Assert.That(maxStack, Is.EqualTo(6));
    }

    [Test]
    public void MultipliedAttributeStackResolver_Throws_WhenAttributeMissingAndFallbackIsNull()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 3), note);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(note)));
    }

    [Test]
    public void MultipliedAttributeStackResolver_Throws_WhenAttributeValueIsInvalid()
    {
        var broken = new StackRatioDefinition("broken", 0);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 3), broken);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(broken)));
    }

    [Test]
    public void MultipliedAttributeStackResolver_Throws_WhenComputedStackExceedsIntMaxValue()
    {
        var coin = new StackRatioDefinition("coin", int.MaxValue);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 2), coin);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, CreateProbeInstance(coin)));
    }

    [Test]
    public void MultipliedAttributeStackResolver_RejectsInvalidConstructorArguments()
    {
        Assert.Throws<ArgumentException>(() => new MultipliedAttributeStackResolver<string>("", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultipliedAttributeStackResolver<string>(StackRatio, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultipliedAttributeStackResolver<string>(StackRatio, -1));
        Assert.Throws<ArgumentException>(() => new MultipliedAttributeStackResolver<string>(StackRatio, double.NaN));
        Assert.Throws<ArgumentException>(() => new MultipliedAttributeStackResolver<string>(StackRatio, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultipliedAttributeStackResolver<string>(StackRatio, 1, 0));
    }

    [Test]
    public void MultipliedAttributeStackResolver_TryCreateWithParameter_UpdatesMultiplier()
    {
        var resolver = new MultipliedAttributeStackResolver<string>(StackRatio, 1, missingAttributeBaseStack: 2);

        var accepted = resolver.TryCreateWithParameter(null!, "multiplier", 2.5, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        var multiplied = (MultipliedAttributeStackResolver<string>)replacement!;
        Assert.That(multiplied.Multiplier, Is.EqualTo(2.5));
        Assert.That(multiplied.MissingAttributeBaseStack, Is.EqualTo(2));
    }

    [Test]
    public void MultipliedAttributeStackResolver_TryCreateWithParameter_UpdatesFallback()
    {
        var resolver = new MultipliedAttributeStackResolver<string>(StackRatio, 2);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeBaseStack", 4, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((MultipliedAttributeStackResolver<string>)replacement!).MissingAttributeBaseStack, Is.EqualTo(4));
    }

    [Test]
    public void MultipliedAttributeStackResolver_TryCreateWithParameter_AllowsStrictNullFallback()
    {
        var resolver = new MultipliedAttributeStackResolver<string>(StackRatio, 2, missingAttributeBaseStack: 4);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeBaseStack", null, out var replacement, out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((MultipliedAttributeStackResolver<string>)replacement!).MissingAttributeBaseStack, Is.Null);
    }

    [Test]
    public void MultipliedAttributeStackResolver_TryCreateWithParameter_RejectsUnknownParameter()
    {
        var resolver = new MultipliedAttributeStackResolver<string>(StackRatio, 2);

        var accepted = resolver.TryCreateWithParameter(null!, "unknown", 1, out var replacement, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(replacement, Is.Null);
        Assert.That(failure?.Message, Does.Contain("not supported"));
    }

    [Test]
    public void MultipliedAttributeStackResolver_TryCreateWithParameter_RejectsInvalidMultiplier()
    {
        var resolver = new MultipliedAttributeStackResolver<string>(StackRatio, 2);

        Assert.That(resolver.TryCreateWithParameter(null!, "multiplier", 0.0, out _, out var zeroError), Is.False);
        Assert.That(zeroError?.Message, Does.Contain("greater than zero"));
        Assert.That(resolver.TryCreateWithParameter(null!, "multiplier", double.NaN, out _, out var finiteError), Is.False);
        Assert.That(finiteError?.Message, Does.Contain("finite"));
        Assert.That(resolver.TryCreateWithParameter(null!, "multiplier", 2, out _, out var typeError), Is.False);
        Assert.That(typeError?.Message, Does.Contain("Double"));
    }

    [Test]
    public void TryAdd_SplitsByMultipliedAttributeMaxStack()
    {
        var coin = new StackRatioDefinition("coin", 2);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 3), coin);

        var accepted = inventory.TryAdd(coin, out var failure, amount: 14);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 6, 6, 2 }));
    }

    [Test]
    public void TryAdd_ReturnsFalse_WhenStrictMultipliedResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 2), note);

        var accepted = inventory.TryAdd(note, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("missing stack-ratio attribute"));
    }

    [Test]
    public void Add_Throws_WhenStrictMultipliedResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 2), note);

        Assert.Throws<InventoryOperationException>(() => inventory.Add(note));
    }

    [Test]
    public void TrySetStackResolverParameter_RevalidatesCurrentContentsWhenMultiplierChanges()
    {
        var coin = new StackRatioDefinition("coin", 10);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 1), coin);
        inventory.Add(coin, amount: 10);

        var accepted = inventory.TrySetStackResolverParameter("multiplier", 0.5, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Message, Does.Contain("exceed max stack size"));
        Assert.That(((MultipliedAttributeStackResolver<string>)inventory.StackResolver).Multiplier, Is.EqualTo(1));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 10 }));
    }

    [Test]
    public void TrySetStackResolverParameter_WithSplit_SplitsEntryLayoutStacksWhenMultiplierShrinks()
    {
        var coin = new StackRatioDefinition("coin", 10);
        var inventory = CreateInventory(new MultipliedAttributeStackResolver<string>(StackRatio, 1), coin);
        inventory.Add(coin, amount: 10);

        var accepted = inventory.TrySetStackResolverParameter(
            "multiplier",
            0.5,
            InventoryParameterMutationActions.SplitOversizedStacks,
            out var failure);

        Assert.That(accepted, Is.True);
        Assert.That(((MultipliedAttributeStackResolver<string>)inventory.StackResolver).Multiplier, Is.EqualTo(0.5));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 5, 5 }));
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            stackResolver,
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        manager.Catalog.Attributes.Define<bool>(Stackable);
        manager.Catalog.Attributes.Define<int>(MaxStack);
        manager.Catalog.Attributes.Define<int>(StackRatio);

        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static ItemInstance<string> CreateProbeInstance(ItemDefinition<string> definition)
    {
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(1000), definition);

        Assert.That(inventory.TryAdd(definition, out var failure), Is.True);
        return inventory.Items.Single();
    }
}

