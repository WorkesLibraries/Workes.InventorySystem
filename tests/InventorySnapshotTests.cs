using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Persistence;
using Workes.InventorySystem.Rules;
using Workes.InventorySystem.Stacking;

#pragma warning disable CS0618 // Reflection verifies the legacy compatibility annotations.

namespace Workes.InventorySystem.Tests;

[TestFixture]
public class InventorySnapshotTests
{
    [Test]
    public void CaptureSnapshot_RoundTripsThroughJsonAndXml()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(
            new SlotLayout<string>(3),
            apple);
        var nested = new List<object?> { "rare", new List<int> { 1, 4, 9 }, null };
        var metadata = new InstanceMetadata();
        metadata.Set("nested", nested);
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(
            builder.TryAdd(apple, 2, SlotLayoutContext<string>.Single(2), metadata, out var addError),
            Is.True,
            addError);
        inventory.CommitTransaction(builder.Build());
        inventory.Attributes.Set("owner", "player-1");
        inventory.Attributes.Set("levels", new[] { 2, 3, 5 });

        var snapshot = inventory.CaptureSnapshot();
        var json = JsonSerializer.Serialize(snapshot);
        var jsonRoundTrip = JsonSerializer.Deserialize<InventorySnapshot>(json);

        Assert.That(InventorySnapshotValidator.TryValidate(jsonRoundTrip, out var jsonError), Is.True, jsonError);
        Assert.That(jsonRoundTrip!.Entries.Single().EntryId, Is.EqualTo("e0"));
        Assert.That(jsonRoundTrip.Attributes.Select(value => value.Name), Is.EqualTo(new[] { "levels", "owner" }));

        var serializer = new XmlSerializer(typeof(InventorySnapshot));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, snapshot);
        stream.Position = 0;
        var xmlRoundTrip = (InventorySnapshot)serializer.Deserialize(stream)!;

        Assert.That(InventorySnapshotValidator.TryValidate(xmlRoundTrip, out var xmlError), Is.True, xmlError);
        Assert.That(xmlRoundTrip.Layout.Kind, Is.EqualTo("workes.inventory.layout.slot"));
        Assert.That(xmlRoundTrip.Entries.Single().Metadata.Single().Name, Is.EqualTo("nested"));
    }

    [Test]
    public void BuiltInScalarCodecs_RoundTripExactValues()
    {
        AssertRoundTrip("hello");
        AssertRoundTrip('ø');
        AssertRoundTrip(true);
        AssertRoundTrip(byte.MaxValue);
        AssertRoundTrip(sbyte.MinValue);
        AssertRoundTrip(short.MinValue);
        AssertRoundTrip(ushort.MaxValue);
        AssertRoundTrip(int.MinValue);
        AssertRoundTrip(uint.MaxValue);
        AssertRoundTrip(long.MinValue);
        AssertRoundTrip(ulong.MaxValue);
        AssertRoundTrip(decimal.MinValue);
        AssertRoundTrip(Guid.Parse("d2719f44-5b51-4e80-a37d-3fb7b4bb7925"));
        AssertRoundTrip(new DateTime(638900000000000000, DateTimeKind.Utc));
        AssertRoundTrip(new DateTimeOffset(638900000000000000, TimeSpan.FromHours(5.5)));
        AssertRoundTrip(TimeSpan.FromTicks(long.MinValue + 1));
    }

    [Test]
    public void CaptureSnapshot_SupportsEveryBuiltInScalarKeyType()
    {
        AssertCapturedKey("key");
        AssertCapturedKey('k');
        AssertCapturedKey(true);
        AssertCapturedKey(byte.MaxValue);
        AssertCapturedKey(sbyte.MinValue);
        AssertCapturedKey(short.MinValue);
        AssertCapturedKey(ushort.MaxValue);
        AssertCapturedKey(int.MinValue);
        AssertCapturedKey(uint.MaxValue);
        AssertCapturedKey(long.MinValue);
        AssertCapturedKey(ulong.MaxValue);
        AssertCapturedKey(1.25f);
        AssertCapturedKey(-2.5d);
        AssertCapturedKey(123.4500m);
        AssertCapturedKey(Guid.Parse("4eb7db2b-1fa3-4631-993a-b14cf134784e"));
        AssertCapturedKey(new DateTime(638900000000000000, DateTimeKind.Utc));
        AssertCapturedKey(new DateTimeOffset(638900000000000000, TimeSpan.FromHours(-3)));
        AssertCapturedKey(TimeSpan.FromMinutes(17));
    }

    [Test]
    public void FloatingPointCodecs_PreserveEveryBit()
    {
        var floats = new[]
        {
            0f,
            BitConverter.ToSingle(BitConverter.GetBytes(0x80000000u), 0),
            float.PositiveInfinity,
            float.NegativeInfinity,
            BitConverter.ToSingle(BitConverter.GetBytes(0x7FC01234u), 0)
        };
        foreach (var value in floats)
        {
            var decoded = RoundTrip(value);
            Assert.That(
                BitConverter.ToUInt32(BitConverter.GetBytes(decoded), 0),
                Is.EqualTo(BitConverter.ToUInt32(BitConverter.GetBytes(value), 0)));
        }

        var doubles = new[]
        {
            0d,
            BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL)),
            double.PositiveInfinity,
            double.NegativeInfinity,
            BitConverter.Int64BitsToDouble(unchecked((long)0x7FF8000000001234UL))
        };
        foreach (var value in doubles)
        {
            var decoded = RoundTrip(value);
            Assert.That(
                BitConverter.DoubleToInt64Bits(decoded),
                Is.EqualTo(BitConverter.DoubleToInt64Bits(value)));
        }
    }

    [Test]
    public void CollectionCodecs_RoundTripNestedArraysListsAndNulls()
    {
        var value = new List<object?>
        {
            new[] { 1, 2, 3 },
            new List<string?> { "a", null, "c" },
            new List<object?> { true, new[] { "nested" } }
        };

        var decoded = RoundTrip(value);

        Assert.That((int[])decoded[0]!, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That((List<string?>)decoded[1]!, Is.EqualTo(new string?[] { "a", null, "c" }));
        Assert.That(((List<object?>)decoded[2]!)[0], Is.True);
    }

    [Test]
    public void CaptureSnapshot_IsDeeplyDetachedInBothDirections()
    {
        var apple = new ItemDefinition<string>("apple");
        var inventory = CreateInventory(new EntryLayout<string>(), apple);
        var values = new List<int> { 1, 2 };
        var metadata = new InstanceMetadata();
        metadata.Set("values", values);
        var builder = InventoryTransaction<string>.From(inventory);
        Assert.That(builder.TryAdd(apple, 1, null, metadata, out var addError), Is.True, addError);
        inventory.CommitTransaction(builder.Build());

        var snapshot = inventory.CaptureSnapshot();
        values.Add(3);

        var encodedValues = snapshot.Entries.Single().Metadata.Single().Value;
        Assert.That(InventorySnapshotCodecs.TryDecode(encodedValues, out List<int> captured, out var error), Is.True, error);
        Assert.That(captured, Is.EqualTo(new[] { 1, 2 }));

        snapshot.Entries[0].Amount = 99;
        encodedValues.Data.Items.Clear();
        Assert.That(inventory.Items.Single().Amount, Is.EqualTo(1));
        Assert.That((List<int>)inventory.Items.Single().Metadata.AsReadOnly()["values"], Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void CaptureSnapshot_PreservesTypedInventoryAttributesIncludingNull()
    {
        var inventory = CreateInventory(new EntryLayout<string>());
        inventory.Attributes.Set("mode", 7);
        inventory.Attributes.Set("mode", "seven");
        inventory.Attributes.Set<string?>("optional", null);

        var snapshot = inventory.CaptureSnapshot();

        Assert.That(snapshot.Attributes, Has.Count.EqualTo(3));
        var modes = snapshot.Attributes.Where(attribute => attribute.Name == "mode").ToList();
        Assert.That(modes, Has.Count.EqualTo(2));
        Assert.That(
            modes.Any(value =>
                InventorySnapshotCodecs.TryDecode(value.Value, out int number, out _) &&
                number == 7),
            Is.True);
        Assert.That(
            modes.Any(value =>
                InventorySnapshotCodecs.TryDecode(value.Value, out string text, out _) &&
                text == "seven"),
            Is.True);
        var optional = snapshot.Attributes.Single(attribute => attribute.Name == "optional");
        Assert.That(
            InventorySnapshotCodecs.TryDecode(optional.Value, out string? decoded, out var error),
            Is.True,
            error);
        Assert.That(decoded, Is.Null);
    }

    [Test]
    public void CaptureSnapshot_UsesStableEntryReferencesInsteadOfStorageIndices()
    {
        var apple = new ItemDefinition<string>("apple");
        var berry = new ItemDefinition<string>("berry");
        var inventory = CreateInventory(new SlotLayout<string>(4), apple, berry);
        inventory.Add(apple, context: SlotLayoutContext<string>.Single(3));
        inventory.Add(berry, context: SlotLayoutContext<string>.Single(1));

        var snapshot = inventory.CaptureSnapshot();
        var slots = snapshot.Layout.Data.Properties.Single(property => property.Name == "slots").Value;
        Assert.That(InventorySnapshotCodecs.TryDecode(slots, out List<object?> references, out var error), Is.True, error);
        Assert.That(references, Is.EqualTo(new object?[] { null, "e1", null, "e0" }));

        snapshot.Entries.Reverse();
        Assert.That(InventorySnapshotValidator.TryValidate(snapshot, out error), Is.True, error);
        Assert.That(InventorySnapshotCodecs.TryDecode(slots, out references, out error), Is.True, error);
        Assert.That(references, Is.EqualTo(new object?[] { null, "e1", null, "e0" }));
    }

    [Test]
    public void CaptureSnapshot_SupportsEveryBuiltInLayout()
    {
        var layouts = new IInventoryLayout<string>[]
        {
            new EntryLayout<string>(),
            new SlotLayout<string>(2),
            new GridLayout<string>(2, 2),
            new MultiCellGridLayout<string>(2, 2, new UnitFootprintProvider()),
            new EquipmentLayout<string>(new EquipmentSlot<string>("head")),
            new SectionedLayout<string>(new SectionDefinition<string>("bag", 2))
        };
        var kinds = new[]
        {
            "workes.inventory.layout.entry",
            "workes.inventory.layout.slot",
            "workes.inventory.layout.grid",
            "workes.inventory.layout.multi-cell-grid",
            "workes.inventory.layout.equipment",
            "workes.inventory.layout.sectioned"
        };

        for (int i = 0; i < layouts.Length; i++)
        {
            var snapshot = CreateInventory(layouts[i]).CaptureSnapshot();
            Assert.That(snapshot.Layout.Kind, Is.EqualTo(kinds[i]));
            Assert.That(snapshot.Layout.DataVersion, Is.EqualTo(1));
            Assert.That(InventorySnapshotValidator.TryValidate(snapshot, out var error), Is.True, error);
        }
    }

    [Test]
    public void TypeAssignedCodec_SupportsCustomDefinitionKeys()
    {
        var key = new CustomKey("apple");
        var definition = new ItemDefinition<CustomKey>(key);
        var inventory = CreateInventory(new EntryLayout<CustomKey>(), definition);
        inventory.Add(definition);

        var snapshot = inventory.CaptureSnapshot();

        var encoded = snapshot.Entries.Single().DefinitionId;
        Assert.That(encoded.CodecId, Is.EqualTo("tests.custom-key"));
        Assert.That(encoded.Data.StringValue, Is.EqualTo(key.Value));
    }

    [Test]
    public void CustomLayoutCodec_IsOwnedByTheLayout()
    {
        var layout = new CustomSlotLayout(3);
        var inventory = CreateInventoryDirect(layout);

        var snapshot = inventory.CaptureSnapshot();

        Assert.That(snapshot.Layout.Kind, Is.EqualTo("tests.layout.custom-slot"));
        Assert.That(snapshot.Layout.DataVersion, Is.EqualTo(2));
        Assert.That(snapshot.Layout.Data.Properties.Single().Name, Is.EqualTo("slotCount"));
    }

    [Test]
    public void CaptureSnapshot_FailsWithoutRequiredValueOrLayoutCodec()
    {
        var unsupportedDefinition = new ItemDefinition<UnsupportedKey>(new UnsupportedKey());
        var unsupportedKeyInventory = CreateInventory(
            new EntryLayout<UnsupportedKey>(),
            unsupportedDefinition);
        unsupportedKeyInventory.Add(unsupportedDefinition);

        Assert.That(
            unsupportedKeyInventory.TryCaptureSnapshot(out var missingKeySnapshot, out var keyError),
            Is.False);
        Assert.That(missingKeySnapshot, Is.Null);
        Assert.That(keyError, Does.Contain("must declare InventorySnapshotKeyCodecAttribute"));

        var metadataInventory = CreateInventory(new EntryLayout<string>(), new ItemDefinition<string>("item"));
        metadataInventory.Add(metadataInventory.Catalog.Registry.Resolve("item"));
        Assert.That(
            metadataInventory.Items.Single().Metadata.TrySet("state", UnsupportedEnum.First, out var enumError),
            Is.False);
        Assert.That(enumError, Does.Contain("unsupported"));
    }

    [Test]
    public void CaptureSnapshot_RejectsCyclesAndMalformedCustomOutput()
    {
        var item = new ItemDefinition<string>("item");
        var inventory = CreateInventory(new EntryLayout<string>(), item);
        inventory.Add(item);
        var cyclic = new List<object?>();
        cyclic.Add(cyclic);
        Assert.That(inventory.Items.Single().Metadata.TrySet("cycle", cyclic, out var cycleError), Is.False);
        Assert.That(cycleError, Does.Contain("cycle"));

        var malformed = new ItemDefinition<MalformedValue>(new MalformedValue());
        var malformedInventory = CreateInventory(new EntryLayout<MalformedValue>(), malformed);
        malformedInventory.Add(malformed);

        Assert.That(malformedInventory.TryCaptureSnapshot(out _, out var malformedError), Is.False);
        Assert.That(malformedError, Does.Contain("produced invalid data"));
    }

    [Test]
    public void CodecAndSnapshotValidation_RejectMalformedData()
    {
        var encoded = InventorySnapshotCodecs.Encode(42);
        encoded.CodecVersion = 99;
        Assert.That(InventorySnapshotCodecs.TryDecode(encoded, out int _, out var versionError), Is.False);
        Assert.That(versionError, Does.Contain("version 99"));

        var malformed = new InventorySnapshot { FormatVersion = 99 };
        Assert.That(InventorySnapshotValidator.TryValidate(malformed, out var formatError), Is.False);
        Assert.That(formatError, Does.Contain("unsupported"));

        malformed = new InventorySnapshot
        {
            Entries =
            {
                new InventorySnapshotEntry
                {
                    EntryId = "e0",
                    Amount = 1,
                    DefinitionId = new SnapshotEncodedValue
                    {
                        CodecId = "missing.codec",
                        CodecVersion = 1,
                        Data = SnapshotValue.String("x")
                    }
                }
            },
            Layout = new InventoryLayoutSnapshot
            {
                Kind = "test",
                DataVersion = 1,
                Data = SnapshotValue.Object()
            }
        };
        Assert.That(InventorySnapshotValidator.TryValidate(malformed, out var codecError), Is.True, codecError);

        var valid = CreateInventory(new SlotLayout<string>(1)).CaptureSnapshot();
        var slots = valid.Layout.Data.Properties.Single(property => property.Name == "slots").Value;
        slots.Data.Items[0] = InventorySnapshotCodecs.Encode("missing-entry");
        Assert.That(InventorySnapshotValidator.TryValidate(valid, out var referenceError), Is.True, referenceError);
        Assert.That(
            new SlotLayout<string>(1).SnapshotCodec.TryDecode(
                new InventoryLayoutSnapshotDecodeContext<string>(
                    valid.Layout,
                    valid.Entries.ToDictionary(entry => entry.EntryId)),
                out _,
                out referenceError),
            Is.False);
        Assert.That(referenceError, Does.Contain("unknown entry id"));
    }

    [Test]
    public void SectionedLayoutCodec_RejectsOverflowingSectionSizesThroughTryContract()
    {
        var snapshot = new InventoryLayoutSnapshot
        {
            Kind = "workes.inventory.layout.sectioned",
            DataVersion = 1,
            Data = SnapshotValue.Object(new[]
            {
                new SnapshotNamedValue
                {
                    Name = "sectionIds",
                    Value = InventorySnapshotCodecs.Encode(new List<string> { "a", "b" })
                },
                new SnapshotNamedValue
                {
                    Name = "sectionSlotCounts",
                    Value = InventorySnapshotCodecs.Encode(new List<int> { int.MaxValue, 1 })
                },
                new SnapshotNamedValue
                {
                    Name = "slots",
                    Value = InventorySnapshotCodecs.Encode(new List<object?>())
                }
            })
        };

        Assert.That(
            new SectionedLayout<string>(new SectionDefinition<string>("section", 1))
                .SnapshotCodec.TryDecode(
                    new InventoryLayoutSnapshotDecodeContext<string>(
                        snapshot,
                        new Dictionary<string, InventorySnapshotEntry>()),
                    out _,
                    out var error),
            Is.False);
        Assert.That(error, Does.Contain("malformed shape"));
    }

    [Test]
    public void CodecExtensionSurface_HasNoPublicRegistration()
    {
        Assert.That(
            typeof(InventorySnapshotCodecs).GetMethod(
                "RegisterValue",
                BindingFlags.Public | BindingFlags.Static),
            Is.Null);
        Assert.That(
            typeof(InventorySnapshot).Assembly.GetType(
                "Workes.InventorySystem.Persistence.InventoryLayoutSnapshotCodecs"),
            Is.Null);
    }

    [Test]
    public void TypeAssignedCustomKeyCodec_SupportsConcurrentCapture()
    {
        Assert.DoesNotThrow(() =>
            Parallel.For(0, 32, index =>
            {
                var key = new CustomKey("item-" + index);
                var definition = new ItemDefinition<CustomKey>(key);
                var inventory = CreateInventory(new EntryLayout<CustomKey>(), definition);
                inventory.Add(definition);
                Assert.That(inventory.CaptureSnapshot().Entries.Single().DefinitionId.CodecId,
                    Is.EqualTo("tests.custom-key"));
            }));
    }

    [Test]
    public void DefinitionIdDecode_RemainsCompatibleWithRegistryMigrations()
    {
        var oldDefinition = new ItemDefinition<string>("old-apple");
        var source = CreateInventory(new EntryLayout<string>(), oldDefinition);
        source.Add(oldDefinition);
        var snapshot = source.CaptureSnapshot();

        var replacement = new ItemDefinition<string>("apple");
        var targetCatalog = new ItemCatalog<string>();
        targetCatalog.Registry.Register(replacement);
        targetCatalog.Registry.RegisterMigration("old-apple", replacement);
        targetCatalog.Freeze();

        Assert.That(
            InventorySnapshotCodecs.TryDecode(
                snapshot.Entries.Single().DefinitionId,
                out string decodedId,
                out var error),
            Is.True,
            error);
        Assert.That(targetCatalog.Registry.Resolve(decodedId), Is.SameAs(replacement));
    }

    [Test]
    public void LegacyPersistenceSurface_IsObsoleteButRetained()
    {
        Assert.That(typeof(SerializedInventory<>).GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
        Assert.That(typeof(SerializedItem<>).GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
        Assert.That(
            typeof(Inventory<string>).GetMethod(nameof(Inventory<string>.Serialize))!
                .GetCustomAttribute<ObsoleteAttribute>(),
            Is.Not.Null);
        Assert.That(
            typeof(Inventory<string>).GetMethod(nameof(Inventory<string>.Deserialize))!
                .GetCustomAttribute<ObsoleteAttribute>(),
            Is.Not.Null);
    }

    private static T RoundTrip<T>(T value)
    {
        Assert.That(InventorySnapshotCodecs.TryEncode(value, out var encoded, out var encodeError), Is.True, encodeError);
        Assert.That(encoded, Is.Not.Null);
        Assert.That(InventorySnapshotCodecs.TryDecode(encoded!, out T decoded, out var decodeError), Is.True, decodeError);
        return decoded;
    }

    private static void AssertRoundTrip<T>(T value)
    {
        Assert.That(RoundTrip(value), Is.EqualTo(value));
    }

    private static void AssertCapturedKey<TKey>(TKey key)
    {
        var definition = new ItemDefinition<TKey>(key);
        var inventory = CreateInventory(new EntryLayout<TKey>(), definition);
        inventory.Add(definition);

        var snapshot = inventory.CaptureSnapshot();

        Assert.That(
            InventorySnapshotCodecs.TryDecode(
                snapshot.Entries.Single().DefinitionId,
                out TKey decoded,
                out var error),
            Is.True,
            error);
        Assert.That(decoded, Is.EqualTo(key));
    }

    private static Inventory<TKey> CreateInventory<TKey>(
        IInventoryLayout<TKey> layout,
        params ItemDefinition<TKey>[] definitions)
    {
        var catalog = new ItemCatalog<TKey>();
        foreach (var definition in definitions)
            catalog.Registry.Register(definition);
        catalog.Freeze();
        var manager = new InventoryManager<TKey>(
            new FixedSizeStackResolver<TKey>(10),
            new UnlimitedCapacityPolicy<TKey>(),
            layout,
            catalog);
        return manager.CreateInventory();
    }

    private static Inventory<string> CreateInventoryDirect(IInventoryLayout<string> layout)
    {
        var catalog = new ItemCatalog<string>();
        catalog.Freeze();
        var manager = new InventoryManager<string>(
            new FixedSizeStackResolver<string>(10),
            new UnlimitedCapacityPolicy<string>(),
            new EntryLayout<string>(),
            catalog);
        return new Inventory<string>(
            manager,
            manager.DefaultStackResolver,
            manager.DefaultCapacityPolicy,
            layout,
            new RuleContainer<string>());
    }

    private sealed class UnitFootprintProvider : IGridFootprintProvider<string>
    {
        public GridFootprint GetFootprint(ItemDefinition<string> definition) => new(1, 1);
    }

    [InventorySnapshotKeyCodec(typeof(CustomKeyCodec))]
    private sealed class CustomKey : IEquatable<CustomKey>
    {
        public CustomKey(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public bool Equals(CustomKey? other) => other != null && other.Value == Value;
        public override bool Equals(object? obj) => obj is CustomKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    private sealed class CustomKeyCodec : IInventorySnapshotKeyCodec<CustomKey>
    {
        public string FormatId => "tests.custom-key";
        public int CurrentVersion => 1;

        public bool TryEncode(CustomKey value, out SnapshotValue? encoded, out string? error)
        {
            encoded = SnapshotValue.String(value.Value);
            error = null;
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out CustomKey value, out string? error)
        {
            value = null!;
            if (version != 1 || encoded.Kind != SnapshotValueKind.String || encoded.StringValue == null)
            {
                error = "Invalid custom key.";
                return false;
            }
            value = new CustomKey(encoded.StringValue);
            error = null;
            return true;
        }
    }

    private sealed class CustomSlotLayout : SlotLayout<string>
    {
        public CustomSlotLayout(int slotCount)
            : base(slotCount)
        {
        }

        public override IInventoryLayoutSnapshotCodec<string> SnapshotCodec =>
            CustomSlotLayoutCodec.Instance;
    }

    private sealed class CustomSlotLayoutCodec : IInventoryLayoutSnapshotCodec<string>
    {
        public static CustomSlotLayoutCodec Instance { get; } = new();
        public string LayoutKind => "tests.layout.custom-slot";
        public int CurrentVersion => 2;

        public bool TryCapture(
            InventoryLayoutSnapshotCaptureContext<string> context,
            out SnapshotValue? data,
            out string? error)
        {
            var layout = (CustomSlotLayout)context.Layout;
            var persistent = (SlotLayoutPersistentData)layout.GetPersistentData();
            data = SnapshotValue.Object(new[]
            {
                new SnapshotNamedValue
                {
                    Name = "slotCount",
                    Value = InventorySnapshotCodecs.Encode(persistent.SlotMap.Count)
                }
            });
            error = null;
            return true;
        }

        public bool TryDecode(
            InventoryLayoutSnapshotDecodeContext<string> context,
            out InventoryLayoutSnapshotCandidate<string>? candidate,
            out string? error)
        {
            candidate = null;
            error = null;
            if (context.Snapshot.Kind != LayoutKind ||
                context.Snapshot.DataVersion != 2 ||
                context.Snapshot.Data.Kind != SnapshotValueKind.Object ||
                context.Snapshot.Data.Properties.Count != 1 ||
                context.Snapshot.Data.Properties[0].Name != "slotCount" ||
                !InventorySnapshotCodecs.TryDecode(
                    context.Snapshot.Data.Properties[0].Value,
                    out int count,
                    out error) ||
                count <= 0)
            {
                error ??= "Invalid custom slot layout data.";
                return false;
            }
            candidate = new InventoryLayoutSnapshotCandidate<string>(
                LayoutKind,
                2,
                context.Snapshot.Data,
                new Dictionary<string, IReadOnlyList<ILayoutContext<string>>>());
            return true;
        }
    }

    private sealed class UnsupportedKey
    {
    }

    private enum UnsupportedEnum
    {
        First
    }

    [InventorySnapshotKeyCodec(typeof(MalformedValueCodec))]
    private sealed class MalformedValue
    {
    }

    private sealed class MalformedValueCodec : IInventorySnapshotKeyCodec<MalformedValue>
    {
        public string FormatId => "tests.malformed-value";
        public int CurrentVersion => 1;

        public bool TryEncode(MalformedValue value, out SnapshotValue? encoded, out string? error)
        {
            encoded = SnapshotValue.String(null!);
            error = null;
            return true;
        }

        public bool TryDecode(SnapshotValue encoded, int version, out MalformedValue value, out string? error)
        {
            value = new MalformedValue();
            error = null;
            return true;
        }
    }

}
