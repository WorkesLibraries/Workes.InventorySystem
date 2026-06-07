using System.IO;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.InventoryPolicyMutation;

[TestFixture]
[Category("Example")]
public class StackIncreaseCompactionPolicyMutationExampleTests
{
    [Test]
    public void StackIncreaseCanOptionallyCompressCompatibleStacks()
    {
        var preserveInventory = CreateInventory(maxStack: 10, slotCount: 4, "coin");
        var compressInventory = CreateInventory(maxStack: 10, slotCount: 4, "coin");
        var compressAndRepackInventory = CreateInventory(maxStack: 10, slotCount: 4, "coin");
        var splitInventory = CreateInventory(maxStack: 10, slotCount: 3, "coin");
        var preserveCoin = preserveInventory.Manager.Registry.Resolve("coin");
        var compressCoin = compressInventory.Manager.Registry.Resolve("coin");
        var compressAndRepackCoin = compressAndRepackInventory.Manager.Registry.Resolve("coin");
        var splitCoin = splitInventory.Manager.Registry.Resolve("coin");

        preserveInventory.Add(preserveCoin, amount: 40);
        compressInventory.Add(compressCoin, amount: 40);
        compressAndRepackInventory.Add(compressAndRepackCoin, amount: 40);
        splitInventory.Add(splitCoin, amount: 10);

        InventoryChangedEventArgs<string>? compressAndRepackEvent = null;
        compressAndRepackInventory.Changed += (_, args) => compressAndRepackEvent = args;

        var preserveUpgrade = preserveInventory.TrySetStackResolverParameter("maxStack", 25, out var preserveError);
        var compressUpgrade = compressInventory.TrySetStackResolverParameter(
            "maxStack",
            25,
            InventoryParameterMutationActions.CompressCompatibleStacks,
            out var compressError);
        var compressAndRepackUpgrade = compressAndRepackInventory.TrySetStackResolverParameter(
            "maxStack",
            25,
            InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.CompressCompatibleStacks,
            out var compressAndRepackError);
        var splitDowngrade = splitInventory.TrySetStackResolverParameter(
            "maxStack",
            4,
            InventoryParameterMutationActions.SplitOversizedStacks,
            out var splitError);

        Assert.That(preserveUpgrade, Is.True, preserveError);
        Assert.That(compressUpgrade, Is.True, compressError);
        Assert.That(compressAndRepackUpgrade, Is.True, compressAndRepackError);
        Assert.That(splitDowngrade, Is.True, splitError);
        Assert.That(preserveInventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 10, 10, 10, 10 }));
        Assert.That(compressInventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 25, 15 }));
        Assert.That(compressAndRepackInventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 25, 15 }));
        Assert.That(compressAndRepackEvent, Is.Not.Null);
        Assert.That(compressAndRepackEvent!.RequiresFullRefresh, Is.True);
        Assert.That(splitInventory.Items.Select(item => item.Amount), Is.EqualTo(new[] { 4, 4, 2 }));

        var output =
            "Stack Increase Compaction Example\n" +
            "=================================\n\n" +
            "Operations\n" +
            "----------\n" +
            "Add coin x40 with maxStack 10: committed\n" +
            "Increase maxStack to 25 without compression: committed\n" +
            "Inventory remains: 10, 10, 10, 10\n\n" +
            "Increase maxStack to 25 with compression: committed\n" +
            "Inventory becomes: 25, 15\n\n" +
            "Increase maxStack to 25 with compression and repack: committed\n" +
            "Inventory becomes: 25, 15\n" +
            "Full refresh required: yes\n\n" +
            "Split Oversized Stack Example\n" +
            "-----------------------------\n" +
            "Lower maxStack 10 -> 4 with split only: 4, 4, 2";

        WriteOutput(output);
    }

    private static Inventory<string> CreateInventory(int maxStack, int slotCount, params string[] definitionIds)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            new SlotLayout<string>(slotCount),
            new ItemCatalog<string>()
            );
        foreach (var definitionId in definitionIds)
            manager.Registry.Register(new ItemDefinition<string>(definitionId));
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static void WriteOutput(string output)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "InventoryPolicyMutation");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "StackIncreaseCompactionPolicyMutationExample.txt");
        File.WriteAllText(path, output);
        TestContext.Out.WriteLine("Stack increase compaction policy mutation example output: " + path);
    }
}
