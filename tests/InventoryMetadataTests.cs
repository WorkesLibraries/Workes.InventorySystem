using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryMetadataTests
{
    [Test]
    public void DetachedOperations_ProvideTryThrowUpdateAndNoOpParity()
    {
        var metadata = new InventoryMetadata();

        Assert.That(metadata.TryAdd("level", 1, out var error), Is.True);
        Assert.That(metadata.TryAdd("level", 2, out error), Is.False);
        Assert.Throws<InventoryOperationException>(() => metadata.Add("level", 2));
        Assert.That(metadata.TryUpdate<int>("level", value => value + 1, out error), Is.True);
        Assert.That(metadata.TryGet<int>("level", out var level), Is.True);
        Assert.That(level, Is.EqualTo(2));
        Assert.That(metadata.TryUpdate<string>("level", value => value, out error), Is.False);
        Assert.That(metadata.TryUpdate<int>("missing", value => value, out error), Is.False);
        Assert.That(metadata.TrySet("nothing", null, out error), Is.True);
        Assert.That(metadata.TryGet<string?>("nothing", out var nothing), Is.True);
        Assert.That(nothing, Is.Null);
        Assert.That(metadata.TryGet<int>("nothing", out _), Is.False);
        Assert.That(metadata.TryRemove("missing", out error), Is.False);
    }

    [Test]
    public void CollectionsAreDeeplyDetachedAtEveryPublicBoundary()
    {
        var metadata = new InventoryMetadata();
        var source = new List<int[]> { new[] { 1, 2 } };
        metadata.Set("values", source);
        source[0][0] = 99;
        source.Add(new[] { 3 });

        Assert.That(metadata.TryGet<List<int[]>>("values", out var first), Is.True);
        Assert.That(first, Has.Count.EqualTo(1));
        Assert.That(first[0], Is.EqualTo(new[] { 1, 2 }));
        first[0][0] = 88;
        first.Clear();

        var readOnly = metadata.AsReadOnly();
        Assert.That(readOnly, Is.TypeOf<ReadOnlyDictionary<string, object?>>());
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)readOnly).Add("other", 1));
        ((List<int[]>)readOnly["values"]!)[0][0] = 77;

        Assert.That(metadata.TryGet<List<int[]>>("values", out var final), Is.True);
        Assert.That(final[0], Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void UnsupportedValuesAndCyclesAreRejectedAtomically()
    {
        var metadata = new InventoryMetadata();
        metadata.Set("kept", 1);
        var dictionary = new Dictionary<string, int> { ["value"] = 2 };
        var cycle = new List<object?>();
        cycle.Add(cycle);

        Assert.That(metadata.TrySet("unsupported", dictionary, out var error), Is.False);
        Assert.That(error?.Message, Does.Contain("not a supported portable snapshot value"));
        Assert.That(metadata.TrySet("cycle", cycle, out error), Is.False);
        Assert.That(error?.Message, Does.Contain("cycle"));
        Assert.That(metadata.TryGet<int>("kept", out var kept), Is.True);
        Assert.That(kept, Is.EqualTo(1));
        Assert.That(metadata.TryReplace(
            new Dictionary<string, object?> { ["valid"] = 2, ["invalid"] = DayOfWeek.Monday },
            out error), Is.False);
        Assert.That(metadata.TryGet<int>("kept", out kept), Is.True);
        Assert.That(kept, Is.EqualTo(1));
    }

    [Test]
    public void TransformAndUpdatePropagateUnexpectedCallbackExceptionsAtomically()
    {
        var metadata = new InventoryMetadata();
        metadata.Set("level", 1);

        var transform = Assert.Throws<InvalidOperationException>(() =>
            metadata.TryTransform(_ => throw new InvalidOperationException("transform failed"), out _));
        var update = Assert.Throws<InvalidOperationException>(() =>
            metadata.TryUpdate<int>("level", _ => throw new InvalidOperationException("update failed"), out _));

        Assert.That(transform!.Message, Is.EqualTo("transform failed"));
        Assert.That(update!.Message, Is.EqualTo("update failed"));
        Assert.That(metadata.TryGet<int>("level", out var level), Is.True);
        Assert.That(level, Is.EqualTo(1));
    }

    [Test]
    public void InventoryOwnedMutationKeepsStableObjectAndEmitsDetachedSortedChange()
    {
        var inventory = CreateInventory();
        var stable = inventory.Metadata;
        var events = new List<InventoryChangedEventArgs<string>>();
        inventory.Changed += (_, args) => events.Add(args);

        inventory.Metadata.Replace(new Dictionary<string, object?>
        {
            ["z"] = new List<int> { 1, 2 },
            ["a"] = "owner"
        });

        Assert.That(inventory.Metadata, Is.SameAs(stable));
        Assert.That(events, Has.Count.EqualTo(1));
        var change = events[0].InventoryMetadataChanged!;
        Assert.That(change.ChangedKeys, Is.EqualTo(new[] { "a", "z" }));
        Assert.That(events[0].Origin, Is.EqualTo(InventoryChangeOrigin.Operation));
        Assert.That(events[0].RequiresFullRefresh, Is.False);

        ((List<int>)change.AfterMetadata["z"]!)[0] = 99;
        inventory.Metadata.Update<List<int>>("z", values =>
        {
            values.Add(3);
            return values;
        });

        Assert.That((List<int>)change.AfterMetadata["z"]!, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(inventory.Metadata.TryGet<List<int>>("z", out var current), Is.True);
        Assert.That(current, Is.EqualTo(new[] { 1, 2, 3 }));
        inventory.Metadata.Set("a", "owner");
        Assert.That(events, Has.Count.EqualTo(2), "A structural no-op must not emit an event.");
    }

    [Test]
    public void ItemMetadataEventSnapshotsAreDeeplyDetachedAndRetainHistory()
    {
        var item = new ItemDefinition<string>("item");
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>(),
            new RuleContainer<string>());
        manager.Registry.Register(item);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        inventory.Add(item);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Items[0].Metadata.Set("traits", new List<string> { "rare" });
        var change = captured!.MetadataChanged.Single();
        ((List<string>)change.AfterMetadata["traits"]!).Add("mutated");

        Assert.That(
            (List<string>)change.AfterMetadata["traits"]!,
            Is.EqualTo(new[] { "rare" }));
        Assert.That(inventory.Items[0].Metadata.TryGet<List<string>>("traits", out var stored), Is.True);
        Assert.That(stored, Is.EqualTo(new[] { "rare" }));
    }

    [Test]
    public void InventoryAttributesIsAbsentWhileDefinitionAttributesRemain()
    {
        Assert.That(typeof(Inventory<string>).GetProperty("Attributes"), Is.Null);
        Assert.That(typeof(ItemDefinition<string>).GetProperty("Attributes"), Is.Not.Null);
        Assert.That(typeof(ItemCatalog<string>).GetProperty("Attributes"), Is.Not.Null);
    }

    [Test]
    public void InventoryMutationValidationObservesProposedRootMetadata()
    {
        var item = new ItemDefinition<string>("item");
        var rules = new RuleContainer<string>();
        rules.Add("metadata-rule", new RejectMetadataFlagRule());
        var manager = new InventoryManager<string>(
            new RootMetadataStackResolver(),
            new RejectMetadataFlagCapacity(),
            new EntryLayout<string>(),
            new ItemCatalog<string>(),
            rules);
        manager.Registry.Register(item);
        manager.Catalog.Freeze();
        var inventory = manager.CreateInventory();
        inventory.Add(item, 5);

        Assert.That(inventory.Metadata.TrySet("maxStack", 2, out var stackError), Is.False);
        Assert.That(stackError?.Message, Does.Contain("max stack size"));
        Assert.That(inventory.Metadata.TrySet("blockCapacity", true, out var capacityError), Is.False);
        Assert.That(capacityError?.Message, Does.Contain("capacity"));
        Assert.That(inventory.Metadata.TrySet("blockRule", true, out var ruleError), Is.False);
        Assert.That(ruleError?.Message, Does.Contain("rule"));
        Assert.That(inventory.Metadata.IsEmpty, Is.True);
    }

    private static Inventory<string> CreateInventory()
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>(),
            new RuleContainer<string>());
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private sealed class RootMetadataStackResolver : IStackResolver<string>
    {
        public int ResolveMaxStackSize(Inventory<string> inventory, ItemInstance<string> instance) =>
            inventory.Metadata.TryGet<int>("maxStack", out var size) ? size : 10;
    }

    private sealed class RejectMetadataFlagCapacity : ICapacityPolicy<string>
    {
        public bool CanApply(
            Inventory<string> inventory,
            NormalizedInventoryTransaction<string> normalizedTransaction,
            out InventoryFailure? error)
        {
            if (inventory.Metadata.TryGet<bool>("blockCapacity", out var blocked) && blocked)
            {
                error = "Root metadata requested capacity rejection.";
                return false;
            }
            error = null;
            return true;
        }

        public bool CanAdd(
            Inventory<string> inventory,
            ItemInstance<string> instance,
            out InventoryFailure? error)
        {
            if (inventory.Metadata.TryGet<bool>("blockCapacity", out var blocked) && blocked)
            {
                error = "Root metadata requested capacity rejection.";
                return false;
            }
            error = null;
            return true;
        }
    }

    private sealed class RejectMetadataFlagRule : IRulePolicy<string>
    {
        public string Id => "metadata-rule";

        public bool CanApply(
            Inventory<string> inventory,
            NormalizedInventoryTransaction<string> transaction,
            out InventoryFailure? error)
        {
            if (inventory.Metadata.TryGet<bool>("blockRule", out var blocked) && blocked)
            {
                error = "Root metadata requested rule rejection.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
