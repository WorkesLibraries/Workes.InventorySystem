using System.ComponentModel;
using System.Reflection;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

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
        AssertHidden(typeof(DefaultStackResolver<string>), nameof(DefaultStackResolver<string>.ResolveMaxStackSize));
        AssertHidden(typeof(AttributeMaxStackResolver<string>), nameof(AttributeMaxStackResolver<string>.ResolveMaxStackSize));
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

    private static void AssertHidden(Type type, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");

        var attribute = method!.GetCustomAttribute<EditorBrowsableAttribute>();
        Assert.That(attribute, Is.Not.Null, $"{type.Name}.{methodName} should have EditorBrowsableAttribute.");
        Assert.That(attribute!.State, Is.EqualTo(EditorBrowsableState.Never));
    }
}
