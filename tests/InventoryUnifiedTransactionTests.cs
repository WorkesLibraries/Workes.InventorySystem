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
    public void InventoryOwnedTransactionCommitMethods_AreNotPublicApi()
    {
        var publicMethods = typeof(Inventory<string>)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Select(method => method.Name)
            .ToArray();

        Assert.That(publicMethods, Does.Not.Contain("CommitTransaction"));
        Assert.That(publicMethods, Does.Not.Contain("TryCommitTransaction"));
        Assert.That(publicMethods, Does.Not.Contain("CanCommitTransaction"));
    }

    [Test]
    public void TransferApiFamily_IsMarkedObsolete()
    {
        var assembly = typeof(Inventory<string>).Assembly;
        AssertObsolete(assembly.GetType("Workes.InventorySystem.Core.InventoryTransfer"));
        AssertObsolete(assembly.GetType("Workes.InventorySystem.Core.InventoryTransferBuilder`1"));
        AssertObsolete(assembly.GetType("Workes.InventorySystem.Core.InventoryTransferEntry`1"));

        var inventoryType = typeof(Inventory<string>);
        foreach (var methodName in new[]
        {
            "CanCommitTransfer",
            "TryCommitTransfer",
            "CommitTransfer"
        })
        {
            var methods = inventoryType
                .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(method => method.Name == methodName)
                .ToArray();

            Assert.That(methods, Is.Not.Empty, methodName);
            Assert.That(methods.All(method => method.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Any()), Is.True, methodName);
        }

        foreach (var methodName in new[]
        {
            "CanTransferTo",
            "TryTransferTo",
            "TransferTo",
            "TryMoveAllTo",
            "TryMoveWhereTo",
            "TryMoveByTagTo",
            "TryMoveAllTagsTo",
            "TryTransferMaximumTo",
            "TryMoveMaximumWhereTo",
            "TryMoveMaximumByTagTo"
        })
        {
            var methods = inventoryType
                .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(method => method.Name == methodName)
                .ToArray();

            Assert.That(methods, Is.Not.Empty, methodName);
            Assert.That(methods.Any(method => method.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Any()), Is.False, methodName);
        }

        static void AssertObsolete(Type? type)
        {
            Assert.That(type, Is.Not.Null);
            Assert.That(type!.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false), Is.Not.Empty);
        }
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
    public void CrossInventoryTransaction_ApplyMirroredRejectsWildcardMetadataRemovals()
    {
        var gem = new ItemDefinition<string>("gem");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, gem);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "rare");
        InventoryTransaction<string>
            .For(player)
            .Add(gem, amount: 1, context: null, metadata: metadata)
            .Commit();
        var playerDelta = InventoryItemDelta<string>.Create()
            .Remove("gem", 1, ItemMetadataMatch.Any, label: "runtime-selected-gem");

        var transaction = InventoryTransaction<string>
            .From(player)
            .To(npc);

        Assert.That(transaction.TryApplyMirrored(playerDelta, out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(failure?.Message, Does.Contain("Wildcard-metadata remove operations cannot be mirrored"));
        Assert.Throws<InventoryOperationException>(() => transaction.ApplyMirrored(playerDelta));
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
            .Remove("apple", amount: 4, label: "auto-craft");

        var accepted = InventoryTransaction<string>
            .For(inventory)
            .TryApply(delta, plan, out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Validation));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(3));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(3));
    }

    [Test]
    public void ManualTransactionBuilder_FluentOperationsCanBeChainedAndCommitted()
    {
        var apple = new ItemDefinition<string>("apple");
        var coin = new ItemDefinition<string>("coin");
        var inventory = CreateInventory(new SlotLayout<string>(3), maxStack: 99, apple, coin);
        inventory.Add(coin, amount: 10, context: SlotLayoutContext<string>.Single(0));

        var builder = InventoryTransaction<string>.For(inventory);
        var returned = builder
            .Remove("coin", amount: 4, context: SlotLayoutContext<string>.Single(0))
            .Add("apple", amount: 2, context: SlotLayoutContext<string>.Single(2));

        Assert.That(returned, Is.SameAs(builder));
        Assert.That(builder.TryCommit(out var failure), Is.True, failure?.ToString());
        Assert.That(inventory.Count(coin), Is.EqualTo(6));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(2))!.Definition, Is.SameAs(apple));
    }

    [Test]
    public void ManualTransactionBuilder_ThrowingWrapperPreservesFailure()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), maxStack: 99, apple);
        var builder = InventoryTransaction<string>.For(inventory);

        var ex = Assert.Throws<InventoryOperationException>(() =>
            builder.RemoveAtContext(SlotLayoutContext<string>.Single(1)));

        Assert.That(ex!.Failure.Kind, Is.EqualTo(InventoryFailureKind.Layout));
        Assert.That(builder.IsEmpty, Is.True);
    }

    [Test]
    public void ManualTransactionBuilder_TryRemoveAtContext_RemovesClickedSlot()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), maxStack: 99, apple);
        inventory.Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(0));
        inventory.Add(apple, amount: 7, context: SlotLayoutContext<string>.Single(1));

        var builder = InventoryTransaction<string>.For(inventory);
        var accepted = builder.TryRemoveAtContext(
            SlotLayoutContext<string>.Single(1),
            out var failure,
            amount: 3);

        Assert.That(accepted, Is.True, failure?.ToString());
        Assert.That(builder.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(5));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(4));
    }

    [Test]
    public void ManualTransactionBuilder_TryRemoveAtContext_FailsForEmptyContextWithoutStaging()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), maxStack: 99, apple);
        inventory.Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.For(inventory);

        var accepted = builder.TryRemoveAtContext(
            SlotLayoutContext<string>.Single(1),
            out var failure,
            amount: 1);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Layout));
        Assert.That(builder.IsEmpty, Is.True);
        Assert.That(inventory.Count(apple), Is.EqualTo(5));
    }

    [Test]
    public void ManualTransactionBuilder_ContextConstrainedDefinitionRemoval_IgnoresMatchingItemsElsewhere()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), maxStack: 99, apple);
        inventory.Add(apple, amount: 2, context: SlotLayoutContext<string>.Single(0));
        inventory.Add(apple, amount: 10, context: SlotLayoutContext<string>.Single(1));
        var builder = InventoryTransaction<string>.For(inventory);

        var accepted = builder.TryRemove(
            "apple",
            amount: 3,
            context: SlotLayoutContext<string>.Single(0),
            out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Layout));
        Assert.That(builder.IsEmpty, Is.True);
        Assert.That(inventory.Count(apple), Is.EqualTo(12));
    }

    [Test]
    public void ManualTransactionBuilder_ContextRemovalSupportsGridAndSectionContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var gridInventory = CreateInventory(new GridLayout<string>(2, 2), maxStack: 99, apple);
        gridInventory.Add(apple, amount: 4, context: GridLayoutContext<string>.Single(1, 1));
        var gridBuilder = InventoryTransaction<string>.For(gridInventory);

        Assert.That(gridBuilder.TryRemoveAtContext(GridLayoutContext<string>.Single(1, 1), out var failure, amount: 2), Is.True, failure?.ToString());
        Assert.That(gridBuilder.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(gridInventory.GetItemAt(GridLayoutContext<string>.Single(1, 1))!.Amount, Is.EqualTo(2));

        var sectionInventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            maxStack: 99,
            apple);
        sectionInventory.Add(apple, amount: 4, context: SectionedLayoutContext<string>.Single("bag", 1));
        var sectionBuilder = InventoryTransaction<string>.For(sectionInventory);

        Assert.That(sectionBuilder.TryRemoveAtContext(SectionedLayoutContext<string>.Single("bag", 1), out failure, amount: 2), Is.True, failure?.ToString());
        Assert.That(sectionBuilder.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(sectionInventory.GetItemAt(SectionedLayoutContext<string>.Single("bag", 1))!.Amount, Is.EqualTo(2));
    }

    [Test]
    public void ManualTransactionBuilder_MetadataRemovalModesMatchDeltaSemantics()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(4), maxStack: 99, apple);
        var ripe = new InstanceMetadata();
        ripe.Set("quality", "ripe");
        inventory.Add(apple, amount: 3, context: SlotLayoutContext<string>.Single(0));
        Assert.That(InventoryTransaction<string>
            .For(inventory)
            .Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(1), metadata: ripe)
            .TryCommit(out var setupFailure), Is.True, setupFailure?.ToString());

        var exactEmpty = InventoryTransaction<string>.For(inventory);
        Assert.That(exactEmpty.TryRemove("apple", amount: 2, context: null, out var failure), Is.True, failure?.ToString());
        Assert.That(exactEmpty.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(1));
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(5));

        var exactMetadata = InventoryTransaction<string>.For(inventory);
        Assert.That(exactMetadata.TryRemove("apple", amount: 2, metadata: ripe, context: SlotLayoutContext<string>.Single(1), out failure), Is.True, failure?.ToString());
        Assert.That(exactMetadata.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(1))!.Amount, Is.EqualTo(3));

        var wildcard = InventoryTransaction<string>.For(inventory);
        Assert.That(wildcard.TryRemove("apple", amount: 3, metadataMatch: ItemMetadataMatch.Any, context: null, out failure), Is.True, failure?.ToString());
        Assert.That(wildcard.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(inventory.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void ManualTransactionBuilder_ContextRemovalUsesSimulatedState()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), maxStack: 99, apple);
        inventory.Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.For(inventory);

        Assert.That(builder.TryRemoveAtContext(SlotLayoutContext<string>.Single(0), out var failure, amount: 5), Is.True, failure?.ToString());
        Assert.That(builder.TryAdd("apple", out failure, amount: 2, context: SlotLayoutContext<string>.Single(0)), Is.True, failure?.ToString());
        Assert.That(builder.TryRemoveAtContext(SlotLayoutContext<string>.Single(0), out failure, amount: 1), Is.True, failure?.ToString());
        Assert.That(builder.TryCommit(out failure), Is.True, failure?.ToString());

        Assert.That(inventory.GetItemAt(SlotLayoutContext<string>.Single(0))!.Amount, Is.EqualTo(1));
    }

    [Test]
    public void ManualTransactionBuilder_StaleInventoryVersionStillRejectsCommit()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), maxStack: 99, apple);
        inventory.Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.For(inventory);
        Assert.That(builder.TryRemoveAtContext(SlotLayoutContext<string>.Single(0), out var failure, amount: 2), Is.True, failure?.ToString());

        inventory.Add(apple, amount: 1, context: SlotLayoutContext<string>.Single(1));

        Assert.That(builder.TryCommit(out failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(inventory.Count(apple), Is.EqualTo(6));
    }

    [Test]
    public void CrossInventoryManualTransaction_CanStageFromAndToSides()
    {
        var apple = new ItemDefinition<string>("apple");
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new SlotLayout<string>(3), maxStack: 99, apple, coin);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(coin, amount: 10, context: SlotLayoutContext<string>.Single(0));

        var transaction = InventoryTransaction<string>.From(player).To(npc);
        var fromReturned = transaction.FromSide.Remove("coin", amount: 4, context: SlotLayoutContext<string>.Single(0));
        var toReturned = transaction.ToSide.Add("coin", amount: 4, context: SlotLayoutContext<string>.Single(2));

        Assert.That(fromReturned, Is.SameAs(transaction.FromSide));
        Assert.That(toReturned, Is.SameAs(transaction.ToSide));
        Assert.That(transaction.TryCommit(out var failure), Is.True, failure?.ToString());
        Assert.That(player.Count(coin), Is.EqualTo(6));
        Assert.That(npc.GetItemAt(SlotLayoutContext<string>.Single(2))!.Amount, Is.EqualTo(4));
    }

    [Test]
    public void CrossInventoryManualTransaction_SourceContextRemovalIsStrict()
    {
        var coin = new ItemDefinition<string>("coin");
        var manager = CreateManager(new SlotLayout<string>(2), maxStack: 99, coin);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(coin, amount: 2, context: SlotLayoutContext<string>.Single(0));
        player.Add(coin, amount: 10, context: SlotLayoutContext<string>.Single(1));
        var transaction = InventoryTransaction<string>.From(player).To(npc);

        var accepted = transaction.FromSide.TryRemove(
            "coin",
            amount: 3,
            context: SlotLayoutContext<string>.Single(0),
            out var failure);

        Assert.That(accepted, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Layout));
        Assert.That(transaction.FromSide.IsEmpty, Is.True);
        Assert.That(player.Count(coin), Is.EqualTo(12));
        Assert.That(npc.Count(coin), Is.Zero);
    }

    [Test]
    public void CrossInventoryManualTransaction_MetadataModesMatchLocalBuilder()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new SlotLayout<string>(4), maxStack: 99, apple);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        var ripe = new InstanceMetadata();
        ripe.Set("quality", "ripe");
        player.Add(apple, amount: 3, context: SlotLayoutContext<string>.Single(0));
        Assert.That(InventoryTransaction<string>
            .For(player)
            .Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(1), metadata: ripe)
            .TryCommit(out var setupFailure), Is.True, setupFailure?.ToString());
        var transaction = InventoryTransaction<string>.From(player).To(npc);

        Assert.That(transaction.FromSide.TryRemove("apple", amount: 2, context: null, out var failure), Is.True, failure?.ToString());
        Assert.That(transaction.FromSide.TryRemove("apple", amount: 2, metadata: ripe, context: SlotLayoutContext<string>.Single(1), out failure), Is.True, failure?.ToString());
        Assert.That(transaction.FromSide.TryRemove("apple", amount: 3, metadataMatch: ItemMetadataMatch.Any, context: null, out failure), Is.True, failure?.ToString());
        Assert.That(transaction.TryCommit(out failure), Is.True, failure?.ToString());

        Assert.That(player.Count(apple), Is.EqualTo(1));
        Assert.That(npc.Count(apple), Is.Zero);
    }

    [Test]
    public void CrossInventoryManualTransaction_FailedSecondSideCommitLeavesBothUnchanged()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new SlotLayout<string>(1), maxStack: 99, apple);
        var player = manager.CreateInventory();
        var npc = manager.CreateInventory();
        player.Add(apple, amount: 5, context: SlotLayoutContext<string>.Single(0));
        var transaction = InventoryTransaction<string>.From(player).To(npc);
        transaction.FromSide.Remove("apple", amount: 2);
        transaction.ToSide.Add("apple", amount: 2, context: SlotLayoutContext<string>.Single(0));
        npc.Add(apple, amount: 99, context: SlotLayoutContext<string>.Single(0));

        var committed = transaction.TryCommit(out var failure);

        Assert.That(committed, Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(player.Count(apple), Is.EqualTo(5));
        Assert.That(npc.Count(apple), Is.EqualTo(99));
    }

    [Test]
    public void CrossInventoryManualTransaction_RejectsStaleSourceOrTarget()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, apple);
        var sourceStale = manager.CreateInventory();
        var sourceTarget = manager.CreateInventory();
        sourceStale.Add(apple, amount: 5);
        var sourceTransaction = InventoryTransaction<string>.From(sourceStale).To(sourceTarget);
        sourceTransaction.FromSide.Remove("apple", amount: 1);
        sourceStale.Add(apple, amount: 1);

        Assert.That(sourceTransaction.TryCommit(out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(sourceStale.Count(apple), Is.EqualTo(6));
        Assert.That(sourceTarget.Count(apple), Is.Zero);

        var targetSource = manager.CreateInventory();
        var targetStale = manager.CreateInventory();
        targetSource.Add(apple, amount: 5);
        var targetTransaction = InventoryTransaction<string>.From(targetSource).To(targetStale);
        targetTransaction.FromSide.Remove("apple", amount: 1);
        targetTransaction.ToSide.Add("apple", amount: 1);
        targetStale.Add(apple, amount: 1);

        Assert.That(targetTransaction.TryCommit(out failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
        Assert.That(targetSource.Count(apple), Is.EqualTo(5));
        Assert.That(targetStale.Count(apple), Is.EqualTo(1));
    }

    [Test]
    public void CrossInventoryManualTransaction_RejectsEmptyButAllowsOneSidedWork()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, apple);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();

        var empty = InventoryTransaction<string>.From(source).To(target);
        Assert.That(empty.TryCommit(out var failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));

        var oneSided = InventoryTransaction<string>.From(source).To(target);
        oneSided.FromSide.Add("apple", amount: 2);
        Assert.That(oneSided.TryCommit(out failure), Is.True, failure?.ToString());
        Assert.That(source.Count(apple), Is.EqualTo(2));
        Assert.That(target.Count(apple), Is.Zero);
    }

    [Test]
    public void CrossInventoryManualTransaction_DoesNotMixWithDeltaApplication()
    {
        var apple = new ItemDefinition<string>("apple");
        var manager = CreateManager(new EntryLayout<string>(), maxStack: 99, apple);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.Add(apple, amount: 2);
        var delta = InventoryItemDelta<string>.Create().Remove("apple", amount: 1);

        var manualFirst = InventoryTransaction<string>.From(source).To(target);
        Assert.That(manualFirst.FromSide.TryRemove("apple", amount: 1, context: null, out var failure), Is.True, failure?.ToString());
        Assert.That(manualFirst.TryApply(delta, InventoryItemDelta<string>.Mirror(delta), out failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));

        var deltaFirst = InventoryTransaction<string>.From(source).To(target);
        Assert.That(deltaFirst.TryApply(delta, InventoryItemDelta<string>.Mirror(delta), out failure), Is.True, failure?.ToString());
        Assert.That(deltaFirst.FromSide.TryRemove("apple", amount: 1, context: null, out failure), Is.False);
        Assert.That(failure?.Kind, Is.EqualTo(InventoryFailureKind.Transaction));
    }

    [Test]
    public void CrossInventoryManualTransaction_SideAccessorsRejectInvalidTransactionModes()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), maxStack: 99, apple);
        var local = InventoryTransaction<string>.For(inventory).Add("apple", amount: 1).Build();
        var incomplete = InventoryTransaction<string>.From(
            new InventoryTransactionEntry<string>(inventory, InventoryItemDelta<string>.Create().Add("apple", amount: 1)));

        Assert.Throws<InvalidOperationException>(() => _ = local.FromSide);
        Assert.Throws<InvalidOperationException>(() => _ = local.ToSide);
        Assert.Throws<InvalidOperationException>(() => _ = incomplete.FromSide);
        Assert.Throws<InvalidOperationException>(() => _ = incomplete.ToSide);
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
