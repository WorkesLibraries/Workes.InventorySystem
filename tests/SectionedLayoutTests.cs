using System;
using System.Linq;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Sorting;
using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class SectionedLayoutTests
{
    [Test]
    public void Constructor_RejectsNullSections()
    {
        Assert.Throws<ArgumentNullException>(() => new SectionedLayout<string>((System.Collections.Generic.IEnumerable<SectionDefinition<string>>)null!));
    }

    [Test]
    public void Constructor_RejectsEmptySections()
    {
        Assert.Throws<ArgumentException>(() => new SectionedLayout<string>(Array.Empty<SectionDefinition<string>>()));
    }

    [Test]
    public void Constructor_RejectsDuplicateSectionIds()
    {
        Assert.Throws<ArgumentException>(() => new SectionedLayout<string>(
            new SectionDefinition<string>("bag", 1),
            new SectionDefinition<string>("bag", 1)));
    }

    [Test]
    public void SectionDefinition_RejectsInvalidId()
    {
        Assert.Throws<ArgumentException>(() => new SectionDefinition<string>(" ", 1));
    }

    [Test]
    public void SectionDefinition_RejectsNonPositiveSlotCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SectionDefinition<string>("bag", 0));
    }

    [Test]
    public void SectionDefinition_OptionsExposeAllowedDefinitionIds()
    {
        var sword = new ItemDefinition<string>("sword");
        var section = new SectionDefinition<string>(
            "weapons",
            2,
            new SectionDefinitionOptions<string>
            {
                AllowedDefinitionIds = new[] { "sword", "sword" },
                AllowedDefinitions = new[] { sword, null! }
            });

        Assert.That(section.AllowedDefinitionIds, Is.EqualTo(new[] { "sword" }));
    }

    [Test]
    public void SectionDefinition_OptionsRejectNullAllowedDefinitionId()
    {
        Assert.Throws<ArgumentException>(() => new SectionDefinition<string>(
            "weapons",
            1,
            new SectionDefinitionOptions<string>
            {
                AllowedDefinitionIds = new[] { (string)null! }
            }));
    }

    [Test]
    public void SectionDefinition_OptionsIgnoreNullAllowedDefinition()
    {
        var section = new SectionDefinition<string>(
            "weapons",
            1,
            new SectionDefinitionOptions<string>
            {
                AllowedDefinitions = new[] { (ItemDefinition<string>)null! }
            });

        Assert.That(section.AllowedDefinitionIds, Is.Empty);
    }

    [Test]
    public void GetAddressableContexts_ReturnsSectionOrderThenSlotOrder()
    {
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 2),
                new SectionDefinition<string>("bag", 1)));

        var contexts = inventory.Layout.GetAddressableContexts(inventory)
            .Cast<SectionedLayoutContext<string>>()
            .Select(c => (c.SectionId, c.SlotIndex))
            .ToList();

        Assert.That(inventory.Layout.GetPositionCount(inventory), Is.EqualTo(3));
        Assert.That(contexts, Is.EqualTo(new[] { ("hotbar", 0), ("hotbar", 1), ("bag", 0) }));
    }

    [Test]
    public void TryAdd_WithSingleContext_PlacesItemInSectionSlot()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: apple);

        Assert.That(inventory.TryAdd(apple, out var failure, 1, SectionedLayoutContext<string>.Single("bag", 1)), Is.True);

        Assert.That(ItemAt(inventory, "bag", 1), Is.EqualTo("apple"));
        Assert.That(inventory.Layout.TryGetContextForStorageIndex(inventory, 0, out var context), Is.True);
        var sectionContext = (SectionedLayoutContext<string>)context!;
        Assert.That((sectionContext.SectionId, sectionContext.SlotIndex), Is.EqualTo(("bag", 1)));
    }

    [Test]
    public void NullContext_SkipsIncompatibleSections()
    {
        var weapon = "gear:weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("potions", 1, "gear:potion"),
                new SectionDefinition<string>("weapons", 1, weapon)),
            tags: new[] { weapon, "gear:potion" },
            definitions: sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);

        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
        Assert.That(ItemAt(inventory, "potions", 0), Is.Null);
    }

    [Test]
    public void SectionedLayout_AcceptsItemByAllowedDefinition()
    {
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>(
                    "weapons",
                    1,
                    new SectionDefinitionOptions<string> { AllowedDefinitionIds = new[] { "sword" } })),
            definitions: sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void SectionedLayout_RejectsItemWhenDefinitionNotAllowed()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>(
                    "weapons",
                    1,
                    new SectionDefinitionOptions<string> { AllowedDefinitions = new[] { sword } })),
            definitions: new ItemDefinition<string>[] { sword, apple });

        Assert.That(inventory.TryAdd(apple, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible section slot available."));
    }

    [Test]
    public void SectionedLayout_AllowsItemWhenEitherTagOrDefinitionMatches()
    {
        var weapon = "gear:weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var lockpick = new ItemDefinition<string>("lockpick");
        var options = new SectionDefinitionOptions<string>
        {
            RequiredTags = new[] { weapon },
            AllowedDefinitions = new[] { lockpick }
        };
        var swordInventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("tools", 1, options)),
            tags: new[] { weapon },
            definitions: new ItemDefinition<string>[] { sword, lockpick });
        var lockpickInventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("tools", 1, options)),
            tags: new[] { weapon },
            definitions: new ItemDefinition<string>[] { sword, lockpick });

        Assert.That(swordInventory.TryAdd(sword, out var swordError), Is.True, swordError?.Message);
        Assert.That(lockpickInventory.TryAdd(lockpick, out var lockpickError), Is.True, lockpickError?.Message);
    }

    [Test]
    public void SectionedLayout_WithNoTagsOrDefinitions_AllowsAnyDefinition()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 1)),
            definitions: apple);

        Assert.That(inventory.TryAdd(apple, out var failure), Is.True);
    }

    [Test]
    public void NullContext_RejectsWhenNoCompatibleSlotAvailable()
    {
        var weapon = "gear:weapon";
        var apple = new TaggedDefinition("apple", "gear:food");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("weapons", 1, weapon)),
            tags: new[] { weapon, "gear:food" },
            definitions: apple);

        Assert.That(inventory.TryAdd(apple, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible section slot available."));
    }

    [Test]
    public void SectionedLayout_MoveSwapAndSortRespectAllowedDefinitions()
    {
        var sword = new ItemDefinition<string>("sword");
        var helmet = new ItemDefinition<string>("helmet");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>(
                    "weapons",
                    2,
                    new SectionDefinitionOptions<string> { AllowedDefinitions = new[] { sword } }),
                new SectionDefinition<string>(
                    "armor",
                    2,
                    new SectionDefinitionOptions<string> { AllowedDefinitions = new[] { helmet } }),
                new SectionDefinition<string>("bag", 3)),
            definitions: new ItemDefinition<string>[] { sword, helmet, apple });
        inventory.Add(sword, context: SectionedLayoutContext<string>.Single("bag", 1));
        inventory.Add(helmet, context: SectionedLayoutContext<string>.Single("bag", 0));
        inventory.Add(apple, context: SectionedLayoutContext<string>.Single("bag", 2));

        Assert.That(inventory.TryMove(
            SectionedLayoutContext<string>.Single("bag", 1),
            SectionedLayoutContext<string>.Single("weapons", 1),
            out var moveError), Is.True);
        Assert.That(inventory.TrySwap(
            SectionedLayoutContext<string>.Single("weapons", 1),
            SectionedLayoutContext<string>.Single("bag", 0),
            out var swapError), Is.False);
        Assert.That(swapError?.Message, Is.EqualTo("No compatible section slot available."));

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var sortError), Is.True);
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
        Assert.That(ItemAt(inventory, "armor", 0), Is.EqualTo("helmet"));
        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("apple"));
    }

    [Test]
    public void SectionedLayout_ParameterMutationPreservesAllowedDefinitions()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>(
                    "weapons",
                    1,
                    new SectionDefinitionOptions<string> { AllowedDefinitions = new[] { sword } })),
            definitions: new ItemDefinition<string>[] { sword, apple });

        Assert.That(inventory.TrySetLayoutParameter("section:weapons.slotCount", 2, out var parameterError), Is.True);
        Assert.That(inventory.TryAdd(apple, out var addError, 1, SectionedLayoutContext<string>.Single("weapons", 1)), Is.False);
        Assert.That(addError?.Message, Is.EqualTo("No compatible section slot available."));
    }

    [Test]
    public void MappedContext_PlacesMultipleAddedEntries()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 2),
                new SectionDefinition<string>("bag", 2)),
            definitions: new ItemDefinition<string>[] { apple, sword });
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        builder.TryAdd(sword, out _);
        var context = SectionedLayoutContext<string>.Map()
            .Add(0, "bag", 1)
            .Add(1, "hotbar", 0)
            .Build();

        Assert.That(builder.TryBuild(context, out var transaction, out var failure), Is.True);
        Assert.That(inventory.TryCommitTransaction(transaction!, out failure), Is.True);

        Assert.That(ItemAt(inventory, "bag", 1), Is.EqualTo("apple"));
        Assert.That(ItemAt(inventory, "hotbar", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void MappedContext_RejectsMappedAddedIndexOutOfRange()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: apple);
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);

        Assert.That(builder.TryBuild(
            SectionedLayoutContext<string>.Map().Add(1, "bag", 0).Build(),
            out _,
            out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Mapped added entry index out of range."));
    }

    [Test]
    public void MappedContext_RejectsDuplicateTargetSectionSlot()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: new ItemDefinition<string>[] { apple, sword });
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, out _);
        builder.TryAdd(sword, out _);
        var context = SectionedLayoutContext<string>.Map()
            .Add(0, "bag", 0)
            .Add(1, "bag", 0)
            .Build();

        Assert.That(builder.TryBuild(context, out _, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Duplicate mapped target section slot."));
    }

    [Test]
    public void MappedContext_CanTargetSlotFreedBySameTransaction()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: new ItemDefinition<string>[] { apple, sword });
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("bag", 0));
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryRemoveAtStorageIndex(0, out _);
        builder.TryAdd(sword, out _);

        Assert.That(builder.TryBuild(
            SectionedLayoutContext<string>.Map().Add(0, "bag", 0).Build(),
            out var transaction,
            out var failure), Is.True);
        Assert.That(inventory.TryCommitTransaction(transaction!, out failure), Is.True);
        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void AmountDelta_DoesNotRequirePlacementMapping()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 1)),
            definitions: apple);
        inventory.TryAdd(apple, out _, 2, SectionedLayoutContext<string>.Single("bag", 0));

        Assert.That(inventory.TryAdd(apple, out var failure, 2), Is.True);
        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("apple"));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(4));
    }

    [Test]
    public void SectionRequiredTags_IncludeGeneratedParentTags()
    {
        var axeTag = "gear:tools.axe";
        var toolsTag = "gear:tools";
        var axe = new TaggedDefinition("axe", axeTag);
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("tools", 1, toolsTag)),
            tags: new[] { axeTag },
            definitions: axe);

        Assert.That(inventory.TryAdd(axe, out var failure), Is.True);
        Assert.That(ItemAt(inventory, "tools", 0), Is.EqualTo("axe"));
    }

    [Test]
    public void SectionedLayout_NonNamespacedMode_AcceptsItemsByRequiredTag()
    {
        var weapon = "gear.weapon";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateNonNamespacedInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("weapons", 1, weapon)),
            new[] { weapon },
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void SectionedLayout_NonNamespacedMode_AcceptsItemsByParentTag()
    {
        var sword = new TaggedDefinition("sword", "gear.weapon.sword");
        var inventory = CreateNonNamespacedInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("weapons", 1, "gear.weapon")),
            new[] { "gear.weapon.sword" },
            sword);

        Assert.That(inventory.TryAdd(sword, out var failure), Is.True);
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void TryMove_MovesToCompatibleEmptySectionSlot()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 1),
                new SectionDefinition<string>("bag", 1)),
            definitions: apple);
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("hotbar", 0));

        Assert.That(inventory.TryMove(
            SectionedLayoutContext<string>.Single("hotbar", 0),
            SectionedLayoutContext<string>.Single("bag", 0),
            out var failure), Is.True);

        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("apple"));
        Assert.That(ItemAt(inventory, "hotbar", 0), Is.Null);
    }

    [Test]
    public void TryMove_RejectsIncompatibleTargetAndFiresNoEvent()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("weapons", 1, weapon),
                new SectionDefinition<string>("armor", 1, armor)),
            tags: new[] { weapon, armor },
            definitions: sword);
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("weapons", 0));
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TryMove(
            SectionedLayoutContext<string>.Single("weapons", 0),
            SectionedLayoutContext<string>.Single("armor", 0),
            out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible section slot available."));
        Assert.That(events, Is.EqualTo(0));
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("sword"));
    }

    [Test]
    public void TrySwap_SwapsCompatibleSectionSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("left", 1),
                new SectionDefinition<string>("right", 1)),
            definitions: new ItemDefinition<string>[] { apple, sword });
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("left", 0));
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("right", 0));

        Assert.That(inventory.TrySwap(
            SectionedLayoutContext<string>.Single("left", 0),
            SectionedLayoutContext<string>.Single("right", 0),
            out var failure), Is.True);

        Assert.That(ItemAt(inventory, "left", 0), Is.EqualTo("sword"));
        Assert.That(ItemAt(inventory, "right", 0), Is.EqualTo("apple"));
    }

    [Test]
    public void TrySwap_RejectsIncompatibleResult()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var helmet = new TaggedDefinition("helmet", armor);
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("weapons", 1, weapon),
                new SectionDefinition<string>("armor", 1, armor)),
            tags: new[] { weapon, armor },
            definitions: new ItemDefinition<string>[] { sword, helmet });
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("weapons", 0));
        inventory.TryAdd(helmet, out _, 1, SectionedLayoutContext<string>.Single("armor", 0));

        Assert.That(inventory.TrySwap(
            SectionedLayoutContext<string>.Single("weapons", 0),
            SectionedLayoutContext<string>.Single("armor", 0),
            out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("No compatible section slot available."));
    }

    [Test]
    public void TrySort_SortsAndCompactsAcrossCompatibleSections()
    {
        var sword = new ItemDefinition<string>("sword");
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 2),
                new SectionDefinition<string>("bag", 2)),
            definitions: new ItemDefinition<string>[] { sword, apple });
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("bag", 1));
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("bag", 0));
        int events = 0;
        inventory.Changed += (_, args) =>
        {
            events++;
            Assert.That(args.Moved, Has.Count.EqualTo(2));
            Assert.That(args.AffectedLayoutContexts, Has.Count.EqualTo(4));
        };

        Assert.That(inventory.TrySortLayout((a, b) => string.CompareOrdinal(a.Definition.Id, b.Definition.Id), out var failure), Is.True);

        Assert.That(ItemAt(inventory, "hotbar", 0), Is.EqualTo("apple"));
        Assert.That(ItemAt(inventory, "hotbar", 1), Is.EqualTo("sword"));
        Assert.That(events, Is.EqualTo(1));
    }

    [Test]
    public void TrySort_RejectsUnknownSortContextAtomically()
    {
        var weapon = "gear:weapon";
        var armor = "gear:armor";
        var sword = new TaggedDefinition("sword", weapon);
        var helmet = new TaggedDefinition("helmet", armor);
        var layout = new SectionedLayout<string>(
            new SectionDefinition<string>("weapons", 1, weapon),
            new SectionDefinition<string>("armor", 1, armor));
        var inventory = CreateInventory(layout, tags: new[] { weapon, armor }, definitions: new ItemDefinition<string>[] { sword, helmet });
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("weapons", 0));
        inventory.TryAdd(helmet, out _, 1, SectionedLayoutContext<string>.Single("armor", 0));
        var data = (SectionedLayoutPersistentData)inventory.Layout.GetPersistentData();
        data.SlotMap = new System.Collections.Generic.List<int?> { 1, 0 };
        inventory.Layout.RestorePersistentData(data);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        Assert.That(inventory.TrySortLayout(new UnknownSortContext(), out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Invalid sort context type."));
        Assert.That(ItemAt(inventory, "weapons", 0), Is.EqualTo("helmet"));
        Assert.That(ItemAt(inventory, "armor", 0), Is.EqualTo("sword"));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void GetPersistentData_ReturnsDefensiveSlotMapCopy()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 1)),
            definitions: apple);
        inventory.TryAdd(apple, out _);

        var data = (SectionedLayoutPersistentData)inventory.Layout.GetPersistentData();
        data.SlotMap[0] = null;

        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("apple"));
    }

    [Test]
    public void RestorePersistentData_RestoresSectionSlots()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: new ItemDefinition<string>[] { apple, sword });
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("bag", 0));
        inventory.TryAdd(sword, out _, 1, SectionedLayoutContext<string>.Single("bag", 1));
        var data = (SectionedLayoutPersistentData)inventory.Layout.GetPersistentData();
        data.SlotMap = new System.Collections.Generic.List<int?> { 1, 0 };

        inventory.Layout.RestorePersistentData(data);

        Assert.That(ItemAt(inventory, "bag", 0), Is.EqualTo("sword"));
        Assert.That(ItemAt(inventory, "bag", 1), Is.EqualTo("apple"));
    }

    [Test]
    public void RestorePersistentData_RejectsMismatchedSections()
    {
        var inventory = CreateInventory(new SectionedLayout<string>(new SectionDefinition<string>("bag", 1)));
        var data = new SectionedLayoutPersistentData
        {
            SectionIds = new System.Collections.Generic.List<string> { "other" },
            SectionSlotCounts = new System.Collections.Generic.List<int> { 1 },
            SlotMap = new System.Collections.Generic.List<int?> { null }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => inventory.Layout.RestorePersistentData(data));
        Assert.That(ex!.Message, Is.EqualTo("Invalid layout data"));
    }

    [Test]
    public void Clone_PreservesSectionLayoutState()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)),
            definitions: apple);
        inventory.TryAdd(apple, out _, 1, SectionedLayoutContext<string>.Single("bag", 1));

        var clone = inventory.Layout.Clone();

        Assert.That(clone.GetItemAt(inventory, SectionedLayoutContext<string>.Single("bag", 1))!.Definition.Id, Is.EqualTo("apple"));
    }

    [Test]
    public void TransferBuilder_WithMappedSectionContext_PlacesIncomingEntries()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(definitions: new ItemDefinition<string>[] { apple, sword });
        var source = manager.CreateInventory(layout: new EntryLayout<string>());
        var target = manager.CreateInventory(layout: new SectionedLayout<string>(
                new SectionDefinition<string>("hotbar", 2),
                new SectionDefinition<string>("bag", 1)));
        source.TryAdd(apple, out _);
        source.TryAdd(sword, out _);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemoveByDefinition(apple, 1, metadataMatch: ItemMetadataMatch.Any, out _);
        transfer.TryRemoveByDefinition(sword, 1, metadataMatch: ItemMetadataMatch.Any, out _);
        var context = SectionedLayoutContext<string>.Map()
            .Add(0, "bag", 0)
            .Add(1, "hotbar", 1)
            .Build();

        Assert.That(transfer.Source.TryCommitTransfer(transfer, target, context, out var failure), Is.True);

        Assert.That(source.Items, Is.Empty);
        Assert.That(ItemAt(target, "bag", 0), Is.EqualTo("apple"));
        Assert.That(ItemAt(target, "hotbar", 1), Is.EqualTo("sword"));
    }

    [Test]
    public void InvalidMappedSectionTransferLeavesSourceAndTargetUnchanged()
    {
        var apple = new ItemDefinition<string>("apple");
        var sword = new ItemDefinition<string>("sword");
        var manager = CreateManager(definitions: new ItemDefinition<string>[] { apple, sword });
        var source = manager.CreateInventory(layout: new EntryLayout<string>());
        var target = manager.CreateInventory(layout: new SectionedLayout<string>(new SectionDefinition<string>("bag", 2)));
        source.TryAdd(apple, out _);
        source.TryAdd(sword, out _);
        var transfer = InventoryTransfer.From(source);
        transfer.TryRemoveByDefinition(apple, 1, metadataMatch: ItemMetadataMatch.Any, out _);
        transfer.TryRemoveByDefinition(sword, 1, metadataMatch: ItemMetadataMatch.Any, out _);
        var context = SectionedLayoutContext<string>.Map()
            .Add(0, "bag", 0)
            .Add(1, "bag", 0)
            .Build();

        Assert.That(transfer.Source.TryCommitTransfer(transfer, target, context, out var failure), Is.False);
        Assert.That(failure?.Message, Is.EqualTo("Duplicate mapped target section slot."));
        Assert.That(source.Items, Has.Count.EqualTo(2));
        Assert.That(target.Items, Is.Empty);
    }

    private static string? ItemAt(Inventory<string> inventory, string sectionId, int slotIndex)
    {
        return inventory.Layout.GetItemAt(inventory, SectionedLayoutContext<string>.Single(sectionId, slotIndex))?.Definition.Id;
    }

    private static Inventory<string> CreateInventory(
        IInventoryLayout<string> layout,
        string[]? tags = null,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );

        foreach (var tag in tags ?? Array.Empty<string>())
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static Inventory<string> CreateNonNamespacedInventory(
        IInventoryLayout<string> layout,
        string[] tags,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            layout,
            new ItemCatalog<string>()
            );

        manager.Catalog.Tags.UseNonNamespacedTagsOnly();
        foreach (var tag in tags)
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private static InventoryManager<string> CreateManager(
        string[]? tags = null,
        params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new ItemCatalog<string>()
            );

        foreach (var tag in tags ?? Array.Empty<string>())
            manager.Catalog.Tags.Define(tag);
        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager;
    }

    private sealed class TaggedDefinition : ItemDefinition<string>
    {
        public TaggedDefinition(string id, params string[] tags)
            : base(id, tags)
        {
        }
    }

    private sealed class UnknownSortContext : IInventorySortContext<string>
    {
    }
}


