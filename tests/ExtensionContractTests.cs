using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class ExtensionContractTests
{
    [Test]
    public void ExtensionInterfaces_RemainPublic()
    {
        Assert.That(typeof(IInventoryLayout<>).IsPublic, Is.True);
        Assert.That(typeof(ICapacityPolicy<>).IsPublic, Is.True);
        Assert.That(typeof(IStackResolver<>).IsPublic, Is.True);
        Assert.That(typeof(IRulePolicy<>).IsPublic, Is.True);
    }
}
