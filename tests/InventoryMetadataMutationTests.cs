using System.Collections.Generic;
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
public class InventoryMetadataMutationTests
{
    [Test]
    public void InstanceMetadata_TrySet_MutatesDetachedMetadata()
    {
        var metadata = new InstanceMetadata();

        var accepted = metadata.TrySet("quality", "fresh", out var error);

        Assert.That(accepted, Is.True);
        Assert.That(metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void InventoryOwnedMetadata_TrySet_ValidatesAndFiresMetadataChanged()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 1, context: SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.Items[0].Metadata.TrySet("quality", "polished", out var error);

        Assert.That(accepted, Is.True);
        var change = captured!.MetadataChanged.Single();
        Assert.That(change.Instance, Is.SameAs(inventory.Items[0]));
        Assert.That(change.BeforeMetadata, Is.Empty);
        Assert.That(change.AfterMetadata["quality"], Is.EqualTo("polished"));
        Assert.That(((SlotLayoutContext<string>)change.LayoutContext!).SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void InventoryOwnedMetadata_Set_FiresMetadataChanged()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 1, context: SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Items[0].Metadata.Set("quality", "polished");

        var change = captured!.MetadataChanged.Single();
        Assert.That(change.Instance, Is.SameAs(inventory.Items[0]));
        Assert.That(change.BeforeMetadata, Is.Empty);
        Assert.That(change.AfterMetadata["quality"], Is.EqualTo("polished"));
        Assert.That(((SlotLayoutContext<string>)change.LayoutContext!).SlotIndex, Is.EqualTo(1));
        Assert.That(captured.RequiresFullRefresh, Is.False);
    }

    [Test]
    public void InventoryOwnedMetadata_TryAdd_FailsWhenKeyExists()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);
        inventory.Items[0].Metadata.TrySet("quality", "fresh", out _);

        var accepted = inventory.Items[0].Metadata.TryAdd("quality", "polished", out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void InventoryOwnedMetadata_Add_ThrowsWhenKeyExists()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);
        inventory.Items[0].Metadata.Set("quality", "fresh");
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Add("quality", "polished"));

        Assert.That(ex!.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_TryChange_FailsWhenKeyMissing()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);

        var accepted = inventory.Items[0].Metadata.TryChange("quality", "polished", out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.Items[0].Metadata.IsEmpty, Is.True);
    }

    [Test]
    public void InventoryOwnedMetadata_Change_ThrowsWhenKeyMissing()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Change("quality", "polished"));

        Assert.That(ex!.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.IsEmpty, Is.True);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_TryRemove_FailsWhenKeyMissing()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);

        var accepted = inventory.Items[0].Metadata.TryRemove("quality", out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
    }

    [Test]
    public void InventoryOwnedMetadata_Remove_ThrowsWhenRuleRejectsMutation()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new RequireMetadataKeyRule<string>("quality"),
            gem);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(gem, 1, null, metadata, out var buildError), Is.True);
        inventory.CommitTransaction(builder);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Remove("quality"));

        Assert.That(ex!.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_TryReplace_ReplacesAllValues()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);
        inventory.Items[0].Metadata.TrySet("quality", "fresh", out _);

        var accepted = inventory.Items[0].Metadata.TryReplace(
            new Dictionary<string, object?> { ["rarity"] = "rare" },
            out var error);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Items[0].Metadata.AsReadOnly().ContainsKey("quality"), Is.False);
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("rarity", out var rarity), Is.True);
        Assert.That(rarity, Is.EqualTo("rare"));
    }

    [Test]
    public void InventoryOwnedMetadata_Replace_ThrowsWhenProposedMetadataRejected()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new RequireMetadataKeyRule<string>("quality"),
            gem);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(gem, 1, null, metadata, out var buildError), Is.True);
        inventory.CommitTransaction(builder);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Replace(
            new Dictionary<string, object?> { ["rarity"] = "rare" }));

        Assert.That(ex!.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(inventory.Items[0].Metadata.AsReadOnly().ContainsKey("rarity"), Is.False);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_TryTransform_ValidatesProposedResult()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);

        var accepted = inventory.Items[0].Metadata.TryTransform(
            metadata =>
            {
                metadata.Set("quality", "fresh");
                metadata.Set("inspected", true);
            },
            out var error);

        Assert.That(accepted, Is.True);
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(inventory.Items[0].Metadata.TryGet<bool>("inspected", out var inspected), Is.True);
        Assert.That(inspected, Is.True);
    }

    [Test]
    public void InventoryOwnedMetadata_Transform_ThrowsWhenProposedMetadataRejected()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new RequireMetadataKeyRule<string>("quality"),
            gem);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(gem, 1, null, metadata, out var buildError), Is.True);
        inventory.CommitTransaction(builder);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Transform(
            proposed => proposed.Remove("quality")));

        Assert.That(ex!.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_RejectedMutationDoesNotChangeMetadata()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new RequireMetadataRule<string>("quality", "approved"),
            gem);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "approved");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(gem, 1, null, metadata, out var buildError), Is.True);
        inventory.CommitTransaction(builder);

        var accepted = inventory.Items[0].Metadata.TrySet("quality", "rejected", out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("approved"));
    }

    [Test]
    public void InventoryOwnedMetadata_RejectedMutationFiresNoEvent()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new MetadataDependentStackResolver("locked", 1, 10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            gem);
        inventory.Add(gem, amount: 5);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var accepted = inventory.Items[0].Metadata.TrySet("locked", true, out _);

        Assert.That(accepted, Is.False);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void InventoryOwnedMetadata_Set_ThrowsWhenStackResolverRejectsMutation()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new MetadataDependentStackResolver("locked", 1, 10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            gem);
        inventory.Add(gem, amount: 5);
        int events = 0;
        inventory.Changed += (_, _) => events++;

        var ex = Assert.Throws<InventoryOperationException>(() => inventory.Items[0].Metadata.Set("locked", true));

        Assert.That(ex!.Message, Does.Contain("exceed maximum stack size"));
        Assert.That(inventory.Items[0].Metadata.AsReadOnly().ContainsKey("locked"), Is.False);
        Assert.That(events, Is.EqualTo(0));
    }

    [Test]
    public void MetadataChangedEvent_AffectedContextsIncludesItemContext()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, context: SlotLayoutContext<string>.Single(1));
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Items[0].Metadata.TrySet("quality", "fresh", out _);

        Assert.That(captured!.AffectedLayoutContexts.OfType<SlotLayoutContext<string>>().Single().SlotIndex, Is.EqualTo(1));
    }

    [Test]
    public void MetadataChangedEvent_DoesNotRequireFullRefresh()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new EntryLayout<string>(), gem);
        inventory.Add(gem);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        inventory.Items[0].Metadata.TrySet("quality", "fresh", out _);

        Assert.That(captured!.RequiresFullRefresh, Is.False);
    }

    [Test]
    public void MetadataMutation_RejectsWhenMetadataRuleWouldFail()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            new RequireMetadataKeyRule<string>("quality"),
            gem);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(gem, 1, null, metadata, out var buildError), Is.True);
        inventory.CommitTransaction(builder);

        var accepted = inventory.Items[0].Metadata.TryRemove("quality", out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error?.Message, Does.Contain("quality"));
        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void MetadataMutation_RejectsWhenStackResolverWouldRejectProposedMetadata()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(
            new MetadataDependentStackResolver("locked", 1, 10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            gem);
        inventory.Add(gem, amount: 5);

        var accepted = inventory.Items[0].Metadata.TrySet("locked", true, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(error?.Message, Does.Contain("exceed maximum stack size"));
    }

    [Test]
    public void SplitAndSetMetadata_SplitsStackAndAppliesMetadataToSplitAmount()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 5);

        var accepted = inventory.Items[0].TrySplitAndSetMetadata(2, "quest-item", true, out var metadataStack, out var error);

        Assert.That(accepted, Is.True);
        Assert.That(metadataStack, Is.Not.Null);
        Assert.That(inventory.Items.Sum(item => item.Amount), Is.EqualTo(5));
        Assert.That(inventory.Items.Select(item => item.Amount), Is.EquivalentTo(new[] { 3, 2 }));
        Assert.That(metadataStack!.Amount, Is.EqualTo(2));
        Assert.That(metadataStack.Metadata.TryGet<bool>("quest-item", out var questItem), Is.True);
        Assert.That(questItem, Is.True);
        Assert.That(inventory.Items.Single(item => item.Amount == 3).Metadata.IsEmpty, Is.True);
    }

    [Test]
    public void SplitAndSetMetadata_FullAmountMutatesSameStack()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 5);
        var original = inventory.Items[0];

        var accepted = original.TrySplitAndSetMetadata(5, "quality", "polished", out var metadataStack, out var error);

        Assert.That(accepted, Is.True);
        Assert.That(metadataStack, Is.SameAs(original));
        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(original.Metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("polished"));
    }

    [Test]
    public void SplitAndSetMetadata_RejectsWhenNoLayoutPositionForNewStack()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(1), gem);
        inventory.Add(gem, amount: 5);

        var accepted = inventory.Items[0].TrySplitAndSetMetadata(2, "quest-item", true, out var metadataStack, out var error);

        Assert.That(accepted, Is.False);
        Assert.That(metadataStack, Is.Null);
        Assert.That(error, Is.Not.Null);
        Assert.That(inventory.Items, Has.Count.EqualTo(1));
        Assert.That(inventory.Items[0].Amount, Is.EqualTo(5));
        Assert.That(inventory.Items[0].Metadata.IsEmpty, Is.True);
    }

    [Test]
    public void SplitAndSetMetadata_PreservesTotalAmount()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 5);

        inventory.Items[0].TrySplitAndSetMetadata(2, "quest-item", true, out _, out _);

        Assert.That(inventory.TotalItemCount, Is.EqualTo(5));
    }

    [Test]
    public void SplitAndSetMetadata_FiresModifiedAndAddedButNotMetadataChanged()
    {
        var gem = new ItemDefinition<string>("gem");
        var inventory = CreateInventory(new FixedSizeStackResolver<string>(10), new UnlimitedCapacityPolicy<string>(), new SlotLayout<string>(2), gem);
        inventory.Add(gem, amount: 5);
        InventoryChangedEventArgs<string>? captured = null;
        inventory.Changed += (_, args) => captured = args;

        var accepted = inventory.Items[0].TrySplitAndSetMetadata(2, "quest-item", true, out _, out var error);

        Assert.That(accepted, Is.True);
        Assert.That(captured!.Modified, Has.Count.EqualTo(1));
        Assert.That(captured.Added, Has.Count.EqualTo(1));
        Assert.That(captured.MetadataChanged, Is.Empty);
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        ICapacityPolicy<string> capacityPolicy,
        IInventoryLayout<string> layout,
        ItemDefinition<string> definition)
    {
        return CreateInventory(stackResolver, capacityPolicy, layout, new RuleContainer<string>(), definition);
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        ICapacityPolicy<string> capacityPolicy,
        IInventoryLayout<string> layout,
        IRulePolicy<string> rule,
        ItemDefinition<string> definition)
    {
        var rules = new RuleContainer<string>();
        rules.Add("metadata-rule", rule);
        return CreateInventory(stackResolver, capacityPolicy, layout, rules, definition);
    }

    private static Inventory<string> CreateInventory(
        IStackResolver<string> stackResolver,
        ICapacityPolicy<string> capacityPolicy,
        IInventoryLayout<string> layout,
        RuleContainer<string> rules,
        ItemDefinition<string> definition)
    {
        var manager = new InventoryManager<string>(
        stackResolver,
        capacityPolicy,
        layout,
        new ItemCatalog<string>(),
        rules
        );
        manager.Registry.Register(definition);
        manager.Catalog.Freeze();
        return manager.CreateInventory();
    }

    private sealed class MetadataDependentStackResolver : IStackResolver<string>
    {
        private readonly string _key;
        private readonly int _whenPresent;
        private readonly int _whenMissing;

        public MetadataDependentStackResolver(string key, int whenPresent, int whenMissing)
        {
            _key = key;
            _whenPresent = whenPresent;
            _whenMissing = whenMissing;
        }

        public int ResolveMaxStackSize(Inventory<string> inventory, ItemInstance<string> instance)
        {
            return instance.Metadata.AsReadOnly().ContainsKey(_key)
                ? _whenPresent
                : _whenMissing;
        }
    }
}
