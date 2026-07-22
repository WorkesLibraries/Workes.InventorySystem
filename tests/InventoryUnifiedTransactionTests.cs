using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

public class InventoryUnifiedTransactionTests
{
    [Test]
    public void LocalTransaction_CanApplyDeltaAndCommitItself()
    {
        var apple = new ItemDefinition<string>("apple");
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new EntryLayout<string>(), maxStack: 99, apple, coin);
        inventory.Add(coin, amount: 10);
        var delta = InventoryItemDelta<string>.Create()
            .Remove("coin", amount: 4, label: "price")
            .Add("apple", amount: 2, label: "reward");

        var transaction = InventoryTransaction<string>
            .For(inventory)
            .Apply(delta)
            .Build();

        Assert.That(transaction.Validate(out var failure), Is.True, failure?.ToString());
        Assert.That(transaction.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.Count(coin), Is.EqualTo(6));
        Assert.That(inventory.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void LocalTransactionBuilder_CanCommitItself()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), maxStack: 99, apple);
        var builder = InventoryTransaction<string>
            .For(inventory)
            .Apply(InventoryItemDelta<string>.Create().Add("apple", amount: 2));

        Assert.That(builder.Validate(out var failure), Is.True, failure?.ToString());
        Assert.That(builder.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.Count(apple), Is.EqualTo(2));
    }

    [Test]
    public void LocalTransaction_RejectsCommitWhenInventoryChangedAfterBuild()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), maxStack: 99, apple);
        var transaction = InventoryTransaction<string>
            .For(inventory)
            .Apply(InventoryItemDelta<string>.Create().Add("apple", amount: 2))
            .Build();

        inventory.Add(apple, amount: 1);

        Assert.That(transaction.TryCommit(out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(inventory.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void CrossInventoryTransaction_AppliesMirroredDeltaAtomically()
    {
        var plate = new ItemDefinition<string>("silver_plate");
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, plate, coin);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(plate, amount: 3);
        npc.Add(coin, amount: 72);
        var playerDelta = InventoryItemDelta<string>.Create()
            .Remove("silver_plate", amount: 1, label: "plate")
            .Add("coin", amount: 4, label: "coins");

        InventoryTransaction<string> transaction = InventoryTransaction<string>
            .From(player)
            .To(npc)
            .ApplyMirrored(playerDelta);

        Assert.That(transaction.Validate(out var failure), Is.True, failure?.ToString());
        Assert.That(transaction.TryCommit(out failure), Is.True, failure?.ToString());

        Assert.That(player.Count(plate), Is.EqualTo(2));
        Assert.That(player.Count(coin), Is.EqualTo(4));
        Assert.That(npc.Count(plate), Is.EqualTo(1));
        Assert.That(npc.Count(coin), Is.EqualTo(68));
    }

    [Test]
    public void CrossInventoryTransaction_CanBeCreatedFromEntries()
    {
        var plate = new ItemDefinition<string>("silver_plate");
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, plate, coin);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(plate, amount: 1);
        npc.Add(coin, amount: 4);
        var playerDelta = InventoryItemDelta<string>.Create()
            .Remove("silver_plate", amount: 1)
            .Add("coin", amount: 4);
        var npcDelta = InventoryItemDelta<string>.Mirror(playerDelta);

        InventoryTransaction<string> transaction = InventoryTransaction<string>
            .From(new InventoryTransactionEntry<string>(player, playerDelta))
            .To(new InventoryTransactionEntry<string>(npc, npcDelta));

        Assert.That(transaction.TryCommit(out var failure), Is.True, failure?.ToString());
        Assert.That(player.Count(plate), Is.Zero);
        Assert.That(player.Count(coin), Is.EqualTo(4));
        Assert.That(npc.Count(plate), Is.EqualTo(1));
        Assert.That(npc.Count(coin), Is.Zero);
    }

    [Test]
    public void CrossInventoryScaffoldingTypes_AreNotPublicApi()
    {
        var assembly = typeof(InventoryTransaction<string>).Assembly;

        Assert.That(assembly.GetType("Workes.InventorySystem.Core.InventoryCrossTransactionBuilder`1"), Is.Null);
        Assert.That(assembly.GetType("Workes.InventorySystem.Core.InventoryCrossTransactionStart`1"), Is.Null);
    }

    [Test]
    public void IncompleteEntryStartedCrossTransaction_CannotCommit()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), maxStack: 99, apple);
        var delta = InventoryItemDelta<string>.Create().Add("apple", amount: 1);
        InventoryTransaction<string> transaction =
            InventoryTransaction<string>.From(new InventoryTransactionEntry<string>(inventory, delta));

        Assert.That(transaction.Validate(out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(transaction.TryCommit(out failure), Is.False);
        Assert.Throws<InventoryOperationException>(() => transaction.Commit());
    }

    [Test]
    public void CrossInventoryTransaction_DoesNotMutateEitherInventoryWhenSecondSideFails()
    {
        var plate = new ItemDefinition<string>("silver_plate");
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, plate, coin);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(plate, amount: 3);
        var playerDelta = InventoryItemDelta<string>.Create()
            .Remove("silver_plate", amount: 1)
            .Add("coin", amount: 4);
        var npcDelta = InventoryItemDelta<string>.Mirror(playerDelta);

        var accepted = InventoryTransaction<string>
            .From(player)
            .To(npc)
            .TryApply(playerDelta, npcDelta, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Validation));
        Assert.That(player.Count(plate), Is.EqualTo(3));
        Assert.That(player.Count(coin), Is.Zero);
        Assert.That(npc.Count(plate), Is.Zero);
        Assert.That(npc.Count(coin), Is.Zero);
    }

    [Test]
    public void DeltaApplicationPlan_CanPlaceAdditionByLabel()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), maxStack: 10, apple);
        var plan = InventoryDeltaApplicationPlan<string>.Create()
            .ForAdditionLabel("reward", request =>
                InventoryPlacementDecision<string>.Place(SlotLayoutContext<string>.Single(2)));
        var delta = InventoryItemDelta<string>.Create()
            .Add("apple", amount: 1, label: "reward");

        var committed = InventoryTransaction<string>
            .For(inventory)
            .Apply(delta, plan)
            .Build()
            .TryCommit(out var failure);

        Assert.That(committed, Is.True, failure?.ToString());
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(2))!.Definition, Is.SameAs(apple));
    }

    [Test]
    public void DeltaApplicationPlan_RemovalRuleIsConstraint()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), maxStack: 10, apple);
        inventory.Add(apple, amount: 3, context: SlotLayoutContext<string>.Single(0));
        inventory.Add(apple, amount: 3, context: SlotLayoutContext<string>.Single(1));
        var plan = InventoryDeltaApplicationPlan<string>.Create()
            .ForRemovalLabel("auto-craft", candidate =>
            {
                bool allowed = candidate.Contexts
                    .OfType<SlotLayoutContext<string>>()
                    .Any(context => context.SlotIndex == 1);
                return allowed ? InventoryRemovalDecision.Allow() : InventoryRemovalDecision.Skip();
            });
        var delta = InventoryItemDelta<string>.Create()
            .RemoveAnyMetadata("apple", amount: 4, label: "auto-craft");

        var accepted = InventoryTransaction<string>
            .For(inventory)
            .TryApply(delta, plan, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Validation));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(3));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(3));
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        int maxStack,
        params ItemDefinition<string>[] definitions) =>
        CreateManager(layout, maxStack, definitions).CreateInventory();

    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string> layout,
        int maxStack,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(maxStack),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>(),
            new RuleContainer<string>());

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }
}
