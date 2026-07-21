using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryMaxStackQueryTests
{
    [Test]
    public void GetMaxStackSize_ResolvesDefinitionAndMigratedId()
    {
        var apple = new ItemDefinition<string>("apple");
        var catalog = new ItemCatalog<string>();
        catalog.Registry.Register(apple);
        catalog.Registry.RegisterMigration("old-apple", apple);
        catalog.Freeze();
        var inventory = CreateInventory(
            catalog,
            new FixedSizeStackResolver<string>(20));

        Assert.That(inventory.GetMaxStackSize(apple), Is.EqualTo(20));
        Assert.That(inventory.GetMaxStackSize("apple"), Is.EqualTo(20));
        Assert.That(inventory.GetMaxStackSize("old-apple"), Is.EqualTo(20));
    }

    [Test]
    public void TryGetMaxStackSize_ReturnsStructuredFailureForUnknownId()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            CreateCatalog(apple),
            new FixedSizeStackResolver<string>(10));

        Assert.That(inventory.TryGetMaxStackSize("missing", out var maxStack, out var failure), Is.False);
        Assert.That(maxStack, Is.Zero);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Definition));
        Assert.That(failure?.Code, Is.EqualTo(InventoryFailureCodes.DefinitionUnresolved));
        Assert.Throws<InventoryOperationException>(() => inventory.GetMaxStackSize("missing"));
    }

    [Test]
    public void TryGetMaxStackSize_ReturnsStructuredFailureForUnregisteredDefinition()
    {
        var registered = new ItemDefinition<string>("registered");
        var unregistered = new ItemDefinition<string>("unregistered");
        var inventory = CreateInventory(
            CreateCatalog(registered),
            new FixedSizeStackResolver<string>(10));

        Assert.That(inventory.TryGetMaxStackSize(unregistered, out var maxStack, out var failure), Is.False);
        Assert.That(maxStack, Is.Zero);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Definition));
    }

    [Test]
    public void GetMaxStackSize_UsesDefinitionAttributesAndInventoryState()
    {
        var apple = new StackDefinition("apple", baseStack: 4);
        var catalog = new ItemCatalog<string>();
        catalog.Attributes.Define<int>(StackDefinition.BaseStackAttribute);
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var inventory = CreateInventory(
            catalog,
            new MultipliedAttributeStackResolver<string>(
                StackDefinition.BaseStackAttribute,
                multiplier: 2.5));

        Assert.That(inventory.GetMaxStackSize("apple"), Is.EqualTo(10));
    }

    [Test]
    public void GetMaxStackSize_UsesProvidedMetadataWhenResolverIsMetadataSensitive()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            CreateCatalog(apple),
            new MetadataTierStackResolver());
        var bulk = new InstanceMetadata();
        bulk.Set("tier", "bulk");
        var tiny = new InstanceMetadata();
        tiny.Set("tier", "tiny");

        Assert.That(inventory.GetMaxStackSize(apple), Is.EqualTo(5));
        Assert.That(inventory.GetMaxStackSize(apple, bulk), Is.EqualTo(50));
        Assert.That(inventory.GetMaxStackSize("apple", tiny), Is.EqualTo(1));
    }

    [Test]
    public void GetMaxStackSize_ItemInstanceOverloadUsesExistingMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            CreateCatalog(apple),
            new MetadataTierStackResolver());
        var metadata = new InstanceMetadata();
        metadata.Set("tier", "bulk");
        var add = InventoryTransaction<string>.From(inventory);
        Assert.That(add.TryAdd(apple, amount: 3, context: null, metadata, out var addFailure), Is.True, addFailure?.Message);
        inventory.CommitTransaction(add);

        Assert.That(inventory.GetMaxStackSize(inventory.Items.Single()), Is.EqualTo(50));
    }

    private static ItemCatalog<string> CreateCatalog(params ItemDefinition<string>[] definitions)
    {
        var catalog = new ItemCatalog<string>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();
        return catalog;
    }

    private static Inventory<string> CreateInventory(
        ItemCatalog<string> catalog,
        IStackResolver<string> resolver)
    {
        return new InventoryManager<string>(
            resolver,
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog).CreateInventory();
    }

    private sealed class StackDefinition : ItemDefinition<string>
    {
        public const string BaseStackAttribute = "baseStack";

        public static readonly ItemSchema<string> StackSchema =
            ItemSchema<string>.CreateFor<StackDefinition>("stack-definition")
                .RequireAttribute<int>(BaseStackAttribute, inherited: true);

        public StackDefinition(string id, int baseStack)
            : base(id, StackSchema)
        {
            DefineAttribute(BaseStackAttribute, baseStack);
        }
    }

    private sealed class MetadataTierStackResolver : IStackResolver<string>
    {
        public int ResolveMaxStackSize(Inventory<string> inventory, ItemInstance<string> instance)
        {
            if (instance.Metadata.TryGet<string>("tier", out var tier))
            {
                return tier switch
                {
                    "bulk" => 50,
                    "tiny" => 1,
                    _ => 5
                };
            }

            return 5;
        }
    }
}
