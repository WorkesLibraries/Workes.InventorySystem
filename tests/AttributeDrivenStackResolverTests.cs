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

    [Test]
    public void ConditionalMaxStackResolver_ReturnsMaxStack_WhenAttributeTrue()
    {
        var coin = new StackableOnlyDefinition("coin", stackable: true);
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(coin));

        Assert.That(maxStack, Is.EqualTo(10));
    }

    [Test]
    public void ConditionalMaxStackResolver_ReturnsOne_WhenAttributeFalse()
    {
        var sword = new StackableOnlyDefinition("sword", stackable: false);
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), sword);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(sword));

        Assert.That(maxStack, Is.EqualTo(1));
    }

    [Test]
    public void ConditionalMaxStackResolver_ReturnsOne_WhenAttributeMissingByDefault()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(note));

        Assert.That(maxStack, Is.EqualTo(1));
    }

    [Test]
    public void ConditionalMaxStackResolver_CanTreatMissingAttributeAsStackable()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new ConditionalMaxStackResolver<string>(Stackable, 10, missingAttributeIsStackable: true), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(note));

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

        var accepted = resolver.TryCreateWithParameter(null!, "maxStack", 20, out var replacement, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((ConditionalMaxStackResolver<string>)replacement!).MaxStack, Is.EqualTo(20));
    }

    [Test]
    public void ConditionalMaxStackResolver_TryCreateWithParameter_UpdatesMissingAttributeBehavior()
    {
        var resolver = new ConditionalMaxStackResolver<string>(Stackable, 10);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeIsStackable", true, out var replacement, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((ConditionalMaxStackResolver<string>)replacement!).MissingAttributeIsStackable, Is.True);
    }

    [Test]
    public void AttributeMaxStackResolver_UsesDefinitionAttribute()
    {
        var coin = new MaxStackOnlyDefinition("coin", 25);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), coin);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(coin));

        Assert.That(maxStack, Is.EqualTo(25));
    }

    [Test]
    public void AttributeMaxStackResolver_UsesConfiguredFallback_WhenAttributeMissing()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack, missingAttributeMaxStack: 3), note);

        var maxStack = inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(note));

        Assert.That(maxStack, Is.EqualTo(3));
    }

    [Test]
    public void AttributeMaxStackResolver_Throws_WhenAttributeMissingAndFallbackIsNull()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(note)));
    }

    [Test]
    public void AttributeMaxStackResolver_Throws_WhenAttributeValueIsInvalid()
    {
        var broken = new MaxStackOnlyDefinition("broken", 0);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), broken);

        Assert.Throws<InvalidOperationException>(() =>
            inventory.StackResolver.ResolveMaxStackSize(inventory, new ItemInstance<string>(broken)));
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

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeMaxStack", 4, out var replacement, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((AttributeMaxStackResolver<string>)replacement!).MissingAttributeMaxStack, Is.EqualTo(4));
    }

    [Test]
    public void AttributeMaxStackResolver_TryCreateWithParameter_AllowsStrictNullFallback()
    {
        var resolver = new AttributeMaxStackResolver<string>(MaxStack, 4);

        var accepted = resolver.TryCreateWithParameter(null!, "missingAttributeMaxStack", null, out var replacement, out var error);

        Assert.That(accepted, Is.True, error);
        Assert.That(((AttributeMaxStackResolver<string>)replacement!).MissingAttributeMaxStack, Is.Null);
    }

    [Test]
    public void TryAdd_ReturnsFalse_WhenStrictAttributeMaxResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        var accepted = inventory.TryAdd(note, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("missing stack-size attribute"));
    }

    [Test]
    public void Add_Throws_WhenStrictAttributeMaxResolverFindsMissingAttribute()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), note);

        Assert.Throws<InvalidOperationException>(() => inventory.Add(note));
    }

    [Test]
    public void TryAdd_SplitsByAttributeDefinedMaxStack()
    {
        var coin = new MaxStackOnlyDefinition("coin", 3);
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack), coin);

        var accepted = inventory.TryAdd(coin, out var error, amount: 8);

        Assert.That(accepted, Is.True, error);
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 3, 3, 2 }));
    }

    [Test]
    public void TrySetStackResolverParameter_ReturnsFalse_WhenStrictFallbackInvalidatesCurrentContents()
    {
        var note = new ItemDefinition<string>("note");
        var inventory = CreateInventory(new AttributeMaxStackResolver<string>(MaxStack, missingAttributeMaxStack: 10), note);
        inventory.Add(note, amount: 5);

        var accepted = inventory.TrySetStackResolverParameter("missingAttributeMaxStack", null, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("missing stack-size attribute"));
        Assert.That(((AttributeMaxStackResolver<string>)inventory.StackResolver).MissingAttributeMaxStack, Is.EqualTo(10));
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            stackResolver,
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>());

        manager.Catalog.Attributes.Define<bool>(Stackable);
        manager.Catalog.Attributes.Define<int>(MaxStack);

        foreach (var definition in definitions)
            manager.Registry.Register(definition);

        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }
}
