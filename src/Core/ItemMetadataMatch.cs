using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes how an operation matches item-instance metadata when selecting existing items by definition.
/// </summary>
/// <remarks>
/// Use <see cref="Empty"/> for plain items, <see cref="Exact(InstanceMetadata?)"/> for structurally equal metadata,
/// and <see cref="Any"/> when metadata should not participate in matching.
/// </remarks>
public readonly struct ItemMetadataMatch : IEquatable<ItemMetadataMatch>
{
    private readonly InstanceMetadata? _metadata;

    private ItemMetadataMatch(ItemMetadataMatchKind kind, InstanceMetadata? metadata)
    {
        Kind = kind;
        _metadata = kind == ItemMetadataMatchKind.Exact ? CloneMetadataOrNull(metadata) : null;
    }

    /// <summary>Matches only structurally empty item metadata.</summary>
    public static ItemMetadataMatch Empty { get; } = new(ItemMetadataMatchKind.Empty, null);

    /// <summary>Matches item metadata regardless of its contents.</summary>
    public static ItemMetadataMatch Any { get; } = new(ItemMetadataMatchKind.Any, null);

    /// <summary>Gets the metadata matching mode.</summary>
    public ItemMetadataMatchKind Kind { get; }

    /// <summary>
    /// Gets detached exact metadata for <see cref="ItemMetadataMatchKind.Exact"/> matches. Empty exact metadata is
    /// normalized to <see cref="Empty"/> and returns <see langword="null"/>.
    /// </summary>
    public InstanceMetadata? Metadata => CloneMetadataOrNull(_metadata);

    /// <summary>Creates an exact metadata match, or <see cref="Empty"/> when metadata is null or empty.</summary>
    public static ItemMetadataMatch Exact(InstanceMetadata? metadata)
    {
        if (metadata == null || metadata.IsEmpty)
            return Empty;

        return new ItemMetadataMatch(ItemMetadataMatchKind.Exact, metadata);
    }

    /// <summary>Returns whether the supplied metadata satisfies this selector.</summary>
    public bool Matches(InstanceMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        return Kind switch
        {
            ItemMetadataMatchKind.Any => true,
            ItemMetadataMatchKind.Empty => metadata.IsEmpty,
            ItemMetadataMatchKind.Exact => _metadata != null && metadata.StructuralEquals(_metadata),
            _ => false
        };
    }

    /// <inheritdoc />
    public bool Equals(ItemMetadataMatch other)
    {
        if (Kind != other.Kind)
            return false;
        if (Kind != ItemMetadataMatchKind.Exact)
            return true;
        if (_metadata == null || _metadata.IsEmpty)
            return other._metadata == null || other._metadata.IsEmpty;
        if (other._metadata == null || other._metadata.IsEmpty)
            return false;
        return _metadata.StructuralEquals(other._metadata);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ItemMetadataMatch other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // InstanceMetadata exposes structural equality but not a stable structural hash. Returning a coarse hash keeps
        // the equality contract correct and avoids exposing mutable metadata identity through this value object.
        return (int)Kind;
    }

    /// <summary>Returns whether two metadata selectors have the same structural semantics.</summary>
    public static bool operator ==(ItemMetadataMatch left, ItemMetadataMatch right) => left.Equals(right);

    /// <summary>Returns whether two metadata selectors have different structural semantics.</summary>
    public static bool operator !=(ItemMetadataMatch left, ItemMetadataMatch right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() =>
        Kind switch
        {
            ItemMetadataMatchKind.Empty => "Empty",
            ItemMetadataMatchKind.Exact => "Exact",
            ItemMetadataMatchKind.Any => "Any",
            _ => Kind.ToString()
        };

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? metadata)
    {
        if (metadata == null || metadata.IsEmpty)
            return null;

        return metadata.Clone();
    }
}
