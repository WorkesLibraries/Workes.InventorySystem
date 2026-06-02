using System.IO;
using System.Linq;
using System.Text;
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
    private const string Weight = "example-weight";
    private static readonly ItemSchema<string> WeightedSchema = ItemSchema<string>.Create("example-weighted").Require<double>(Weight);

    [Test]
    public void LimitsInventoryByTotalWeight()
    {
        var sword = new WeightedDefinition("sword", 4.0);
        var potion = new WeightedDefinition("potion", 0.5);
        var shield = new WeightedDefinition("shield", 3.0);
        var feather = new ItemDefinition<string>("feather");
        var inventory = CreateInventory(new WeightCapacityPolicy<string>(Weight, maxWeight: 5), sword, potion, shield, feather);
        var operations = new StringBuilder();

        AppendOperation(operations, "add sword x1", inventory.TryAdd(sword, out var error), error);
        AppendOperation(operations, "add potion x2", inventory.TryAdd(potion, out error, 2), error);
        AppendOperation(operations, "add shield x1", inventory.TryAdd(shield, out error), error);
        AppendOperation(operations, "add feather x10", inventory.TryAdd(feather, out error, 10), error);

        WriteExample("Capacity", "WeightCapacityPolicyExample.txt", Describe(inventory, operations.ToString()));
    }

    private static void AppendOperation(StringBuilder builder, string label, bool accepted, string? error)
    {
        builder.Append(label).Append(": ");
        builder.AppendLine(accepted ? "accepted" : "rejected - " + error);
    }

    private static string Describe(Inventory<string> inventory, string operations)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Weight capacity");
        builder.AppendLine("---------------");
        builder.AppendLine("Maximum weight: 5");
        builder.AppendLine("Effective total weight: " + CalculateWeight(inventory));
        builder.AppendLine();
        builder.Append(operations);
        builder.AppendLine();
        builder.AppendLine("Final contents");
        foreach (var item in inventory.Items.OrderBy(i => i.Definition.Id))
            builder.AppendLine(item.Definition.Id + " x" + item.Amount);
        return builder.ToString();
    }

    private static double CalculateWeight(Inventory<string> inventory)
    {
        double total = 0;
        foreach (var item in inventory.Items)
        {
            if (item.Definition.Attributes.TryGet<double>(Weight, out var weight))
                total += weight * item.Amount;
        }

        return total;
    }

    private static Inventory<string> CreateInventory(ICapacityPolicy<string> capacity, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            capacity,
            new EntryLayout<string>());
        manager.Catalog.Attributes.Define<double>(Weight);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
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



