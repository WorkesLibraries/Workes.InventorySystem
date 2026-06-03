using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventoryChangedEventTests
{
    [Test]
    public void InventoryChangedEventArgs_CollectionsAreReadOnlyFacingAndDefaultToEmpty()
    {
        var args = new InventoryChangedEventArgs<string>(cleared: true);

        Assert.That(typeof(InventoryChangedEventArgs<string>).GetProperty(nameof(InventoryChangedEventArgs<string>.Added))!.PropertyType, Is.EqualTo(typeof(IReadOnlyList<ItemAdded<string>>)));
        Assert.That(typeof(InventoryChangedEventArgs<string>).GetProperty(nameof(InventoryChangedEventArgs<string>.Added))!.CanWrite, Is.False);
        Assert.That(args.Added, Is.Empty);
        Assert.That(args.Removed, Is.Empty);
        Assert.That(args.Modified, Is.Empty);
        Assert.That(args.Moved, Is.Empty);
        Assert.That(args.Swapped, Is.Empty);
        Assert.That(args.MetadataChanged, Is.Empty);
        Assert.That(args.ConfigurationChanged, Is.Empty);
        Assert.That(args.Cleared, Is.True);
    }

    [Test]
    public void TryAdd_WithSlotLayout_FiresAddedEventWithLayoutContext()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), apple);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryAdd(apple, out var error, 1, SlotLayoutContext<string>.Single(2));

        Assert.That(result, Is.True, error);
        var added = captured!.Added.Single();
        Assert.That(added.Index, Is.EqualTo(0));
        Assert.That(((SlotLayoutContext<string>)added.LayoutContext!).SlotIndex, Is.EqualTo(2));
    }

    [Test]
    public void TryAdd_WithEntryLayout_FiresAddedEventWithLayoutContext()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), new UnlimitedCapacityPolicy<string>(), apple);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryAdd(apple, out var error);

        Assert.That(result, Is.True, error);
        var added = captured!.Added.Single();
        Assert.That(added.Index, Is.EqualTo(0));
        Assert.That(((EntryLayoutContext<string>)added.LayoutContext!).TargetIndex, Is.EqualTo(0));
    }

    [Test]
    public void TryRemove_FullStack_FiresRemovedEventWithPreRemovalLayoutContext()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), apple);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryRemove(inventory.Items[0], out var error);

        Assert.That(result, Is.True, error);
        var removed = captured!.Removed.Single();
        Assert.That(removed.Index, Is.EqualTo(0));
        Assert.That(((SlotLayoutContext<string>)removed.LayoutContext!).SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void TryAdd_MergingStack_FiresModifiedEventWithBeforeAndAfterAmounts()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), new UnlimitedCapacityPolicy<string>(), apple);
        inventory.TryAdd(apple, out _, 2, SlotLayoutContext<string>.Single(0));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryAdd(apple, out var error, 3, SlotLayoutContext<string>.Single(0));

        Assert.That(result, Is.True, error);
        var modified = captured!.Modified.Single();
        Assert.That(modified.BeforeAmount, Is.EqualTo(2));
        Assert.That(modified.AfterAmount, Is.EqualTo(5));
        Assert.That(((SlotLayoutContext<string>)modified.BeforeLayoutContext!).SlotIndex, Is.EqualTo(0));
        Assert.That(((SlotLayoutContext<string>)modified.AfterLayoutContext!).SlotIndex, Is.EqualTo(0));
    }

    [Test]
    public void TryRemove_PartialStack_FiresModifiedEventWithBeforeAndAfterAmounts()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), new UnlimitedCapacityPolicy<string>(), apple);
        inventory.TryAdd(apple, out _, 5);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryRemove(inventory.Items[0], out var error, 2);

        Assert.That(result, Is.True, error);
        var modified = captured!.Modified.Single();
        Assert.That(modified.BeforeAmount, Is.EqualTo(5));
        Assert.That(modified.AfterAmount, Is.EqualTo(3));
    }

    [Test]
    public void CommitTransaction_ModifiedEventResolvesAfterContextAfterRemovalShiftsLayout()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new EntryLayout<string>(), new UnlimitedCapacityPolicy<string>(), apple, berry);
        inventory.TryAdd(apple, out _, 1);
        inventory.TryAdd(berry, out _, 2);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(berry, out _, 1);
        builder.TryRemove(inventory.Items[0], out _, 1);

        inventory.CommitTransaction(builder.ToInventoryTransaction());

        var modified = captured!.Modified.Single();
        Assert.That(modified.Index, Is.EqualTo(1));
        Assert.That(((EntryLayoutContext<string>)modified.BeforeLayoutContext!).TargetIndex, Is.EqualTo(1));
        Assert.That(((EntryLayoutContext<string>)modified.AfterLayoutContext!).TargetIndex, Is.EqualTo(0));
    }

    [Test]
    public void TryMove_FiresMovedEventWithIsSortResultFalse()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), apple);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TryMove(SlotLayoutContext<string>.Single(0), SlotLayoutContext<string>.Single(1), out var error);

        Assert.That(result, Is.True, error);
        Assert.That(captured!.Moved.Single().IsSortResult, Is.False);
    }

    [Test]
    public void TrySortLayout_FiresMovedEventsWithIsSortResultTrue()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), berry, apple);
        inventory.TryAdd(berry, out _, 1, SlotLayoutContext<string>.Single(0));
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error);

        Assert.That(result, Is.True, error);
        Assert.That(captured!.Moved, Is.Not.Empty);
        Assert.That(captured.Moved.All(move => move.IsSortResult), Is.True);
    }

    [Test]
    public void InventoryChangedEventArgs_AffectedContextsStillIncludeSortMovedContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), berry, apple);
        inventory.TryAdd(berry, out _, 1, SlotLayoutContext<string>.Single(0));
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        var result = inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var error);

        Assert.That(result, Is.True, error);
        var movedContexts = captured!.Moved
            .SelectMany(move => move.FromLayoutContexts.Concat(move.ToLayoutContexts))
            .ToList();
        foreach (var context in movedContexts)
            Assert.That(captured.AffectedLayoutContexts, Does.Contain(context));
    }

    [Test]
    public void FailedCapacityValidation_FiresNoChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), new MaxTotalItemAmountCapacityPolicy<string>(1), apple);
        inventory.TryAdd(apple, out _, 1);
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        var result = inventory.TryAdd(apple, out var error, 1);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Capacity exceeded."));
        Assert.That(changed, Is.EqualTo(0));
    }

    [Test]
    public void FailedLayoutValidation_FiresNoChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(2), new UnlimitedCapacityPolicy<string>(), apple, berry);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(berry, out _, 1);
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        var result = inventory.TryCommitTransaction(builder.ToInventoryTransaction(), SlotLayoutContext<string>.Single(0), out var error);

        Assert.That(result, Is.False);
        Assert.That(error, Is.EqualTo("Slot already occupied."));
        Assert.That(changed, Is.EqualTo(0));
    }

    [Test]
    public void SuccessfulTransfer_FiresOneEnrichedEventPerInventory()
    {
        var catalog = new ItemCatalog<string>();
        var apple = new ItemDefinition<string>("apple");
        catalog.Registry.Register(apple);
        catalog.Freeze();
        var manager = CreateManager(new SlotLayout<string>(2), new UnlimitedCapacityPolicy<string>(), catalog);
        var source = manager.CreateInventory();
        var target = manager.CreateInventory();
        source.TryAdd(apple, out _, 2, SlotLayoutContext<string>.Single(0));
        InventoryChangedEventArgs<string>? sourceEvent = null;
        InventoryChangedEventArgs<string>? targetEvent = null;
        source.Changed += (_, e) => sourceEvent = e;
        target.Changed += (_, e) => targetEvent = e;

        var result = InventoryTransfer.TryTransfer(source, target, source.Items[0], 1, SlotLayoutContext<string>.Single(1), out var error);

        Assert.That(result, Is.True, error);
        Assert.That(sourceEvent!.Modified.Single().AfterAmount, Is.EqualTo(1));
        Assert.That(((SlotLayoutContext<string>)sourceEvent.Modified.Single().AfterLayoutContext!).SlotIndex, Is.EqualTo(0));
        Assert.That(targetEvent!.Added.Single().Instance.Amount, Is.EqualTo(1));
        Assert.That(((SlotLayoutContext<string>)targetEvent.Added.Single().LayoutContext!).SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void ReplaceContents_NonEmptyInventory_FiresSingleChangedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new EntryLayout<string>(), new UnlimitedCapacityPolicy<string>(), apple, berry);
        inventory.TryAdd(apple, out _);
        int changed = 0;
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) =>
        {
            changed++;
            captured = e;
        };

        inventory.ReplaceContents(new[] { (berry, 2, (ILayoutContext<string>?)EntryLayoutContext<string>.Single(0)) });

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(captured!.Cleared, Is.True);
        Assert.That(captured.Removed.Single().Instance.Definition.Id, Is.EqualTo("apple"));
        Assert.That(captured.Added.Single().Instance.Definition.Id, Is.EqualTo("berry"));
    }

    [Test]
    public void ReplaceContents_EventIncludesRemovedAndAddedLayoutContexts()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(3), new UnlimitedCapacityPolicy<string>(), apple, berry);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) => captured = e;

        inventory.ReplaceContents(new[] { (berry, 1, (ILayoutContext<string>?)SlotLayoutContext<string>.Single(1)) });

        Assert.That(((SlotLayoutContext<string>)captured!.Removed.Single().LayoutContext!).SlotIndex, Is.EqualTo(0));
        Assert.That(((SlotLayoutContext<string>)captured.Added.Single().LayoutContext!).SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void ReplaceContents_EmptyWithEmptyReplacement_FiresNoEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), new UnlimitedCapacityPolicy<string>(), apple);
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        inventory.ReplaceContents(null);

        Assert.That(changed, Is.EqualTo(0));
        Assert.That(inventory.InstanceCount, Is.EqualTo(0));
    }

    [Test]
    public void ReplaceContents_InvalidReplacementLeavesOriginalInventoryUnchanged()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(2), new UnlimitedCapacityPolicy<string>(), apple, berry);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        int changed = 0;
        inventory.Changed += (_, _) => changed++;

        Assert.Throws<InvalidOperationException>(() =>
            inventory.ReplaceContents(new[] { (berry, 1, (ILayoutContext<string>?)SlotLayoutContext<string>.Single(9)) }));

        Assert.That(changed, Is.EqualTo(0));
        Assert.That(inventory.InstanceCount, Is.EqualTo(1));
        Assert.That(inventory.Items[0].Definition.Id, Is.EqualTo("apple"));
        Assert.That(inventory.Layout.GetItemAt(inventory, SlotLayoutContext<string>.Single(0))!.Definition.Id, Is.EqualTo("apple"));
    }

    [Test]
    public void ReplaceContents_EmptyReplacementFromNonEmptyInventory_FiresSingleClearedEvent()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new SlotLayout<string>(2), new UnlimitedCapacityPolicy<string>(), apple);
        inventory.TryAdd(apple, out _, 1, SlotLayoutContext<string>.Single(0));
        int changed = 0;
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, e) =>
        {
            changed++;
            captured = e;
        };

        inventory.ReplaceContents(null);

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(captured!.Cleared, Is.True);
        Assert.That(captured.Removed.Single().Instance.Definition.Id, Is.EqualTo("apple"));
        Assert.That(captured.Added, Is.Empty);
        Assert.That(inventory.InstanceCount, Is.EqualTo(0));
    }

    private static Inventory<string> CreateInventory(IInventoryLayout<string> layout, ICapacityPolicy<string> capacityPolicy, params ItemDefinition<string>[] definitions)
    {
        var manager = CreateManager(layout, capacityPolicy);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static InventoryManager<string> CreateManager(
        IInventoryLayout<string> layout,
        ICapacityPolicy<string> capacityPolicy,
        ItemCatalog<string>? catalog = null)
    {
        return new InventoryManager<string>(
            new DefaultStackResolver<string>(10),
            capacityPolicy,
            layout,
            catalog: catalog);
    }
}


