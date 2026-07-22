using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes a reusable, context-free semantic net change for one inventory.
/// </summary>
/// <remarks>
/// A delta does not know about an inventory, layout, capacity policy, or rules. It only describes which definitions are
/// added to or removed from the inventory it is later applied to.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryItemDelta<TKey>
{
    private readonly List<InventoryItemDeltaOperation<TKey>> _operations = new();
    private readonly HashSet<string> _labels = new(StringComparer.Ordinal);

    /// <summary>Creates an empty item delta.</summary>
    public InventoryItemDelta()
    {
    }

    private InventoryItemDelta(IEnumerable<InventoryItemDeltaOperation<TKey>> operations)
    {
        foreach (var operation in operations)
        {
            if (!TryAppend(operation, out var failure))
                throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Delta operation was rejected."));
        }
    }

    /// <summary>Gets the semantic operations in insertion order.</summary>
    public IReadOnlyList<InventoryItemDeltaOperation<TKey>> Operations =>
        new ReadOnlyCollection<InventoryItemDeltaOperation<TKey>>(_operations);

    /// <summary>Gets whether this delta contains no semantic operations.</summary>
    public bool IsEmpty => _operations.Count == 0;

    /// <summary>Creates an empty item delta.</summary>
    public static InventoryItemDelta<TKey> Create() => new();

    /// <summary>Adds an add operation by definition.</summary>
    public InventoryItemDelta<TKey> Add(
        ItemDefinition<TKey> definition,
        int amount = 1,
        InstanceMetadata? metadata = null,
        string? label = null)
    {
        ThrowIfRejected(TryAdd(definition, amount, metadata, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add an add operation by definition.</summary>
    public bool TryAdd(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        string? label,
        out InventoryFailure? failure)
    {
        if (definition == null)
        {
            failure = InventoryFailures.Definition("Definition cannot be null.");
            return false;
        }

        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Add,
                definition,
                definition.Id,
                amount,
                metadata,
                InventoryItemDeltaMetadataMatch.Exact,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Adds an add operation by definition id.</summary>
    public InventoryItemDelta<TKey> Add(
        TKey definitionId,
        int amount = 1,
        InstanceMetadata? metadata = null,
        string? label = null)
    {
        ThrowIfRejected(TryAdd(definitionId, amount, metadata, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add an add operation by definition id.</summary>
    public bool TryAdd(
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        string? label,
        out InventoryFailure? failure)
    {
        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Add,
                null,
                definitionId,
                amount,
                metadata,
                InventoryItemDeltaMetadataMatch.Exact,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Adds a remove operation that matches exact empty metadata.</summary>
    public InventoryItemDelta<TKey> Remove(ItemDefinition<TKey> definition, int amount = 1, string? label = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation that matches exact empty metadata.</summary>
    public bool TryRemove(
        ItemDefinition<TKey> definition,
        int amount,
        string? label,
        out InventoryFailure? failure) =>
        TryRemove(definition, amount, metadata: null, label, out failure);

    /// <summary>Adds a remove operation that matches exact metadata.</summary>
    public InventoryItemDelta<TKey> Remove(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        string? label = null)
    {
        ThrowIfRejected(TryRemove(definition, amount, metadata, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation that matches exact metadata.</summary>
    public bool TryRemove(
        ItemDefinition<TKey> definition,
        int amount,
        InstanceMetadata? metadata,
        string? label,
        out InventoryFailure? failure)
    {
        if (definition == null)
        {
            failure = InventoryFailures.Definition("Definition cannot be null.");
            return false;
        }

        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Remove,
                definition,
                definition.Id,
                amount,
                metadata,
                InventoryItemDeltaMetadataMatch.Exact,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Adds a remove operation by definition id that matches exact empty metadata.</summary>
    public InventoryItemDelta<TKey> Remove(TKey definitionId, int amount = 1, string? label = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation by definition id that matches exact empty metadata.</summary>
    public bool TryRemove(TKey definitionId, int amount, string? label, out InventoryFailure? failure) =>
        TryRemove(definitionId, amount, metadata: null, label, out failure);

    /// <summary>Adds a remove operation by definition id that matches exact metadata.</summary>
    public InventoryItemDelta<TKey> Remove(
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        string? label = null)
    {
        ThrowIfRejected(TryRemove(definitionId, amount, metadata, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation by definition id that matches exact metadata.</summary>
    public bool TryRemove(
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        string? label,
        out InventoryFailure? failure)
    {
        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Remove,
                null,
                definitionId,
                amount,
                metadata,
                InventoryItemDeltaMetadataMatch.Exact,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Adds a remove operation that ignores item-instance metadata.</summary>
    public InventoryItemDelta<TKey> RemoveAnyMetadata(
        ItemDefinition<TKey> definition,
        int amount = 1,
        string? label = null)
    {
        ThrowIfRejected(TryRemoveAnyMetadata(definition, amount, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation that ignores item-instance metadata.</summary>
    public bool TryRemoveAnyMetadata(
        ItemDefinition<TKey> definition,
        int amount,
        string? label,
        out InventoryFailure? failure)
    {
        if (definition == null)
        {
            failure = InventoryFailures.Definition("Definition cannot be null.");
            return false;
        }

        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Remove,
                definition,
                definition.Id,
                amount,
                metadata: null,
                InventoryItemDeltaMetadataMatch.Any,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Adds a remove operation by definition id that ignores item-instance metadata.</summary>
    public InventoryItemDelta<TKey> RemoveAnyMetadata(TKey definitionId, int amount = 1, string? label = null)
    {
        ThrowIfRejected(TryRemoveAnyMetadata(definitionId, amount, label, out var failure), failure);
        return this;
    }

    /// <summary>Attempts to add a remove operation by definition id that ignores item-instance metadata.</summary>
    public bool TryRemoveAnyMetadata(TKey definitionId, int amount, string? label, out InventoryFailure? failure)
    {
        if (!TryCreateOperation(
                InventoryItemDeltaOperationKind.Remove,
                null,
                definitionId,
                amount,
                metadata: null,
                InventoryItemDeltaMetadataMatch.Any,
                label,
                out var operation,
                out failure))
            return false;

        return TryAppend(operation!, out failure);
    }

    /// <summary>Creates a delta with add and remove operations inverted.</summary>
    /// <exception cref="InventoryOperationException">
    /// The delta contains a wildcard-metadata removal that cannot be mirrored into an exact add.
    /// </exception>
    public InventoryItemDelta<TKey> Mirror() => Mirror(this);

    /// <summary>Attempts to create a delta with add and remove operations inverted.</summary>
    public bool TryMirror(out InventoryItemDelta<TKey>? mirrored, out InventoryFailure? failure) =>
        TryMirror(this, out mirrored, out failure);

    /// <summary>Creates a delta with add and remove operations inverted.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="delta"/> is <see langword="null"/>.</exception>
    /// <exception cref="InventoryOperationException">
    /// The delta contains a wildcard-metadata removal that cannot be mirrored into an exact add.
    /// </exception>
    public static InventoryItemDelta<TKey> Mirror(InventoryItemDelta<TKey> delta)
    {
        if (delta == null)
            throw new ArgumentNullException(nameof(delta));

        if (!TryMirror(delta, out var mirrored, out var failure) || mirrored == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Delta mirroring was rejected."));

        return mirrored;
    }

    /// <summary>Attempts to create a delta with add and remove operations inverted.</summary>
    public static bool TryMirror(
        InventoryItemDelta<TKey> delta,
        out InventoryItemDelta<TKey>? mirrored,
        out InventoryFailure? failure)
    {
        if (delta == null)
            throw new ArgumentNullException(nameof(delta));

        var mirroredOperations = new List<InventoryItemDeltaOperation<TKey>>(delta._operations.Count);
        foreach (var operation in delta._operations)
        {
            if (operation.Kind == InventoryItemDeltaOperationKind.Remove
                && operation.MetadataMatch == InventoryItemDeltaMetadataMatch.Any)
            {
                mirrored = null;
                failure = InventoryFailures.Transaction(
                    "Wildcard-metadata remove operations cannot be mirrored because the mirrored add metadata would be ambiguous. Use exact metadata, explicit per-side deltas, manual cross-inventory side staging, or transfer helpers when runtime-selected metadata must be preserved.");
                return false;
            }

            mirroredOperations.Add(operation.WithKind(operation.Kind == InventoryItemDeltaOperationKind.Add
                ? InventoryItemDeltaOperationKind.Remove
                : InventoryItemDeltaOperationKind.Add));
        }

        mirrored = new InventoryItemDelta<TKey>(mirroredOperations);
        failure = null;
        return true;
    }

    /// <summary>Semantically combines prefixed deltas into one net delta.</summary>
    public static InventoryItemDelta<TKey> Combine(params InventoryItemDeltaPart<TKey>[] parts) =>
        Combine((IEnumerable<InventoryItemDeltaPart<TKey>>)parts);

    /// <summary>Semantically combines prefixed deltas into one net delta.</summary>
    public static InventoryItemDelta<TKey> Combine(IEnumerable<InventoryItemDeltaPart<TKey>> parts)
    {
        if (parts == null)
            throw new ArgumentNullException(nameof(parts));

        var partList = parts.ToList();
        if (partList.Count == 0)
            return new InventoryItemDelta<TKey>();

        ValidateUniquePrefixes(partList);

        var groups = new List<CombineGroup>();
        foreach (var part in partList)
        {
            if (part == null)
                throw new ArgumentException("Combine parts cannot contain null values.", nameof(parts));

            foreach (var operation in part.Delta._operations)
            {
                var group = groups.FirstOrDefault(candidate => candidate.Matches(operation));
                if (group == null)
                {
                    group = new CombineGroup(operation);
                    groups.Add(group);
                }

                int signedAmount = operation.Kind == InventoryItemDeltaOperationKind.Add
                    ? operation.Amount * part.Count
                    : -operation.Amount * part.Count;
                group.NetAmount += signedAmount;
                group.AddLabelContributions(operation, part, signedAmount);
            }
        }

        var result = new InventoryItemDelta<TKey>();
        foreach (var group in groups)
        {
            if (group.NetAmount == 0)
                continue;

            var kind = group.NetAmount > 0
                ? InventoryItemDeltaOperationKind.Add
                : InventoryItemDeltaOperationKind.Remove;
            int amount = Math.Abs(group.NetAmount);
            var references = group.GetSurvivingLabelReferences(kind, amount);
            var operation = new InventoryItemDeltaOperation<TKey>(
                kind,
                group.Definition,
                group.DefinitionId,
                amount,
                group.Metadata,
                group.MetadataMatch,
                label: null,
                references);
            if (!result.TryAppend(operation, out var failure))
                throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Combined delta operation was rejected."));
        }

        return result;
    }

    private bool TryAppend(InventoryItemDeltaOperation<TKey> operation, out InventoryFailure? failure)
    {
        if (operation == null)
        {
            failure = InventoryFailures.Transaction("Delta operation cannot be null.");
            return false;
        }

        var proposedLabels = new HashSet<string>(_labels, StringComparer.Ordinal);
        foreach (var reference in operation.LabelReferences)
        {
            if (!string.IsNullOrWhiteSpace(reference.CombinedLabel) && !proposedLabels.Add(reference.CombinedLabel!))
            {
                failure = InventoryFailures.Transaction($"Delta label '{reference.CombinedLabel}' already exists.");
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(operation.Label) && !proposedLabels.Add(operation.Label!))
        {
            failure = InventoryFailures.Transaction($"Delta label '{operation.Label}' already exists.");
            return false;
        }

        _labels.Clear();
        foreach (var label in proposedLabels)
            _labels.Add(label);
        _operations.Add(operation);
        failure = null;
        return true;
    }

    private static bool TryCreateOperation(
        InventoryItemDeltaOperationKind kind,
        ItemDefinition<TKey>? definition,
        TKey definitionId,
        int amount,
        InstanceMetadata? metadata,
        InventoryItemDeltaMetadataMatch metadataMatch,
        string? label,
        out InventoryItemDeltaOperation<TKey>? operation,
        out InventoryFailure? failure)
    {
        operation = null;
        if (definition == null && definitionId == null)
        {
            failure = InventoryFailures.Definition("Definition id cannot be null.");
            return false;
        }
        if (definition != null && definition.Id == null)
        {
            failure = InventoryFailures.Definition("Definition id cannot be null.");
            return false;
        }
        if (amount <= 0)
        {
            failure = InventoryFailures.Validation("Amount must be greater than zero.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(label))
            label = null;

        try
        {
            operation = new InventoryItemDeltaOperation<TKey>(
                kind,
                definition,
                definition != null ? definition.Id : definitionId,
                amount,
                metadata,
                metadataMatch,
                label);
            failure = null;
            return true;
        }
        catch (InventorySystemException ex)
        {
            failure = ex.Failure;
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is ArgumentOutOfRangeException)
        {
            failure = InventoryFailures.Transaction(ex.Message);
            return false;
        }
    }

    private static void ThrowIfRejected(bool accepted, InventoryFailure? failure)
    {
        if (!accepted)
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Delta operation was rejected."));
    }

    private static void ValidateUniquePrefixes(IReadOnlyList<InventoryItemDeltaPart<TKey>> parts)
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (part == null)
                throw new ArgumentException("Combine parts cannot contain null values.", nameof(parts));
            if (!prefixes.Add(part.Prefix))
                throw new InventoryOperationException(InventoryFailures.Transaction($"Delta combine prefix '{part.Prefix}' is already used."));
        }
    }

    private sealed class CombineGroup
    {
        private readonly List<LabelContribution> _positiveLabels = new();
        private readonly List<LabelContribution> _negativeLabels = new();

        public ItemDefinition<TKey>? Definition { get; }
        public TKey DefinitionId { get; }
        public InstanceMetadata? Metadata { get; }
        public InventoryItemDeltaMetadataMatch MetadataMatch { get; }
        public int NetAmount { get; set; }

        public CombineGroup(InventoryItemDeltaOperation<TKey> operation)
        {
            Definition = operation.Definition;
            DefinitionId = operation.DefinitionId;
            Metadata = operation.GetStoredMetadataClone();
            MetadataMatch = operation.MetadataMatch;
        }

        public bool Matches(InventoryItemDeltaOperation<TKey> operation)
        {
            return EqualityComparer<TKey>.Default.Equals(DefinitionId, operation.DefinitionId)
                && MetadataMatch == operation.MetadataMatch
                && InventoryItemDeltaOperation<TKey>.MetadataEquals(Metadata, operation.Metadata);
        }

        public void AddLabelContributions(
            InventoryItemDeltaOperation<TKey> operation,
            InventoryItemDeltaPart<TKey> part,
            int signedAmount)
        {
            if (operation.LabelReferences.Count == 0)
                return;

            var target = signedAmount >= 0 ? _positiveLabels : _negativeLabels;
            foreach (var reference in operation.LabelReferences)
            {
                int amount = reference.Amount * part.Count;
                string combinedLabel = $"{part.Prefix}.{reference.OriginalLabel}";
                target.Add(new LabelContribution(
                    reference.OriginalLabel,
                    part.Prefix,
                    combinedLabel,
                    amount));
            }
        }

        public IReadOnlyList<InventoryItemDeltaLabelReference<TKey>> GetSurvivingLabelReferences(
            InventoryItemDeltaOperationKind kind,
            int amount)
        {
            var source = kind == InventoryItemDeltaOperationKind.Add ? _positiveLabels : _negativeLabels;
            var result = new List<InventoryItemDeltaLabelReference<TKey>>();
            int remaining = amount;
            for (int i = source.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var label = source[i];
                int used = Math.Min(label.Amount, remaining);
                result.Insert(0, new InventoryItemDeltaLabelReference<TKey>(
                    label.OriginalLabel,
                    used,
                    label.Prefix,
                    label.CombinedLabel));
                remaining -= used;
            }
            return result;
        }
    }

    private sealed class LabelContribution
    {
        public string OriginalLabel { get; }
        public string Prefix { get; }
        public string CombinedLabel { get; }
        public int Amount { get; }

        public LabelContribution(string originalLabel, string prefix, string combinedLabel, int amount)
        {
            OriginalLabel = originalLabel;
            Prefix = prefix;
            CombinedLabel = combinedLabel;
            Amount = amount;
        }
    }
}
