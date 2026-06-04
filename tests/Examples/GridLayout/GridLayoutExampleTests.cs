using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests.Examples.GridLayout;

[TestFixture]
[Category("Example")]
public class GridLayoutExampleTests
{
    [Test]
    public void ManualGridPlacement_WritesReadableExample()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateManager(new Workes.InventorySystem.Layout.GridLayout<string>(3, 2), apple).CreateInventory();

        var placed = inventory.TryAdd(apple, out var error, 5, GridLayoutContext<string>.Single(2, 1));

        Assert.That(placed, Is.True, error);
        WriteOutput("ManualGridPlacement.txt", DescribeGrid(inventory, 3, 2));
    }

    [Test]
    public void GridAutoPlacementOrder_WritesReadableExample()
    {
        var apple = new ItemDefinition<string>("apple");
        var rowMajor = CreateManager(new Workes.InventorySystem.Layout.GridLayout<string>(3, 2), 1, apple).CreateInventory();
        var columnMajor = CreateManager(new Workes.InventorySystem.Layout.GridLayout<string>(3, 2, GridPlacementOrder.ColumnMajor), 1, apple).CreateInventory();

        for (int i = 0; i < 3; i++)
        {
            rowMajor.TryAdd(apple, out _);
            columnMajor.TryAdd(apple, out _);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Row-major");
        builder.Append(DescribeGrid(rowMajor, 3, 2));
        builder.AppendLine("Column-major");
        builder.Append(DescribeGrid(columnMajor, 3, 2));
        WriteOutput("GridAutoPlacementOrder.txt", builder.ToString());
    }

    [Test]
    public void MappedGridTransaction_WritesReadableExample()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateManager(new Workes.InventorySystem.Layout.GridLayout<string>(3, 2), apple, sword).CreateInventory();
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _, 5);
        builder.TryAdd(sword, out _, 1);
        var context = GridLayoutContext<string>.Map()
            .Add(0, 2, 0)
            .Add(1, 0, 1)
            .Build();

        var built = builder.TryToInventoryTransaction(context, out var transaction, out var error);
        var committed = built && inventory.TryCommitTransaction(transaction!, out error);

        Assert.That(committed, Is.True, error);
        WriteOutput("MappedGridTransaction.txt", DescribeGrid(inventory, 3, 2));
    }

    [Test]
    public void MappedGridTransfer_WritesReadableExample()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(new Workes.InventorySystem.Layout.GridLayout<string>(3, 2), apple, sword);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 5);
        source.TryAdd(sword, out _, 1);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemove(source.Find(apple)[0], 5, out _);
        transfer.TryRemove(source.Find(sword)[0], 1, out _);
        var context = GridLayoutContext<string>.Map()
            .Add(0, 1, 0)
            .Add(1, 2, 1)
            .Build();

        Assert.That(InventoryTransfer.TryTransfer(transfer, target, context, out var error), Is.True, error);

        WriteOutput("MappedGridTransfer.txt", DescribeGrid(target, 3, 2));
    }

    private static InventoryManager<string> CreateManager(IInventoryLayout<string> layout, params ItemDefinition<string>[] definitions)
    {
        return CreateManager(layout, 10, definitions);
    }

    private static InventoryManager<string> CreateManager(IInventoryLayout<string> layout, int maxStack, params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new RuleContainer<string>());

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    private static string DescribeGrid(Inventory<string> inventory, int width, int height)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Grid");
        builder.AppendLine("----");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var item = inventory.Layout.GetItemAt(inventory, GridLayoutContext<string>.Single(x, y));
                builder.Append('[');
                builder.Append(item == null ? "empty" : item.Definition.Id + " x" + item.Amount);
                builder.Append(']');
                if (x < width - 1)
                    builder.Append(' ');
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static void WriteOutput(string fileName, string content)
    {
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ExampleOutputs", "GridLayout", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content);
        TestContext.Out.WriteLine("Grid layout example output: " + outputPath);
    }
}


