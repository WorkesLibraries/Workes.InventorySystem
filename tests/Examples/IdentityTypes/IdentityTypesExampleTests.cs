using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Tests.Examples.IdentityTypes;

[TestFixture]
[Category("Example")]
public class IdentityTypesExampleTests
{
    [Test]
    public void CatalogsCanUseStringGuidAndExplicitIntegerIdentities()
    {
        var stringCatalog = new ItemCatalog<string>();
        var stringCoin = new ItemDefinition<string>("coin");
        stringCatalog.Registry.Register(stringCoin);
        stringCatalog.Freeze();

        var guidCatalog = new ItemCatalog<Guid>();
        var potionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guidPotion = new ItemDefinition<Guid>(potionId);
        guidCatalog.Registry.Register(guidPotion);
        guidCatalog.Freeze();

        var intCatalog = new ItemCatalog<int>();
        var intCoin = new ItemDefinition<int>(1001);
        var intPotion = new ItemDefinition<int>(1002);
        intCatalog.Registry.Register(intCoin);
        intCatalog.Registry.Register(intPotion);
        intCatalog.Registry.RegisterMigration(1, intCoin);
        intCatalog.Freeze();

        Assert.That(stringCatalog.Registry.Resolve("coin"), Is.SameAs(stringCoin));
        Assert.That(guidCatalog.Registry.Resolve(potionId), Is.SameAs(guidPotion));
        Assert.That(intCatalog.Registry.Resolve(1001), Is.SameAs(intCoin));
        Assert.That(intCatalog.Registry.Resolve(1002), Is.SameAs(intPotion));
        Assert.That(intCatalog.Registry.Resolve(1), Is.SameAs(intCoin));

        var output = BuildOutput(stringCoin, guidPotion, intCatalog, intCoin, intPotion);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Identity types example written to: {outputPath}");
    }

    private static string BuildOutput(
        ItemDefinition<string> stringCoin,
        ItemDefinition<Guid> guidPotion,
        ItemCatalog<int> intCatalog,
        ItemDefinition<int> intCoin,
        ItemDefinition<int> intPotion)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Identity Types Example");
        builder.AppendLine("======================");
        builder.AppendLine();
        builder.AppendLine("String Identity");
        builder.AppendLine("---------------");
        builder.AppendLine($"coin resolved by id: {stringCoin.Id}");
        builder.AppendLine();
        builder.AppendLine("Guid Identity");
        builder.AppendLine("-------------");
        builder.AppendLine($"potion resolved by id: {guidPotion.Id}");
        builder.AppendLine();
        builder.AppendLine("Explicit Int Identity");
        builder.AppendLine("---------------------");
        builder.AppendLine($"coin resolved by id: {intCoin.Id}");
        builder.AppendLine($"potion resolved by id: {intPotion.Id}");
        builder.AppendLine($"old id 1 migrated to: {intCatalog.Registry.Resolve(1).Id}");
        builder.AppendLine();
        builder.AppendLine("Notes");
        builder.AppendLine("-----");
        builder.AppendLine("Item definition ids are explicit stable identities.");
        builder.AppendLine("The examples use strings for readability, but integer-like ids remain supported when chosen deliberately.");

        return builder.ToString();
    }

    private static string WriteOutput(string output)
    {
        var directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "ExampleOutputs",
            "IdentityTypes");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "IdentityTypesExample.txt");
        File.WriteAllText(path, output);
        return path;
    }
}
