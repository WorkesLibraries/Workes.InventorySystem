using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes one semantic add or remove operation inside an <see cref="InventoryItemDelta{TKey}"/>.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryItemDeltaOperation<TKey>
{
    private readonly InstanceMetadata? _addMetadata;
    private readonly ItemMetadataMatch _removeMetadataMatch;

    /// <summary>Gets whether this operation adds or removes items.</summary>
    public InventoryItemDeltaOperationKind Kind { get; }

    /// <summary>Gets the registered definition instance, when the operation was authored from a definition.</summary>
    public ItemDefinition<TKey>? Definition { get; }

    /// <summary>Gets the definition id for this operation.</summary>
    public TKey DefinitionId { get; }

    /// <summary>Gets the operation amount.</summary>
    public int Amount { get; }

    /// <summary>
    /// Gets detached concrete metadata for added items. Empty metadata is represented as
    /// <see langword="null"/>.
    /// </summary>
    public InstanceMetadata? AddMetadata => CloneMetadataOrNull(_addMetadata);

    /// <summary>
    /// Gets the metadata selector for remove operations. Add operations always use concrete metadata and expose
    /// <see cref="ItemMetadataMatch.Empty"/>.
    /// </summary>
    public ItemMetadataMatch RemoveMetadataMatch => _removeMetadataMatch;

    /// <summary>Gets the optional unique label authored on this operation, or <see langword="null"/>.</summary>
    public string? Label { get; }

    /// <summary>Gets label identities associated with this operation.</summary>
    public IReadOnlyList<InventoryItemDeltaLabelReference<TKey>> LabelReferences { get; }

    internal InventoryItemDeltaOperation(
        InventoryItemDeltaOperationKind kind,
        ItemDefinition<TKey>? definition,
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        ItemMetadataMatch metadataMatch,
        string? label,
        IReadOnlyList<InventoryItemDeltaLabelReference<TKey>>? labelReferences = null)
    {
        if (definitionId == null)
            throw new ArgumentNullException(nameof(definitionId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        if (kind == InventoryItemDeltaOperationKind.Add && metadataMatch.Kind != ItemMetadataMatchKind.Empty)
            throw new ArgumentException("Add operations cannot use metadata selectors.", nameof(metadataMatch));

        Kind = kind;
        Definition = definition;
        DefinitionId = definitionId;
        Amount = amount;
        _addMetadata = kind == InventoryItemDeltaOperationKind.Add
            ? CloneMetadataOrNull(metadata)
            : null;
        _removeMetadataMatch = kind == InventoryItemDeltaOperationKind.Remove
            ? metadataMatch
            : ItemMetadataMatch.Empty;
        Label = string.IsNullOrWhiteSpace(label) ? null : label;

        var references = labelReferences?.Select(CloneLabelReference).ToList()
            ?? CreateDefaultLabelReferences(Label, amount);
        LabelReferences = new ReadOnlyCollection<InventoryItemDeltaLabelReference<TKey>>(references);
    }

    internal InventoryItemDeltaOperation<TKey> WithKind(InventoryItemDeltaOperationKind kind)
    {
        if (kind == Kind)
            return new(kind, Definition, DefinitionId, Amount, _addMetadata, _removeMetadataMatch, Label, LabelReferences);

        if (kind == InventoryItemDeltaOperationKind.Add)
        {
            if (_removeMetadataMatch.Kind == ItemMetadataMatchKind.Any)
                throw new InvalidOperationException("Wildcard-metadata remove operations cannot be converted to add operations.");
            return new(
                kind,
                Definition,
                DefinitionId,
                Amount,
                _removeMetadataMatch.Metadata,
                ItemMetadataMatch.Empty,
                Label,
                LabelReferences);
        }

        return new(
            kind,
            Definition,
            DefinitionId,
            Amount,
            null,
            ItemMetadataMatch.Exact(_addMetadata),
            Label,
            LabelReferences);
    }

    internal InventoryItemDeltaOperation<TKey> WithLabelReferences(
        IReadOnlyList<InventoryItemDeltaLabelReference<TKey>> references) =>
        new(Kind, Definition, DefinitionId, Amount, _addMetadata, _removeMetadataMatch, Label, references);

    internal InstanceMetadata? GetStoredAddMetadataClone() =>
        CloneMetadataOrNull(_addMetadata);

    internal static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? metadata)
    {
        if (metadata == null || metadata.IsEmpty)
            return null;
        return metadata.Clone();
    }

    internal static bool MetadataEquals(InstanceMetadata? left, InstanceMetadata? right)
    {
        if ((left == null || left.IsEmpty) && (right == null || right.IsEmpty))
            return true;
        if (left == null || right == null)
            return false;
        return left.StructuralEquals(right);
    }

    private static List<InventoryItemDeltaLabelReference<TKey>> CreateDefaultLabelReferences(string? label, int amount)
    {
        if (string.IsNullOrWhiteSpace(label))
            return new List<InventoryItemDeltaLabelReference<TKey>>();
        return new List<InventoryItemDeltaLabelReference<TKey>>
        {
            new(label!, amount)
        };
    }

    private static InventoryItemDeltaLabelReference<TKey> CloneLabelReference(
        InventoryItemDeltaLabelReference<TKey> reference)
    {
        if (reference == null)
            throw new ArgumentException("Label references cannot contain null values.", nameof(reference));
        return new InventoryItemDeltaLabelReference<TKey>(
            reference.OriginalLabel,
            reference.Amount,
            reference.Prefix,
            reference.CombinedLabel);
    }
}
