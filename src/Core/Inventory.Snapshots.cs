using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Events;
using Workes.InventorySystem.Events.Dto;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Persistence;
using Workes.InventorySystem.Rules;

namespace Workes.InventorySystem.Core;

public partial class Inventory<TKey>
{
    private sealed class ResolvedSnapshotEntry
    {
        public ResolvedSnapshotEntry(
            InventorySnapshotEntry source,
            ItemDefinition<TKey> definition,
            InstanceMetadata metadata,
            int sourceIndex)
        {
            Source = source;
            Definition = definition;
            Metadata = metadata;
            SourceIndex = sourceIndex;
        }

        public InventorySnapshotEntry Source { get; }
        public ItemDefinition<TKey> Definition { get; }
        public InstanceMetadata Metadata { get; }
        public int SourceIndex { get; }
    }

    private sealed class SnapshotPlan
    {
        public SnapshotPlan(
            Inventory<TKey> candidate,
            IReadOnlyList<SnapshotItemLoss> losses,
            IReadOnlyList<SnapshotIssue> issues)
        {
            Candidate = candidate;
            Losses = losses;
            Issues = issues;
        }

        public Inventory<TKey> Candidate { get; }
        public IReadOnlyList<SnapshotItemLoss> Losses { get; }
        public IReadOnlyList<SnapshotIssue> Issues { get; }
    }

    /// <summary>
    /// Assesses exact restoration, lossless reconciliation, and salvage without mutating this inventory.
    /// </summary>
    public SnapshotAssessmentResult AssessSnapshot(
        InventorySnapshot snapshot,
        SnapshotSalvageOptions<TKey>? salvageOptions = null)
    {
        var issues = new List<SnapshotIssue>();
        if (TryPlanExactSnapshot(snapshot, out _, out var exactIssue, out _))
        {
            return new SnapshotAssessmentResult(
                canRestoreExactly: true,
                canReconcileWithoutLoss: true,
                canSalvage: true,
                SnapshotApplicationOutcome.Exact,
                issues,
                Array.Empty<SnapshotItemLoss>());
        }
        if (exactIssue != null)
            issues.Add(exactIssue);

        if (TryPlanReconciledSnapshot(snapshot, out _, out var reconcileIssue, out _))
        {
            return new SnapshotAssessmentResult(
                canRestoreExactly: false,
                canReconcileWithoutLoss: true,
                canSalvage: true,
                SnapshotApplicationOutcome.Reconciled,
                issues,
                Array.Empty<SnapshotItemLoss>());
        }
        if (reconcileIssue != null)
            issues.Add(reconcileIssue);

        if (TryPlanSalvagedSnapshot(
                snapshot,
                salvageOptions ?? new SnapshotSalvageOptions<TKey>(),
                out var salvage,
                out var salvageIssue,
                out _))
        {
            if (salvageIssue != null)
                issues.Add(salvageIssue);
            return new SnapshotAssessmentResult(
                canRestoreExactly: false,
                canReconcileWithoutLoss: false,
                canSalvage: true,
                salvage!.Losses.Count == 0
                    ? SnapshotApplicationOutcome.Reconciled
                    : SnapshotApplicationOutcome.Salvaged,
                issues,
                salvage.Losses);
        }
        if (salvageIssue != null)
            issues.Add(salvageIssue);

        return new SnapshotAssessmentResult(
            false,
            false,
            false,
            null,
            issues,
            Array.Empty<SnapshotItemLoss>());
    }

    /// <summary>Restores the snapshot exactly or throws without changing this inventory.</summary>
    public SnapshotApplicationResult RestoreSnapshot(InventorySnapshot snapshot)
    {
        if (!TryRestoreSnapshot(snapshot, out var result, out var failure) || result == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Snapshot("Exact snapshot restoration failed."));
        return result;
    }

    /// <summary>Attempts exact stack, storage-order, metadata, and layout restoration atomically.</summary>
    public bool TryRestoreSnapshot(
        InventorySnapshot snapshot,
        out SnapshotApplicationResult? result,
        out InventoryFailure? failure)
    {
        result = null;
        if (!TryPlanExactSnapshot(snapshot, out var plan, out _, out failure) || plan == null)
            return false;
        result = ApplySnapshotPlan(plan, SnapshotApplicationMode.Exact, SnapshotApplicationOutcome.Exact);
        return true;
    }

    /// <summary>Reconciles all snapshot quantities into current stacking and automatic layout placement.</summary>
    public SnapshotApplicationResult ReconcileSnapshot(InventorySnapshot snapshot)
    {
        if (!TryReconcileSnapshot(snapshot, out var result, out var failure) || result == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Snapshot("Snapshot reconciliation failed."));
        return result;
    }

    /// <summary>Attempts lossless snapshot reconciliation atomically.</summary>
    public bool TryReconcileSnapshot(
        InventorySnapshot snapshot,
        out SnapshotApplicationResult? result,
        out InventoryFailure? failure)
    {
        result = null;
        if (!TryPlanReconciledSnapshot(snapshot, out var plan, out _, out failure) || plan == null)
            return false;
        result = ApplySnapshotPlan(plan, SnapshotApplicationMode.Reconcile, SnapshotApplicationOutcome.Reconciled);
        return true;
    }

    /// <summary>Salvages a deterministic best-effort subset or throws without changing this inventory.</summary>
    public SnapshotApplicationResult SalvageSnapshot(
        InventorySnapshot snapshot,
        SnapshotSalvageOptions<TKey>? options = null)
    {
        if (!TrySalvageSnapshot(snapshot, options, out var result, out var failure) || result == null)
            throw new InventoryOperationException(failure ?? InventoryFailures.Snapshot("Snapshot salvage failed."));
        return result;
    }

    /// <summary>Attempts deterministic, explicitly lossy snapshot salvage atomically.</summary>
    public bool TrySalvageSnapshot(
        InventorySnapshot snapshot,
        SnapshotSalvageOptions<TKey>? options,
        out SnapshotApplicationResult? result,
        out InventoryFailure? failure)
    {
        result = null;
        if (!TryPlanSalvagedSnapshot(
                snapshot,
                options ?? new SnapshotSalvageOptions<TKey>(),
                out var plan,
                out _,
                out failure) ||
            plan == null)
        {
            return false;
        }
        var outcome = plan.Losses.Count == 0
            ? SnapshotApplicationOutcome.Reconciled
            : SnapshotApplicationOutcome.Salvaged;
        result = ApplySnapshotPlan(plan, SnapshotApplicationMode.Salvage, outcome);
        return true;
    }

    private bool TryPlanExactSnapshot(
        InventorySnapshot snapshot,
        out SnapshotPlan? plan,
        out SnapshotIssue? issue,
        out InventoryFailure? failure)
    {
        plan = null;
        issue = null;
        if (!TryResolveSnapshot(
                snapshot,
                allowUnknownDefinitions: false,
                out var entries,
                out var rootMetadata,
                out _,
                out issue,
                out failure))
            return false;

        if (!string.Equals(snapshot.Layout.Kind, _layout.SnapshotCodec.LayoutKind, StringComparison.Ordinal))
            return FailPlan(
                SnapshotIssueCode.Layout,
                InventoryFailures.Layout($"Snapshot layout kind '{snapshot.Layout.Kind}' does not match current layout kind '{_layout.SnapshotCodec.LayoutKind}'."),
                out issue,
                out failure);

        var snapshotEntries = snapshot.Entries.ToDictionary(entry => entry.EntryId, StringComparer.Ordinal);
        if (!_layout.SnapshotCodec.TryDecode(
                new InventoryLayoutSnapshotDecodeContext<TKey>(snapshot.Layout, snapshotEntries),
                out var layoutCandidate,
                out failure) ||
            layoutCandidate == null)
        {
            return FailPlan(
                SnapshotIssueCode.Layout,
                failure ?? InventoryFailures.Layout("Current layout codec rejected the snapshot layout."),
                out issue,
                out failure);
        }
        var instancesById = new Dictionary<string, ItemInstance<TKey>>(StringComparer.Ordinal);
        var storageIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var instance = new ItemInstance<TKey>(
                entry.Definition,
                entry.Source.Amount,
                entry.Metadata.IsEmpty ? null : entry.Metadata.Clone());
            if (!TryResolveMaxStackSize(instance, out int maxStack, out failure) ||
                instance.Amount > maxStack)
            {
                failure ??=
                    InventoryFailures.Stacking(
                        $"Snapshot entry '{entry.Source.EntryId}' amount {instance.Amount} exceeds max stack size {maxStack}.");
                return FailPlan(
                    SnapshotIssueCode.StackLimit,
                    failure,
                    out issue,
                    out failure,
                    entry.Source.EntryId,
                    entry.Source.Amount);
            }
            instancesById.Add(entry.Source.EntryId, instance);
            storageIndices.Add(entry.Source.EntryId, index);
        }

        if (!TryValidateExactPlacement(layoutCandidate, entries, instancesById, rootMetadata, out failure))
            return FailPlan(SnapshotIssueCode.Layout, failure!, out issue, out failure);

        if (!_layout.SnapshotCodec.TryCreateExactLayout(
                new InventoryLayoutSnapshotRestoreContext<TKey>(
                    _layout,
                    layoutCandidate,
                    storageIndices,
                    instancesById),
                out var exactLayout,
                out failure) ||
            exactLayout == null)
        {
            return FailPlan(
                SnapshotIssueCode.Layout,
                failure ?? InventoryFailures.Layout("The current layout configuration cannot reproduce the saved placement."),
                out issue,
                out failure);
        }

        var candidate = CreateDetachedCandidate(exactLayout, rootMetadata);
        foreach (var entry in entries)
            AddCandidateInstanceDirect(candidate, instancesById[entry.Source.EntryId]);

        if (!TryValidateReplacement(candidate, out var validationCode, out failure))
            return FailPlan(validationCode, failure!, out issue, out failure);

        plan = new SnapshotPlan(
            candidate,
            Array.Empty<SnapshotItemLoss>(),
            Array.Empty<SnapshotIssue>());
        return true;
    }

    private bool TryPlanReconciledSnapshot(
        InventorySnapshot snapshot,
        out SnapshotPlan? plan,
        out SnapshotIssue? issue,
        out InventoryFailure? failure)
    {
        plan = null;
        issue = null;
        if (!TryResolveSnapshot(
                snapshot,
                allowUnknownDefinitions: false,
                out var entries,
                out var rootMetadata,
                out _,
                out issue,
                out failure))
            return false;

        var candidate = CreateEmptyAutomaticCandidate(rootMetadata);
        foreach (var entry in entries)
        {
            if (!TryAddAutomatically(candidate, entry, entry.Source.Amount, out failure))
            {
                return FailPlan(
                    SnapshotIssueCode.Layout,
                    InventoryFailures.Layout($"Snapshot entry '{entry.Source.EntryId}' could not be reconciled: {failure?.Message}"),
                    out issue,
                    out failure,
                    entry.Source.EntryId,
                    entry.Source.Amount);
            }
        }
        if (!TryValidateReplacement(candidate, out var validationCode, out failure))
            return FailPlan(validationCode, failure!, out issue, out failure);

        plan = new SnapshotPlan(
            candidate,
            Array.Empty<SnapshotItemLoss>(),
            Array.Empty<SnapshotIssue>());
        return true;
    }

    private bool TryPlanSalvagedSnapshot(
        InventorySnapshot snapshot,
        SnapshotSalvageOptions<TKey> options,
        out SnapshotPlan? plan,
        out SnapshotIssue? issue,
        out InventoryFailure? failure)
    {
        plan = null;
        issue = null;
        if (!ValidateSalvageOptions(options, out failure))
            return FailPlan(SnapshotIssueCode.InvalidOptions, failure!, out issue, out failure);

        bool discardUnknown =
            options.UnknownDefinitionHandling == SnapshotUnknownDefinitionHandling.Discard;
        if (!TryResolveSnapshot(
                snapshot,
                discardUnknown,
                out var entries,
                out var rootMetadata,
                out var losses,
                out issue,
                out failure))
            return false;

        var ordered = entries.ToList();
        if (options.PriorityComparer != null)
        {
            try
            {
                ordered = ordered
                    .OrderBy(
                        entry => new SnapshotSalvageCandidate<TKey>(
                            entry.Source.EntryId,
                            entry.Definition,
                            entry.Source.Amount,
                            entry.Metadata),
                        Comparer<SnapshotSalvageCandidate<TKey>>.Create(
                            (left, right) => options.PriorityComparer.Compare(right, left)))
                    .ThenBy(entry => entry.SourceIndex)
                    .ToList();
            }
            catch (Exception ex)
            {
                return FailPlan(
                    SnapshotIssueCode.InvalidOptions,
                    InventoryFailures.Snapshot($"Snapshot salvage priority comparer failed: {ex.Message}"),
                    out issue,
                    out failure);
            }
        }

        var candidate = CreateEmptyAutomaticCandidate(rootMetadata);
        foreach (var entry in ordered)
        {
            int retained = 0;
            InventoryFailure? rejection = null;
            if (options.QuantityMode == SnapshotSalvageQuantityMode.WholeEntryOnly)
            {
                if (TryCandidateWithAdd(candidate, entry, entry.Source.Amount, out var trial, out rejection))
                {
                    candidate = trial!;
                    retained = entry.Source.Amount;
                }
            }
            else
            {
                int low = 1;
                int high = entry.Source.Amount;
                Inventory<TKey>? best = null;
                while (low <= high)
                {
                    int amount = low + (high - low) / 2;
                    if (TryCandidateWithAdd(candidate, entry, amount, out var trial, out rejection))
                    {
                        retained = amount;
                        best = trial;
                        low = amount + 1;
                    }
                    else
                    {
                        high = amount - 1;
                    }
                }
                if (best != null)
                    candidate = best;
            }

            int lost = entry.Source.Amount - retained;
            if (lost > 0)
            {
                losses.Add(new SnapshotItemLoss(
                    entry.Source.EntryId,
                    lost,
                    rejection?.Message ?? "Current inventory constraints could not retain this quantity."));
            }
        }

        if (!TryValidateReplacement(candidate, out var validationCode, out failure))
            return FailPlan(validationCode, failure!, out issue, out failure);

        var issues = losses
            .Select(loss => new SnapshotIssue(
                SnapshotIssueCode.ItemDiscarded,
                loss.Reason,
                loss.EntryId,
                loss.Quantity))
            .ToList();
        plan = new SnapshotPlan(candidate, losses, issues);
        return true;
    }

    private bool TryResolveSnapshot(
        InventorySnapshot snapshot,
        bool allowUnknownDefinitions,
        out List<ResolvedSnapshotEntry> entries,
        out InventoryMetadata rootMetadata,
        out List<SnapshotItemLoss> losses,
        out SnapshotIssue? issue,
        out InventoryFailure? failure)
    {
        entries = new List<ResolvedSnapshotEntry>();
        rootMetadata = new InventoryMetadata();
        losses = new List<SnapshotItemLoss>();
        issue = null;

        if (!InventorySnapshotValidator.TryValidate(snapshot, out failure))
            return FailPlan(SnapshotIssueCode.MalformedSnapshot, failure!, out issue, out failure);

        foreach (var named in snapshot.Metadata)
        {
            if (!InventorySnapshotCodecs.TryDecodeRuntime(named.Value, out var value, out failure) ||
                !rootMetadata.TrySet(named.Name, value, out failure))
            {
                return FailPlan(
                    SnapshotIssueCode.UnsupportedCodec,
                    InventoryFailures.Metadata($"Inventory metadata '{named.Name}' could not be decoded: {failure?.Message}"),
                    out issue,
                    out failure);
            }
        }

        for (int index = 0; index < snapshot.Entries.Count; index++)
        {
            var source = snapshot.Entries[index];
            if (!InventorySnapshotCodecs.TryDecodeKey(source.DefinitionId, out TKey definitionId, out failure))
            {
                return FailPlan(
                    SnapshotIssueCode.UnsupportedCodec,
                    InventoryFailures.SnapshotCodecRejected($"Snapshot entry '{source.EntryId}' definition id could not be decoded: {failure?.Message}"),
                    out issue,
                    out failure,
                    source.EntryId,
                    source.Amount);
            }

            ItemDefinition<TKey> definition;
            try
            {
                definition = Manager.Registry.Resolve(definitionId);
            }
            catch (InvalidOperationException ex)
            {
                if (!allowUnknownDefinitions)
                {
                    return FailPlan(
                        SnapshotIssueCode.UnknownDefinition,
                        InventoryFailures.DefinitionUnresolved(ex.Message),
                        out issue,
                        out failure,
                        source.EntryId,
                        source.Amount);
                }
                losses.Add(new SnapshotItemLoss(source.EntryId, source.Amount, ex.Message));
                continue;
            }

            var metadata = new InstanceMetadata();
            foreach (var named in source.Metadata)
            {
                if (!InventorySnapshotCodecs.TryDecodeRuntime(named.Value, out var value, out failure) ||
                    !metadata.TrySet(named.Name, value, out failure))
                {
                    return FailPlan(
                        SnapshotIssueCode.UnsupportedCodec,
                        InventoryFailures.Metadata($"Snapshot entry '{source.EntryId}' metadata '{named.Name}' could not be decoded: {failure?.Message}"),
                        out issue,
                        out failure,
                        source.EntryId,
                        source.Amount);
                }
            }
            entries.Add(new ResolvedSnapshotEntry(source, definition, metadata, index));
        }

        failure = null;
        return true;
    }

    private bool TryValidateExactPlacement(
        InventoryLayoutSnapshotCandidate<TKey> layoutCandidate,
        IReadOnlyList<ResolvedSnapshotEntry> entries,
        IReadOnlyDictionary<string, ItemInstance<TKey>> instances,
        InventoryMetadata rootMetadata,
        out InventoryFailure? failure)
    {
        var validation = CreateEmptyAutomaticCandidate(rootMetadata);
        for (int storageIndex = 0; storageIndex < entries.Count; storageIndex++)
        {
            var entry = entries[storageIndex];
            if (!layoutCandidate.EntryContexts.TryGetValue(entry.Source.EntryId, out var contexts) ||
                contexts.Count == 0)
            {
                failure = InventoryFailures.Layout($"Snapshot entry '{entry.Source.EntryId}' has no exact layout position.");
                return false;
            }

            ILayoutContext<TKey> placement = contexts[0];
            if (placement is MultiCellGridLayoutContext<TKey>)
            {
                var cells = contexts.Cast<MultiCellGridLayoutContext<TKey>>().ToList();
                placement = new MultiCellGridLayoutContext<TKey>(
                    cells.Min(cell => cell.X),
                    cells.Min(cell => cell.Y),
                    GridAnchor.TopLeft);
            }

            var instance = instances[entry.Source.EntryId];
            if (!validation._layout.CanAcceptNewItem(validation, instance, placement, out failure))
                return false;
            try
            {
                validation._items.Add(instance);
                validation._layout.OnItemAdded(validation, storageIndex, placement);
            }
            catch (Exception ex)
            {
                validation._items.RemoveAt(validation._items.Count - 1);
                failure = InventoryFailures.Layout(ex.Message);
                return false;
            }
        }
        failure = null;
        return true;
    }

    private Inventory<TKey> CreateEmptyAutomaticCandidate(InventoryMetadata? metadata = null)
    {
        var layout = _layout.Clone();
        var candidate = new Inventory<TKey>(
            Manager,
            _stackResolver,
            new UnlimitedCapacityPolicy<TKey>(),
            layout,
            new RuleContainer<TKey>());
        layout.OnInventoryCleared(candidate);
        if (metadata != null)
            candidate.Metadata.ReplaceDirect(metadata);
        return candidate;
    }

    private Inventory<TKey> CreateDetachedCandidate(
        IInventoryLayout<TKey> layout,
        InventoryMetadata? metadata = null)
    {
        var candidate = new Inventory<TKey>(
            Manager,
            _stackResolver,
            new UnlimitedCapacityPolicy<TKey>(),
            layout,
            new RuleContainer<TKey>());
        if (metadata != null)
            candidate.Metadata.ReplaceDirect(metadata);
        return candidate;
    }

    private static void AddCandidateInstanceDirect(Inventory<TKey> candidate, ItemInstance<TKey> instance)
    {
        candidate._items.Add(instance);
        instance.AttachOwner(candidate);
    }

    private static bool TryAddAutomatically(
        Inventory<TKey> candidate,
        ResolvedSnapshotEntry entry,
        int amount,
        out InventoryFailure? failure)
    {
        if (!candidate.TryFormulateAdd(
                entry.Definition,
                amount,
                null,
                entry.Metadata.IsEmpty ? null : entry.Metadata,
                out var transaction,
                out failure) ||
            transaction == null)
            return false;
        candidate.ApplyPreparedTransaction(transaction);
        return true;
    }

    private bool TryCandidateWithAdd(
        Inventory<TKey> candidate,
        ResolvedSnapshotEntry entry,
        int amount,
        out Inventory<TKey>? result,
        out InventoryFailure? failure)
    {
        result = CreateSimulationClone(candidate);
        if (!TryAddAutomatically(result, entry, amount, out failure) ||
            !TryValidateReplacement(result, out _, out failure))
        {
            result = null;
            return false;
        }
        return true;
    }

    private bool TryValidateReplacement(
        Inventory<TKey> candidate,
        out SnapshotIssueCode issueCode,
        out InventoryFailure? failure)
    {
        foreach (var item in candidate._items)
        {
            if (!candidate.TryResolveMaxStackSize(item, out int maxStack, out failure) ||
                item.Amount > maxStack)
            {
                issueCode = SnapshotIssueCode.StackLimit;
                failure ??= InventoryFailures.Definition($"Candidate stack '{item.Definition.Id}' amount {item.Amount} exceeds max stack size {maxStack}.");
                return false;
            }
        }

        var removed = candidate._items
            .Select((item, index) => (index, item))
            .ToList();
        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>();
        for (int index = 0; index < candidate._items.Count; index++)
        {
            candidate._layout.TryGetContextForStorageIndex(candidate, index, out var context);
            added.Add((candidate._items[index], context));
        }
        var replacement = new InventoryTransaction<TKey>(
            candidate,
            new List<(int index, int delta)>(),
            removed,
            added);
        if (!candidate.TryValidateTransactionDefinitions(replacement, out failure))
        {
            issueCode = SnapshotIssueCode.UnknownDefinition;
            return false;
        }
        var normalized = candidate.GenerateNormalizedInventoryTransaction(replacement);
        if (!_capacityPolicy.CanApply(candidate, normalized, out failure))
        {
            issueCode = SnapshotIssueCode.Capacity;
            return false;
        }
        if (!_rules.CanApply(candidate, normalized, replacement, out failure))
        {
            issueCode = SnapshotIssueCode.Rule;
            return false;
        }
        if (!candidate._layout.CanSatisfyPlacement(candidate, replacement, out failure))
        {
            issueCode = SnapshotIssueCode.Layout;
            return false;
        }

        issueCode = default;
        failure = null;
        return true;
    }

    private SnapshotApplicationResult ApplySnapshotPlan(
        SnapshotPlan plan,
        SnapshotApplicationMode mode,
        SnapshotApplicationOutcome outcome)
    {
        var removedEvents = new List<ItemRemoved<TKey>>(_items.Count);
        for (int index = 0; index < _items.Count; index++)
        {
            removedEvents.Add(new ItemRemoved<TKey>(
                _items[index],
                index,
                _layout.GetContextsForStorageIndex(this, index)));
        }

        foreach (var item in _items)
            item.DetachOwner(this);
        _items.Clear();

        var sourceCandidate = plan.Candidate;
        var beforeMetadata = Metadata.Clone();
        Metadata.ReplaceDirect(sourceCandidate.Metadata);
        InventoryMetadataChanged? inventoryMetadataChanged =
            beforeMetadata.StructuralEquals(Metadata)
                ? null
                : new InventoryMetadataChanged(beforeMetadata, Metadata);
        _layout = sourceCandidate._layout;
        for (int index = 0; index < sourceCandidate._items.Count; index++)
        {
            var item = sourceCandidate._items[index];
            item.DetachOwner(sourceCandidate);
            _items.Add(item);
            item.AttachOwner(this);
        }
        sourceCandidate._items.Clear();

        var addedEvents = new List<ItemAdded<TKey>>(_items.Count);
        for (int index = 0; index < _items.Count; index++)
        {
            addedEvents.Add(new ItemAdded<TKey>(
                _items[index],
                index,
                _layout.GetContextsForStorageIndex(this, index)));
        }

        if (removedEvents.Count > 0 || addedEvents.Count > 0 || inventoryMetadataChanged != null)
        {
            Changed?.Invoke(this, new InventoryChangedEventArgs<TKey>(
                added: addedEvents,
                removed: removedEvents,
                inventoryMetadataChanged: inventoryMetadataChanged,
                origin: mode switch
                {
                    SnapshotApplicationMode.Exact => InventoryChangeOrigin.SnapshotExactRestore,
                    SnapshotApplicationMode.Reconcile => InventoryChangeOrigin.SnapshotReconciliation,
                    _ => InventoryChangeOrigin.SnapshotSalvage
                }));
        }

        return new SnapshotApplicationResult(
            mode,
            outcome,
            plan.Losses,
            plan.Issues,
            _items.Count,
            _items.Sum(item => item.Amount));
    }

    private static bool ValidateSalvageOptions(
        SnapshotSalvageOptions<TKey> options,
        out InventoryFailure? failure)
    {
        if (!Enum.IsDefined(typeof(SnapshotSalvageQuantityMode), options.QuantityMode) ||
            !Enum.IsDefined(typeof(SnapshotUnknownDefinitionHandling), options.UnknownDefinitionHandling) ||
            !Enum.IsDefined(typeof(SnapshotSalvagePlacementStrategy), options.PlacementStrategy))
        {
            failure = InventoryFailures.Snapshot("Snapshot salvage options contain an unsupported enum value.");
            return false;
        }
        if (options.PlacementStrategy != SnapshotSalvagePlacementStrategy.GreedyAutomatic)
        {
            failure = InventoryFailures.Layout("Only deterministic greedy automatic snapshot placement is supported.");
            return false;
        }
        failure = null;
        return true;
    }

    private static bool FailPlan(
        SnapshotIssueCode code,
        InventoryFailure rejection,
        out SnapshotIssue? issue,
        out InventoryFailure? failure,
        string? entryId = null,
        int quantity = 0)
    {
        issue = new SnapshotIssue(code, rejection.Message, entryId, quantity);
        failure = rejection;
        return false;
    }
}
