using System.IO;
using NUnit.Framework;
using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.Capacity;

[TestFixture]
[Category("Example")]
public class WeightCapacityPolicyExampleTests
{
    private static readonly AttributeKey<double> Weight = new("example-weight");
    private static readonly ItemSchema<string> WeightedSchema = ItemSchema<string>.Create("example-weighted").Require(Weight);

    [Test]
    public void LimitsInventoryByTotalWeight()
    {
        var sword = new WeightedDefinition("sword", 4);
        var potion = new WeightedDefinition("potion", 0.5);
        var inventory = CreateInventory(new WeightCapacityPolicy<string>(Weight, 5), sword, potion);

        inventory.TryAdd(sword, out _);
        var accepted = inventory.TryAdd(potion, out var error, 3);

        WriteExample("Capacity", "WeightCapacityPolicyExample.txt",
            $"accepted: {accepted}\nerror: {error}\ntotal count: {inventory.TotalItemCount}");
    }

    private static Inventory<string> CreateInventory(ICapacityPolicy<string> capacity, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            capacity,
            new EntryLayout<string>());
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Registry.Freeze();
        return manager.CreateInventory();
    }

    private static void WriteExample(string area, string fileName, string content)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", area);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private sealed class WeightedDefinition : ItemDefinition<string>
    {
        public WeightedDefinition(string id, double weight)
            : base(id, WeightedSchema)
        {
            DefineAttribute(Weight, weight);
        }
    }
}
