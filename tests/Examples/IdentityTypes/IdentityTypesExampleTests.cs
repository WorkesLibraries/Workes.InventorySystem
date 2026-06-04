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
    public void CatalogsCanUseStringGuidAndAutoIncrementIntIdentities()
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
        intCatalog.Registry.EnableAutoIncrement();
        var intCoin = intCatalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));
        var intPotion = intCatalog.Registry.RegisterAuto(id => new ItemDefinition<int>(id));
        intCatalog.Freeze();

        var strictCatalog = new ItemCatalog<int>();
        strictCatalog.Registry.EnableAutoIncrement(AutoIncrementMode.Strict);
        var explicitAfterStrictWasRejected = false;
        try
        {
            strictCatalog.Registry.Register(new ItemDefinition<int>(1));
        }
        catch (InvalidOperationException)
        {
            explicitAfterStrictWasRejected = true;
        }

        Assert.That(stringCatalog.Registry.Resolve("coin"), Is.SameAs(stringCoin));
        Assert.That(guidCatalog.Registry.Resolve(potionId), Is.SameAs(guidPotion));
        Assert.That(intCoin.Id, Is.EqualTo(1));
        Assert.That(intPotion.Id, Is.EqualTo(2));
        Assert.That(explicitAfterStrictWasRejected, Is.True);

        var output = BuildOutput(stringCoin, guidPotion, intCoin, intPotion, explicitAfterStrictWasRejected);
        var outputPath = WriteOutput(output);
        TestContext.Out.WriteLine($"Identity types example written to: {outputPath}");
    }

    private static string BuildOutput(
        ItemDefinition<string> stringCoin,
        ItemDefinition<Guid> guidPotion,
        ItemDefinition<int> intCoin,
        ItemDefinition<int> intPotion,
        bool explicitAfterStrictWasRejected)
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
        builder.AppendLine("Auto-Increment Int Identity");
        builder.AppendLine("---------------------------");
        builder.AppendLine($"coin generated id: {intCoin.Id}");
        builder.AppendLine($"potion generated id: {intPotion.Id}");
        builder.AppendLine();
        builder.AppendLine("Strict Auto-Increment");
        builder.AppendLine("---------------------");
        builder.AppendLine($"explicit registration after strict enable: {(explicitAfterStrictWasRejected ? "rejected" : "accepted")}");

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
