using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a structural change to an inventory (deltas, removals, additions).
/// Transactions are formulated against an inventory state and committed through the transaction itself.
/// </summary>
/// <remarks>
/// Transactions do not represent layout move or swap operations. Use <see cref="Inventory{TKey}.Move"/> /
/// <see cref="Inventory{TKey}.TryMove"/> and <see cref="Inventory{TKey}.Swap"/> /
/// <see cref="Inventory{TKey}.TrySwap"/> for deliberate item movement.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class InventoryTransaction<TKey>
{
    private enum TransactionMode
    {
        Structural,
        CrossStart,
        Cross
    }

    private enum CrossWorkflow
    {
        None,
        Delta,
        Manual
    }

    private readonly TransactionMode _mode;
    private readonly Inventory<TKey>? _inventory;
    private readonly IReadOnlyList<(int index, int delta)>? _amountDeltas;
    private readonly IReadOnlyList<(int index, ItemInstance<TKey> instance)>? _removed;
    private readonly IReadOnlyList<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>? _added;
    private readonly InventoryTransactionEntry<TKey>? _fromEntry;
    private readonly InventoryTransactionEntry<TKey>? _toEntry;
    private InventoryTransaction<TKey>? _fromTransaction;
    private InventoryTransaction<TKey>? _toTransaction;
    private readonly InventoryTransactionSideBuilder<TKey>? _fromSide;
    private readonly InventoryTransactionSideBuilder<TKey>? _toSide;
    private CrossWorkflow _crossWorkflow;
    private bool _isApplied;

    /// <summary>
    /// Creates a transaction builder seeded with the current inventory state.
    /// </summary>
    /// <param name="inventory">The inventory the transaction will target.</param>
    /// <returns>A transaction builder for <paramref name="inventory"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is <see langword="null"/>.</exception>
    public static InventoryTransactionBuilder<TKey> From(Inventory<TKey> inventory)
    {
        return For(inventory);
    }

    /// <summary>
    /// Starts a cross-inventory transaction from a prepared inventory-local transaction entry.
    /// </summary>
    /// <param name="entry">The first inventory-local transaction entry.</param>
    /// <returns>A builder that must be completed with a second entry through <c>To(...)</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public static InventoryTransaction<TKey> From(InventoryTransactionEntry<TKey> entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        return new InventoryTransaction<TKey>(entry);
    }

    /// <summary>
    /// Creates a local-inventory transaction builder seeded with the current inventory state.
    /// </summary>
    /// <param name="inventory">The inventory the transaction will target.</param>
    /// <returns>A transaction builder for <paramref name="inventory"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is <see langword="null"/>.</exception>
    public static InventoryTransactionBuilder<TKey> For(Inventory<TKey> inventory)
    {
        if (inventory == null)
            throw new ArgumentNullException(nameof(inventory));

        var simulation = Inventory<TKey>.CreateSimulationClone(inventory);
        return new InventoryTransactionBuilder<TKey>(inventory, simulation);
    }

    /// <summary>
    /// Gets the inventory this transaction targets.
    /// </summary>
    public Inventory<TKey> Inventory => _inventory
        ?? throw new InvalidOperationException("Cross-inventory transactions do not expose a single inventory.");

    internal long InventoryVersion { get; }

    /// <summary>
    /// Gets storage-index amount deltas to apply to existing item instances.
    /// </summary>
    public IReadOnlyList<(int index, int delta)> AmountDeltas => _amountDeltas
        ?? throw new InvalidOperationException("Cross-inventory transactions do not expose structural amount deltas.");

    /// <summary>
    /// Gets item instances to remove by storage index.
    /// </summary>
    public IReadOnlyList<(int index, ItemInstance<TKey> instance)> Removed => _removed
        ?? throw new InvalidOperationException("Cross-inventory transactions do not expose structural removals.");

    /// <summary>
    /// Gets item instances to add with optional layout contexts.
    /// </summary>
    public IReadOnlyList<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> Added => _added
        ?? throw new InvalidOperationException("Cross-inventory transactions do not expose structural additions.");

    /// <summary>
    /// Gets the source/from side manual staging builder for a completed cross-inventory transaction.
    /// </summary>
    public InventoryTransactionSideBuilder<TKey> FromSide => _fromSide
        ?? throw new InvalidOperationException("Manual side staging is only available on completed cross-inventory transactions created from inventories.");

    /// <summary>
    /// Gets the target/to side manual staging builder for a completed cross-inventory transaction.
    /// </summary>
    public InventoryTransactionSideBuilder<TKey> ToSide => _toSide
        ?? throw new InvalidOperationException("Manual side staging is only available on completed cross-inventory transactions created from inventories.");

    /// <summary>
    /// Gets whether this transaction has already been committed.
    /// </summary>
    public bool IsApplied => _isApplied;

    /// <summary>
    /// Gets whether this transaction contains no structural changes.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            if (_mode == TransactionMode.Structural)
                return AmountDeltas.Count == 0 && Removed.Count == 0 && Added.Count == 0;
            return _fromTransaction == null && _toTransaction == null;
        }
    }

    internal InventoryTransaction(
        Inventory<TKey> inventory,
        List<(int index, int delta)> amountDeltas,
        List<(int index, ItemInstance<TKey> instance)> removed,
        List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> added)
        : this(inventory, amountDeltas, removed, added, inventory?.Version ?? 0)
    {
    }

    internal InventoryTransaction(
        Inventory<TKey> inventory,
        List<(int index, int delta)> amountDeltas,
        List<(int index, ItemInstance<TKey> instance)> removed,
        List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)> added,
        long inventoryVersion)
    {
        _mode = TransactionMode.Structural;
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _amountDeltas = amountDeltas ?? new List<(int, int)>();
        _removed = removed ?? new List<(int, ItemInstance<TKey>)>();
        _added = added ?? new List<(ItemInstance<TKey>, ILayoutContext<TKey>?)>();
        InventoryVersion = inventoryVersion;
    }

    private InventoryTransaction(InventoryTransactionEntry<TKey> fromEntry)
    {
        _mode = TransactionMode.CrossStart;
        _fromEntry = fromEntry ?? throw new ArgumentNullException(nameof(fromEntry));
    }

    private InventoryTransaction(InventoryTransactionEntry<TKey> fromEntry, InventoryTransactionEntry<TKey> toEntry)
    {
        _mode = TransactionMode.Cross;
        _fromEntry = fromEntry ?? throw new ArgumentNullException(nameof(fromEntry));
        _toEntry = toEntry ?? throw new ArgumentNullException(nameof(toEntry));
        ValidateCrossInventories(fromEntry.Inventory, toEntry.Inventory);
        if (!TryBuildEntry(fromEntry, out _fromTransaction, out var failure)
            || !TryBuildEntry(toEntry, out _toTransaction, out failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Cross-inventory transaction entry application was rejected."));
        _crossWorkflow = CrossWorkflow.Delta;
    }

    private InventoryTransaction(Inventory<TKey> from, Inventory<TKey> to)
    {
        _mode = TransactionMode.Cross;
        ValidateCrossInventories(from, to);
        _fromEntry = new InventoryTransactionEntry<TKey>(from, InventoryItemDelta<TKey>.Create());
        _toEntry = new InventoryTransactionEntry<TKey>(to, InventoryItemDelta<TKey>.Create());
        _fromSide = new InventoryTransactionSideBuilder<TKey>(this, For(from));
        _toSide = new InventoryTransactionSideBuilder<TKey>(this, For(to));
    }

    internal static InventoryTransaction<TKey> CreateCross(Inventory<TKey> from, Inventory<TKey> to) =>
        new(from, to);

    internal void MarkApplied() => _isApplied = true;

    /// <summary>
    /// Completes an entry-started cross-inventory transaction with the second inventory-local entry.
    /// </summary>
    /// <param name="entry">The second inventory-local transaction entry.</param>
    /// <returns>A complete cross-inventory transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This transaction was not created from a single entry.</exception>
    /// <exception cref="InventoryOperationException">The entries cannot form a valid cross-inventory transaction.</exception>
    public InventoryTransaction<TKey> To(InventoryTransactionEntry<TKey> entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        if (_mode != TransactionMode.CrossStart || _fromEntry == null)
            throw new InvalidOperationException("Only entry-started cross-inventory transactions can be completed with To(entry).");

        return new InventoryTransaction<TKey>(_fromEntry, entry);
    }

    /// <summary>Applies inventory-local deltas to a cross-inventory transaction.</summary>
    public InventoryTransaction<TKey> Apply(
        InventoryItemDelta<TKey> fromDelta,
        InventoryItemDelta<TKey> toDelta)
    {
        if (!TryApply(fromDelta, toDelta, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Cross-inventory delta application was rejected."));
        return this;
    }

    /// <summary>Applies one delta to the first inventory and its mirror to the second inventory.</summary>
    public InventoryTransaction<TKey> ApplyMirrored(InventoryItemDelta<TKey> fromDelta)
    {
        if (!TryApplyMirrored(fromDelta, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Mirrored cross-inventory delta application was rejected."));
        return this;
    }

    /// <summary>Attempts to apply one delta to the first inventory and its mirror to the second inventory.</summary>
    public bool TryApplyMirrored(InventoryItemDelta<TKey> fromDelta, out InventoryFailure? failure)
    {
        if (fromDelta == null)
        {
            failure = InventoryFailures.Transaction("Delta cannot be null.");
            return false;
        }

        if (!InventoryItemDelta<TKey>.TryMirror(fromDelta, out var mirrored, out failure) || mirrored == null)
            return false;

        return TryApply(fromDelta, mirrored, out failure);
    }

    /// <summary>Attempts to apply inventory-local deltas to a cross-inventory transaction.</summary>
    public bool TryApply(
        InventoryItemDelta<TKey> fromDelta,
        InventoryItemDelta<TKey> toDelta,
        out InventoryFailure? failure)
    {
        if (_mode != TransactionMode.Cross || _fromEntry == null || _toEntry == null)
        {
            failure = InventoryFailures.Transaction("Only complete cross-inventory transactions can apply two deltas.");
            return false;
        }
        if (_fromTransaction != null || _toTransaction != null)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction already has applied deltas.");
            return false;
        }
        if (_crossWorkflow == CrossWorkflow.Manual)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction already has manual staged operations.");
            return false;
        }

        if (!TryBuildEntry(new InventoryTransactionEntry<TKey>(_fromEntry.Inventory, fromDelta), out _fromTransaction, out failure))
            return false;
        if (!TryBuildEntry(new InventoryTransactionEntry<TKey>(_toEntry.Inventory, toDelta), out _toTransaction, out failure))
        {
            _fromTransaction = null;
            return false;
        }

        _crossWorkflow = CrossWorkflow.Delta;
        failure = null;
        return true;
    }

    /// <summary>Applies inventory-local transaction entries to a cross-inventory transaction.</summary>
    public InventoryTransaction<TKey> Apply(
        InventoryTransactionEntry<TKey> fromEntry,
        InventoryTransactionEntry<TKey> toEntry)
    {
        if (!TryApply(fromEntry, toEntry, out var failure))
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Cross-inventory transaction entry application was rejected."));
        return this;
    }

    /// <summary>Attempts to apply inventory-local transaction entries to a cross-inventory transaction.</summary>
    public bool TryApply(
        InventoryTransactionEntry<TKey> fromEntry,
        InventoryTransactionEntry<TKey> toEntry,
        out InventoryFailure? failure)
    {
        if (_mode != TransactionMode.Cross || _fromEntry == null || _toEntry == null)
        {
            failure = InventoryFailures.Transaction("Only complete cross-inventory transactions can apply two entries.");
            return false;
        }
        if (_fromTransaction != null || _toTransaction != null)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction already has applied deltas.");
            return false;
        }
        if (_crossWorkflow == CrossWorkflow.Manual)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction already has manual staged operations.");
            return false;
        }
        if (fromEntry == null)
        {
            failure = InventoryFailures.Transaction("From transaction entry cannot be null.");
            return false;
        }
        if (toEntry == null)
        {
            failure = InventoryFailures.Transaction("To transaction entry cannot be null.");
            return false;
        }
        if (!ReferenceEquals(fromEntry.Inventory, _fromEntry.Inventory))
        {
            failure = InventoryFailures.Transaction("From transaction entry belongs to another inventory.");
            return false;
        }
        if (!ReferenceEquals(toEntry.Inventory, _toEntry.Inventory))
        {
            failure = InventoryFailures.Transaction("To transaction entry belongs to another inventory.");
            return false;
        }

        if (!TryBuildEntry(fromEntry, out _fromTransaction, out failure))
            return false;
        if (!TryBuildEntry(toEntry, out _toTransaction, out failure))
        {
            _fromTransaction = null;
            return false;
        }

        _crossWorkflow = CrossWorkflow.Delta;
        failure = null;
        return true;
    }

    /// <summary>
    /// Creates an unapplied copy with replacement contexts for the existing added entries.
    /// </summary>
    /// <param name="contexts">
    /// Replacement contexts in the same order as <see cref="Added"/>. The count must match <see cref="Added"/> exactly.
    /// </param>
    /// <returns>
    /// A transaction that preserves the target inventory, amount deltas, removals, and added item instances while
    /// replacing only the added-entry contexts.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contexts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The number of supplied contexts does not match the number of added entries.
    /// </exception>
    /// <exception cref="InvalidOperationException">This transaction has already been applied.</exception>
    /// <remarks>
    /// Custom layouts can use this method from
    /// <see cref="IInventoryLayout{TKey}.TryApplyPlacementContext(Inventory{TKey}, InventoryTransaction{TKey}, ILayoutContext{TKey}?, out InventoryTransaction{TKey}?, out InventoryFailure?)"/>
    /// after validating a transaction-level placement context. It intentionally does not allow layouts to replace the
    /// transaction's structural item changes.
    /// </remarks>
    public InventoryTransaction<TKey> WithAddedEntryContexts(
        IReadOnlyList<ILayoutContext<TKey>?> contexts)
    {
        if (contexts == null)
            throw new ArgumentNullException(nameof(contexts));
        if (IsApplied)
            throw new InvalidOperationException("Applied transactions cannot be copied with replacement added-entry contexts.");
        if (contexts.Count != Added.Count)
        {
            throw new ArgumentException(
                $"Expected {Added.Count} added-entry contexts, but received {contexts.Count}.",
                nameof(contexts));
        }

        var added = new List<(ItemInstance<TKey> instance, ILayoutContext<TKey>? context)>(Added.Count);
        for (int i = 0; i < Added.Count; i++)
            added.Add((Added[i].instance, contexts[i]));

        return new InventoryTransaction<TKey>(
            Inventory,
            new List<(int index, int delta)>(AmountDeltas),
            new List<(int index, ItemInstance<TKey> instance)>(Removed),
            added,
            InventoryVersion);
    }

    /// <summary>
    /// Creates a new transaction with the same structural data but targeting a different inventory.
    /// Used when committing a transaction built against a simulation to the real inventory.
    /// </summary>
    /// <param name="target">The inventory the copied transaction should target.</param>
    /// <returns>A transaction with copied structural data targeting <paramref name="target"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public InventoryTransaction<TKey> ForInventory(Inventory<TKey> target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        return new InventoryTransaction<TKey>(
            target,
            new List<(int, int)>(AmountDeltas),
            new List<(int, ItemInstance<TKey>)>(Removed),
            new List<(ItemInstance<TKey>, ILayoutContext<TKey>?)>(Added));
    }

    /// <summary>
    /// Evaluates whether this transaction can be committed against the current inventory state.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction can currently be committed; otherwise, <see langword="false"/>.</returns>
    public bool Validate(out InventoryFailure? failure) =>
        _mode == TransactionMode.Structural
            ? Inventory.CanCommitTransaction(this, out failure)
            : ValidateCross(out failure);

    /// <summary>
    /// Attempts to commit this transaction through its owning inventory.
    /// </summary>
    /// <param name="failure">A consumer-facing reason when commit is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transaction commits; otherwise, <see langword="false"/>.</returns>
    public bool TryCommit(out InventoryFailure? failure) =>
        _mode == TransactionMode.Structural
            ? Inventory.TryCommitTransaction(this, out failure)
            : TryCommitCross(out failure);

    /// <summary>
    /// Commits this transaction or throws when expected-success commit is rejected.
    /// </summary>
    /// <exception cref="InventoryOperationException">The transaction is rejected.</exception>
    public void Commit() =>
        ThrowIfCommitRejected(TryCommit(out var failure), failure);

    private bool ValidateCross(out InventoryFailure? failure)
    {
        if (!TryPrepareCross(out _, out _, out failure))
            return false;
        failure = null;
        return true;
    }

    private bool TryCommitCross(out InventoryFailure? failure)
    {
        if (!TryPrepareCross(out var fromPrepared, out var toPrepared, out failure)
            || fromPrepared == null
            || toPrepared == null)
            return false;

        var fromInventory = _fromEntry!.Inventory;
        var toInventory = _toEntry!.Inventory;
        var fromEvent = fromInventory.ApplyPreparedTransactionDeferred(fromPrepared);
        var toEvent = toInventory.ApplyPreparedTransactionDeferred(toPrepared);
        MarkApplied();
        fromInventory.PublishChange(fromEvent);
        toInventory.PublishChange(toEvent);
        failure = null;
        return true;
    }

    private bool TryPrepareCross(
        out InventoryTransaction<TKey>? fromPrepared,
        out InventoryTransaction<TKey>? toPrepared,
        out InventoryFailure? failure)
    {
        fromPrepared = null;
        toPrepared = null;
        if (_mode == TransactionMode.CrossStart)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction is incomplete. Call To(...) before validating or committing.");
            return false;
        }
        if (_mode != TransactionMode.Cross || _fromEntry == null || _toEntry == null)
        {
            failure = InventoryFailures.Transaction("Transaction is not a cross-inventory transaction.");
            return false;
        }
        if (IsApplied)
        {
            failure = InventoryFailures.Transaction("Transaction has already been applied.");
            return false;
        }
        InventoryTransaction<TKey>? fromTransaction = _fromTransaction;
        InventoryTransaction<TKey>? toTransaction = _toTransaction;
        if (fromTransaction == null || toTransaction == null)
        {
            if (_crossWorkflow == CrossWorkflow.Manual && _fromSide != null && _toSide != null)
            {
                fromTransaction = _fromSide.Build();
                toTransaction = _toSide.Build();
            }
            else
            {
                failure = InventoryFailures.Transaction("Cross-inventory transaction has no staged operations.");
                return false;
            }
        }

        if (_crossWorkflow == CrossWorkflow.Manual && fromTransaction.IsEmpty && toTransaction.IsEmpty)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction has no staged operations.");
            return false;
        }

        if (!_fromEntry.Inventory.TryPrepareTransaction(fromTransaction, null, out fromPrepared, out failure))
            return false;
        if (!_toEntry.Inventory.TryPrepareTransaction(toTransaction, null, out toPrepared, out failure))
        {
            fromPrepared = null;
            return false;
        }

        failure = null;
        return true;
    }

    internal bool TryCanStageManualCrossOperation(out InventoryFailure? failure)
    {
        if (_mode != TransactionMode.Cross || _fromEntry == null || _toEntry == null || _fromSide == null || _toSide == null)
        {
            failure = InventoryFailures.Transaction("Manual side staging is only available on completed cross-inventory transactions created from inventories.");
            return false;
        }
        if (IsApplied)
        {
            failure = InventoryFailures.Transaction("Transaction has already been applied.");
            return false;
        }
        if (_crossWorkflow == CrossWorkflow.Delta || _fromTransaction != null || _toTransaction != null)
        {
            failure = InventoryFailures.Transaction("Cross-inventory transaction already has applied deltas.");
            return false;
        }

        failure = null;
        return true;
    }

    internal void MarkManualCrossStaged()
    {
        _crossWorkflow = CrossWorkflow.Manual;
        _fromTransaction = null;
        _toTransaction = null;
    }

    private static bool TryBuildEntry(
        InventoryTransactionEntry<TKey> entry,
        out InventoryTransaction<TKey>? transaction,
        out InventoryFailure? failure)
    {
        transaction = null;
        if (entry == null)
        {
            failure = InventoryFailures.Transaction("Transaction entry cannot be null.");
            return false;
        }
        var builder = InventoryTransaction<TKey>.For(entry.Inventory);
        if (!builder.TryApply(entry.Delta, entry.Plan, out failure))
            return false;
        transaction = builder.Build();
        failure = null;
        return true;
    }

    private static void ValidateCrossInventories(Inventory<TKey> from, Inventory<TKey> to)
    {
        if (from == null)
            throw new ArgumentNullException(nameof(from));
        if (to == null)
            throw new ArgumentNullException(nameof(to));
        if (ReferenceEquals(from, to))
            throw new InvalidOperationException("Cross-inventory transactions require two different inventories.");
        if (!ReferenceEquals(from.Catalog, to.Catalog))
            throw new InvalidOperationException("Cross-inventory transactions require inventories that share the same item catalog.");
    }

    private static void ThrowIfCommitRejected(bool accepted, InventoryFailure? failure)
    {
        if (!accepted)
            throw new InventoryOperationException(failure ?? InventoryFailures.Transaction("Transaction was rejected."));
    }
}
