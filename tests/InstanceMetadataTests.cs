using System.Collections.Generic;
using System;
using NUnit.Framework;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Stacking;

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InstanceMetadataTests
{
    [Test]
    public void InstanceMetadata_Add_StoresNewValue()
    {
        var metadata = new InstanceMetadata();

        metadata.Add("quality", "fresh");

        Assert.That(metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void InstanceMetadata_Add_ThrowsWhenKeyExists()
    {
        var metadata = new InstanceMetadata();
        metadata.Add("quality", "fresh");

        Assert.Throws<InvalidOperationException>(() => metadata.Add("quality", "stale"));

        Assert.That(metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
    }

    [Test]
    public void InstanceMetadata_Set_AddsOrReplacesValue()
    {
        var metadata = new InstanceMetadata();

        metadata.Set("quality", "fresh");
        metadata.Set("quality", "stale");

        Assert.That(metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("stale"));
    }

    [Test]
    public void InstanceMetadata_Change_ThrowsWhenKeyMissing()
    {
        var metadata = new InstanceMetadata();

        Assert.Throws<InvalidOperationException>(() => metadata.Change("quality", "fresh"));

        Assert.That(metadata.IsEmpty, Is.True);
    }

    [Test]
    public void InstanceMetadata_Remove_ThrowsWhenKeyMissing()
    {
        var metadata = new InstanceMetadata();

        Assert.Throws<InvalidOperationException>(() => metadata.Remove("quality"));
    }

    [Test]
    public void InstanceMetadata_Remove_RemovesExistingValue()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        metadata.Remove("quality");

        Assert.That(metadata.AsReadOnly().ContainsKey("quality"), Is.False);
    }

    [Test]
    public void InstanceMetadata_Clear_RemovesAllValues()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        metadata.Set("owner", "player");

        metadata.Clear();
        metadata.Clear();

        Assert.That(metadata.IsEmpty, Is.True);
    }

    [Test]
    public void InstanceMetadata_Replace_ReplacesAllValues()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        metadata.Replace(new Dictionary<string, object> { ["rarity"] = "rare" });

        Assert.That(metadata.AsReadOnly().ContainsKey("quality"), Is.False);
        Assert.That(metadata.TryGet<string>("rarity", out var rarity), Is.True);
        Assert.That(rarity, Is.EqualTo("rare"));

        metadata.Replace(null);

        Assert.That(metadata.IsEmpty, Is.True);
    }

    [Test]
    public void InstanceMetadata_Transform_AppliesMutations()
    {
        var metadata = new InstanceMetadata();

        metadata.Transform(proposed =>
        {
            proposed.Set("quality", "fresh");
            proposed.Set("inspected", true);
        });

        Assert.That(metadata.TryGet<string>("quality", out var quality), Is.True);
        Assert.That(quality, Is.EqualTo("fresh"));
        Assert.That(metadata.TryGet<bool>("inspected", out var inspected), Is.True);
        Assert.That(inspected, Is.True);
    }

    [Test]
    public void InstanceMetadata_Transform_ThrowsForNullTransform()
    {
        var metadata = new InstanceMetadata();

        Assert.Throws<ArgumentNullException>(() => metadata.Transform(null!));
    }

    [Test]
    public void RestoreMetadata_CopiesDictionaryContainer()
    {
        var data = new Dictionary<string, object> { ["quality"] = "fresh" };
        var metadata = new InstanceMetadata();

        metadata.RestoreMetadata(data);
        data["quality"] = "stale";

        Assert.That(metadata.TryGet<string>("quality", out var value), Is.True);
        Assert.That(value, Is.EqualTo("fresh"));
    }

    [Test]
    public void ToDictionary_ReturnsMutableCopy()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        var copy = metadata.ToDictionary();
        copy["quality"] = "stale";

        Assert.That(metadata.TryGet<string>("quality", out var value), Is.True);
        Assert.That(value, Is.EqualTo("fresh"));
    }

    [Test]
    public void AsReadOnly_ReturnsLiveViewWhenMetadataExists()
    {
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        var view = metadata.AsReadOnly();
        metadata.Set("quality", "stale");

        Assert.That(view["quality"], Is.EqualTo("stale"));
    }

    [Test]
    public void ItemInstance_KeepsProvidedMetadataReference()
    {
        var apple = new ItemDefinition<string>("apple");
        var metadata = new InstanceMetadata();

        var instance = new ItemInstance<string>(apple, 1, metadata);

        Assert.That(ReferenceEquals(instance.Metadata, metadata), Is.True);
    }

    [Test]
    public void TransactionAdd_ClonesCallerMetadataForInsertedInstance()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(apple);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");
        var builder = InventoryTransaction<string>.From(inventory);
        builder.TryAdd(apple, 1, null, metadata, out _);
        inventory.CommitTransaction(builder.Build());

        metadata.Set("quality", "stale");

        Assert.That(inventory.Items[0].Metadata.TryGet<string>("quality", out var value), Is.True);
        Assert.That(value, Is.EqualTo("fresh"));
        Assert.That(ReferenceEquals(inventory.Items[0].Metadata, metadata), Is.False);
    }

    [Test]
    public void InventoryTransferEntry_ClonesProvidedMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceInstance = new ItemInstance<string>(apple);
        var metadata = new InstanceMetadata();
        metadata.Set("quality", "fresh");

        var entry = new InventoryTransferEntry<string>(apple, 1, metadata, sourceInstance);
        metadata.Set("quality", "stale");

        Assert.That(entry.Metadata, Is.Not.Null);
        Assert.That(ReferenceEquals(entry.Metadata, metadata), Is.False);
        Assert.That(entry.Metadata!.TryGet<string>("quality", out var value), Is.True);
        Assert.That(value, Is.EqualTo("fresh"));
    }

    [Test]
    public void InventoryTransferEntry_StoresNullForEmptyMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceInstance = new ItemInstance<string>(apple);

        var entry = new InventoryTransferEntry<string>(apple, 1, new InstanceMetadata(), sourceInstance);

        Assert.That(entry.Metadata, Is.Null);
    }

    [Test]
    public void InventoryTransferEntry_StoresNullForNullMetadata()
    {
        var apple = new ItemDefinition<string>("apple");
        var sourceInstance = new ItemInstance<string>(apple);

        var entry = new InventoryTransferEntry<string>(apple, 1, null, sourceInstance);

        Assert.That(entry.Metadata, Is.Null);
    }

    private static Inventory<string> CreateInventory(params ItemDefinition<string>[] definitions)
    {
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>());

        foreach (var definition in definitions)
            manager.Registry.Register(definition);
        manager.Catalog.Freeze();

        return manager.CreateInventory();
    }
}


