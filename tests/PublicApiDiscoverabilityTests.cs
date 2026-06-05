using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class PublicApiDiscoverabilityTests
{
    [Test]
    public void ConcreteLayoutInfrastructureMethods_AreHiddenFromNormalIntelliSense()
    {
        AssertHidden(typeof(SlotLayout<string>), nameof(SlotLayout<string>.TryMove));
        AssertHidden(typeof(SlotLayout<string>), nameof(SlotLayout<string>.CanSatisfyPlacement));
        AssertHidden(typeof(SlotLayout<string>), nameof(SlotLayout<string>.OnItemAdded));
        AssertHidden(typeof(GridLayout<string>), nameof(GridLayout<string>.TrySort));
        AssertHidden(typeof(EntryLayout<string>), nameof(EntryLayout<string>.Clone));
    }

    [Test]
    public void ConcretePolicyAndResolverInfrastructureMethods_AreHiddenFromNormalIntelliSense()
    {
        AssertHidden(typeof(FixedSizeStackResolver<string>), nameof(FixedSizeStackResolver<string>.ResolveMaxStackSize));
        AssertHidden(typeof(AttributeMaxStackResolver<string>), nameof(AttributeMaxStackResolver<string>.ResolveMaxStackSize));
        AssertHidden(typeof(MultipliedAttributeStackResolver<string>), nameof(MultipliedAttributeStackResolver<string>.ResolveMaxStackSize));
        AssertHidden(typeof(MultipliedAttributeStackResolver<string>), nameof(MultipliedAttributeStackResolver<string>.TryCreateWithParameter));
        AssertHidden(typeof(MaxTotalItemAmountCapacityPolicy<string>), nameof(MaxTotalItemAmountCapacityPolicy<string>.CanApply));
        AssertHidden(typeof(WeightCapacityPolicy<string>), nameof(WeightCapacityPolicy<string>.TryCreateWithParameter));
    }

    [Test]
    public void ExtensionInterfaces_RemainPublic()
    {
        Assert.That(typeof(IInventoryLayout<>).IsPublic, Is.True);
        Assert.That(typeof(ICapacityPolicy<>).IsPublic, Is.True);
        Assert.That(typeof(IStackResolver<>).IsPublic, Is.True);
        Assert.That(typeof(IRulePolicy<>).IsPublic, Is.True);
    }

    [Test]
    public void AutoIncrementMode_IsNotPublicApi()
    {
        var type = typeof(ItemRegistry<>).Assembly.GetType("Workes.InventorySystem.Core.AutoIncrementMode");

        Assert.That(type, Is.Null);
    }

    [Test]
    public void ItemRegistry_DoesNotExposeAutoIncrementApis()
    {
        var type = typeof(ItemRegistry<int>);

        Assert.That(type.GetMethod("EnableAutoIncrement", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetMethod("RegisterAuto", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetProperty("AutoIncrementEnabled", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetProperty("AutoIncrementMode", BindingFlags.Public | BindingFlags.Instance), Is.Null);
    }

    [Test]
    public void TagKey_IsNotPublicApi()
    {
        var type = typeof(TagCatalog).Assembly.GetType("Workes.InventorySystem.Tags.TagKey");

        Assert.That(type, Is.Not.Null);
        Assert.That(type!.IsPublic, Is.False);
    }

    [Test]
    public void TagContainer_IsNotPublicApi()
    {
        var type = typeof(TagCatalog).Assembly.GetType("Workes.InventorySystem.Tags.TagContainer");

        Assert.That(type, Is.Not.Null);
        Assert.That(type!.IsPublic, Is.False);
        Assert.That(typeof(ItemDefinition<string>).GetProperty("Tags")!.PropertyType, Is.EqualTo(typeof(IReadOnlyCollection<string>)));
    }

    [Test]
    public void TagCatalog_ExposesModeSelectionApis()
    {
        var type = typeof(TagCatalog);

        Assert.That(type.GetProperty(nameof(TagCatalog.Mode), BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
        Assert.That(type.GetMethod(nameof(TagCatalog.UseNamespacedTagsOnly), BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
        Assert.That(type.GetMethod(nameof(TagCatalog.UseNonNamespacedTagsOnly), BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
    }

    [Test]
    public void ItemInstance_DoesNotExposePublicConstructors()
    {
        var constructors = typeof(ItemInstance<string>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.That(constructors, Is.Empty);
    }

    [Test]
    public void ItemInstance_DoesNotExposePublicAmountMutators()
    {
        var type = typeof(ItemInstance<string>);

        Assert.That(type.GetMethod("SetAmount", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetMethod("AddAmount", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetMethod("ReduceAmount", BindingFlags.Public | BindingFlags.Instance), Is.Null);
    }

    [Test]
    public void ItemInstance_OwnershipMethodsAreNotPublic()
    {
        var type = typeof(ItemInstance<string>);

        Assert.That(type.GetMethod("AttachOwner", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(type.GetMethod("DetachOwner", BindingFlags.Public | BindingFlags.Instance), Is.Null);
    }

    [Test]
    public void InventoryTransferEntry_DoesNotExposePublicConstructors()
    {
        var constructors = typeof(InventoryTransferEntry<string>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.That(constructors, Is.Empty);
    }

    private static void AssertHidden(Type type, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");

        var attribute = method!.GetCustomAttribute<EditorBrowsableAttribute>();
        Assert.That(attribute, Is.Not.Null, $"{type.Name}.{methodName} should have EditorBrowsableAttribute.");
        Assert.That(attribute!.State, Is.EqualTo(EditorBrowsableState.Never));
    }
}
