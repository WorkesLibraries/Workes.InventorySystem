using System.Collections.Generic;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Portable, non-generic representation of one captured inventory state.
/// </summary>
public sealed class InventorySnapshot
{
    /// <summary>The current package-owned snapshot format version.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Gets or sets the package snapshot format version.</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>Gets or sets entries in inventory storage order.</summary>
    public List<InventorySnapshotEntry> Entries { get; set; } = new();

    /// <summary>Gets or sets inventory-owned metadata values.</summary>
    public List<SnapshotNamedValue> Metadata { get; set; } = new();

    /// <summary>Gets or sets the captured layout state.</summary>
    public InventoryLayoutSnapshot Layout { get; set; } = new();
}

/// <summary>
/// Portable representation of one inventory item instance.
/// </summary>
public sealed class InventorySnapshotEntry
{
    /// <summary>Gets or sets the stable snapshot-local entry identifier.</summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the encoded item-definition identifier.</summary>
    public SnapshotEncodedValue DefinitionId { get; set; } = new();

    /// <summary>Gets or sets the captured stack amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets per-instance metadata values.</summary>
    public List<SnapshotNamedValue> Metadata { get; set; } = new();
}

/// <summary>
/// Portable representation of layout-owned snapshot state.
/// </summary>
public sealed class InventoryLayoutSnapshot
{
    /// <summary>Gets or sets the stable layout kind identifier.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the layout data format version.</summary>
    public int DataVersion { get; set; }

    /// <summary>Gets or sets the layout-owned value tree.</summary>
    public SnapshotValue Data { get; set; } = SnapshotValue.Null();
}

/// <summary>
/// Associates a stable string name with an encoded snapshot value.
/// </summary>
public sealed class SnapshotNamedValue
{
    /// <summary>Gets or sets the property or metadata name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the encoded value.</summary>
    public SnapshotEncodedValue Value { get; set; } = new();
}

/// <summary>
/// Associates a value payload with the codec required to decode it.
/// </summary>
public sealed class SnapshotEncodedValue
{
    /// <summary>Gets or sets the stable codec format identifier.</summary>
    public string CodecId { get; set; } = string.Empty;

    /// <summary>Gets or sets the codec data version.</summary>
    public int CodecVersion { get; set; }

    /// <summary>Gets or sets the encoded value payload.</summary>
    public SnapshotValue Data { get; set; } = SnapshotValue.Null();
}

/// <summary>
/// Concrete serializer-friendly value tree used by snapshot codecs.
/// </summary>
public sealed class SnapshotValue
{
    /// <summary>Gets or sets the payload shape.</summary>
    public SnapshotValueKind Kind { get; set; }

    /// <summary>Gets or sets a Boolean scalar when <see cref="Kind"/> is <see cref="SnapshotValueKind.Boolean"/>.</summary>
    public bool BooleanValue { get; set; }

    /// <summary>Gets or sets a string scalar when <see cref="Kind"/> is <see cref="SnapshotValueKind.String"/>.</summary>
    public string? StringValue { get; set; }

    /// <summary>Gets or sets ordered encoded children when <see cref="Kind"/> is <see cref="SnapshotValueKind.List"/>.</summary>
    public List<SnapshotEncodedValue> Items { get; set; } = new();

    /// <summary>Gets or sets named encoded children when <see cref="Kind"/> is <see cref="SnapshotValueKind.Object"/>.</summary>
    public List<SnapshotNamedValue> Properties { get; set; } = new();

    /// <summary>Creates a null value.</summary>
    public static SnapshotValue Null() => new() { Kind = SnapshotValueKind.Null };

    /// <summary>Creates a Boolean value.</summary>
    public static SnapshotValue Boolean(bool value) =>
        new() { Kind = SnapshotValueKind.Boolean, BooleanValue = value };

    /// <summary>Creates a string value.</summary>
    public static SnapshotValue String(string value) =>
        new() { Kind = SnapshotValueKind.String, StringValue = value };

    /// <summary>Creates a list value.</summary>
    public static SnapshotValue List(IEnumerable<SnapshotEncodedValue>? items = null) =>
        new()
        {
            Kind = SnapshotValueKind.List,
            Items = items != null ? new List<SnapshotEncodedValue>(items) : new List<SnapshotEncodedValue>()
        };

    /// <summary>Creates an object value.</summary>
    public static SnapshotValue Object(IEnumerable<SnapshotNamedValue>? properties = null) =>
        new()
        {
            Kind = SnapshotValueKind.Object,
            Properties = properties != null ? new List<SnapshotNamedValue>(properties) : new List<SnapshotNamedValue>()
        };
}
