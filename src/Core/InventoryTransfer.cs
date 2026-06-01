using System;
using System.Collections.Generic;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Tags;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Provides helpers for moving item amounts between compatible inventories.
/// </summary>
public static class InventoryTransfer
{
    private sealed class InventoryTransferPlan<TKey>
    {
        public InventoryTransferPlan(
            Inventory<TKey> source,
            Inventory<TKey> target,
            InventoryTransaction<TKey> sourceTransaction,
            InventoryTransaction<TKey> targetTransaction,
            IReadOnlyList<InventoryTransferEntry<TKey>> entries)
        {
            Source = source;
            Target = target;
            SourceTransaction = sourceTransaction;
            TargetTransaction = targetTransaction;
            Entries = entries;
        }

        public Inventory<TKey> Source { get; }
        public Inventory<TKey> Target { get; }
        public InventoryTransaction<TKey> SourceTransaction { get; }
        public InventoryTransaction<TKey> TargetTransaction { get; }
        public IReadOnlyList<InventoryTransferEntry<TKey>> Entries { get; }
    }

    /// <summary>
    /// Creates an outgoing-only transfer builder for a source inventory.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="source">The source inventory items will leave.</param>
    /// <returns>A transfer builder for <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static InventoryTransferBuilder<TKey> From<TKey>(Inventory<TKey> source)
    {
        return new InventoryTransferBuilder<TKey>(source);
    }

    /// <summary>
    /// Attempts to transfer an amount from one inventory to another.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="source">The inventory that currently owns the item instance.</param>
    /// <param name="target">The inventory that should receive the transferred amount.</param>
    /// <param name="item">The source item instance to transfer from.</param>
    /// <param name="amount">The amount to transfer.</param>
    /// <param name="targetContext">Optional target layout context.</param>
    /// <param name="error">A consumer-facing reason when the transfer is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryTransfer<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (target == null)
        {
            error = "Target inventory cannot be null.";
            return false;
        }
        if (item == null)
        {
            error = "Item cannot be null.";
            return false;
        }
        if (ReferenceEquals(source, target))
        {
            error = "Cannot transfer between the same inventory.";
            return false;
        }
        if (amount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }
        if (!ReferenceEquals(source.Manager, target.Manager) && !ReferenceEquals(source.Catalog, target.Catalog))
        {
            error = "Inventories must share the same item catalog.";
            return false;
        }

        bool sourceContainsItem = false;
        foreach (var sourceItem in source.Items)
        {
            if (ReferenceEquals(sourceItem, item))
            {
                sourceContainsItem = true;
                break;
            }
        }

        if (!sourceContainsItem)
        {
            error = "Item not found in source inventory.";
            return false;
        }
        if (item.Amount < amount)
        {
            error = "Not enough quantity to transfer.";
            return false;
        }

        var builder = new InventoryTransferBuilder<TKey>(source);
        if (!builder.TryTake(item, amount, out error))
            return false;

        return TryTransfer(builder, target, targetContext, out error);
    }

    /// <summary>
    /// Attempts to commit a transfer builder into a target inventory using one target context for every entry.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="builder">The outgoing transfer builder.</param>
    /// <param name="target">The inventory that should receive the entries.</param>
    /// <param name="targetContext">Optional target layout context used for every entry.</param>
    /// <param name="error">A consumer-facing reason when the transfer is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full transfer succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryTransfer<TKey>(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        return TryTransfer(builder, target, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Attempts to commit a transfer builder into a target inventory using per-entry target contexts.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="builder">The outgoing transfer builder.</param>
    /// <param name="target">The inventory that should receive the entries.</param>
    /// <param name="targetContextSelector">Optional selector for each entry's target layout context.</param>
    /// <param name="error">A consumer-facing reason when the transfer is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full transfer succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryTransfer<TKey>(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        if (!TryCreateTransferPlan(builder, target, targetContextSelector, out var plan, out error) || plan == null)
            return false;

        return TryCommitPlan(plan, out error);
    }

    /// <summary>
    /// Evaluates whether a single-item transfer can succeed without mutating either inventory.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="source">The inventory that currently owns the item instance.</param>
    /// <param name="target">The inventory that would receive the item amount.</param>
    /// <param name="item">The source item instance to evaluate.</param>
    /// <param name="amount">The amount to evaluate.</param>
    /// <param name="targetContext">Optional target layout context.</param>
    /// <param name="error">A consumer-facing reason when the transfer would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer can succeed; otherwise, <see langword="false"/>.</returns>
    public static bool CanTransfer<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int amount,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }

        var builder = new InventoryTransferBuilder<TKey>(source);
        if (!builder.TryTake(item, amount, out error))
            return false;

        return CanTransfer(builder, target, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Evaluates whether a transfer builder can be committed without mutating either inventory.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="builder">The outgoing transfer builder.</param>
    /// <param name="target">The inventory that would receive the entries.</param>
    /// <param name="targetContextSelector">Optional selector for each entry's target layout context.</param>
    /// <param name="error">A consumer-facing reason when the transfer would be rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transfer can succeed; otherwise, <see langword="false"/>.</returns>
    public static bool CanTransfer<TKey>(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        return TryCreateTransferPlan(builder, target, targetContextSelector, out _, out error);
    }

    /// <summary>
    /// Attempts to swap complete item stacks between two compatible inventories.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="first">The first inventory.</param>
    /// <param name="second">The second inventory.</param>
    /// <param name="firstItem">The item leaving the first inventory.</param>
    /// <param name="secondItem">The item leaving the second inventory.</param>
    /// <param name="firstTargetContext">Optional context where <paramref name="secondItem"/> lands in <paramref name="first"/>.</param>
    /// <param name="secondTargetContext">Optional context where <paramref name="firstItem"/> lands in <paramref name="second"/>.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full swap succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TrySwap<TKey>(
        Inventory<TKey> first,
        Inventory<TKey> second,
        ItemInstance<TKey> firstItem,
        ItemInstance<TKey> secondItem,
        ILayoutContext<TKey>? firstTargetContext,
        ILayoutContext<TKey>? secondTargetContext,
        out string? error)
    {
        if (firstItem == null)
        {
            error = "First item cannot be null.";
            return false;
        }
        if (secondItem == null)
        {
            error = "Second item cannot be null.";
            return false;
        }

        return TrySwap(first, second, firstItem!, firstItem.Amount, secondItem!, secondItem.Amount, firstTargetContext, secondTargetContext, out error);
    }

    /// <summary>
    /// Attempts to swap item amounts between two compatible inventories.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="first">The first inventory.</param>
    /// <param name="second">The second inventory.</param>
    /// <param name="firstItem">The item leaving the first inventory.</param>
    /// <param name="firstAmount">The amount to move from the first inventory.</param>
    /// <param name="secondItem">The item leaving the second inventory.</param>
    /// <param name="secondAmount">The amount to move from the second inventory.</param>
    /// <param name="firstTargetContext">Optional context where <paramref name="secondItem"/> lands in <paramref name="first"/>.</param>
    /// <param name="secondTargetContext">Optional context where <paramref name="firstItem"/> lands in <paramref name="second"/>.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the full swap succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TrySwap<TKey>(
        Inventory<TKey> first,
        Inventory<TKey> second,
        ItemInstance<TKey> firstItem,
        int firstAmount,
        ItemInstance<TKey> secondItem,
        int secondAmount,
        ILayoutContext<TKey>? firstTargetContext,
        ILayoutContext<TKey>? secondTargetContext,
        out string? error)
    {
        if (!TryValidateCompatibility(first, second, out error))
            return false;
        if (firstItem == null)
        {
            error = "First item cannot be null.";
            return false;
        }
        if (secondItem == null)
        {
            error = "Second item cannot be null.";
            return false;
        }
        if (firstAmount <= 0 || secondAmount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }

        var firstEntry = new InventoryTransferEntry<TKey>(firstItem.Definition, firstAmount, CloneMetadataOrNull(firstItem.Metadata), firstItem);
        var secondEntry = new InventoryTransferEntry<TKey>(secondItem.Definition, secondAmount, CloneMetadataOrNull(secondItem.Metadata), secondItem);

        if (!TryCreateInventoryExchangeTransaction(first, new[] { firstEntry }, new[] { secondEntry }, (_, _) => firstTargetContext, out var firstTx, out error))
            return false;
        if (!TryCreateInventoryExchangeTransaction(second, new[] { secondEntry }, new[] { firstEntry }, (_, _) => secondTargetContext, out var secondTx, out error))
            return false;

        first.CommitTransaction(firstTx!);
        second.CommitTransaction(secondTx!);
        return true;
    }

    /// <summary>
    /// Attempts to swap all contents between two compatible inventories.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="first">The first inventory.</param>
    /// <param name="second">The second inventory.</param>
    /// <param name="firstTargetContext">Optional context used for every item entering the first inventory.</param>
    /// <param name="secondTargetContext">Optional context used for every item entering the second inventory.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the inventory swap succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TrySwapInventories<TKey>(
        Inventory<TKey> first,
        Inventory<TKey> second,
        ILayoutContext<TKey>? firstTargetContext,
        ILayoutContext<TKey>? secondTargetContext,
        out string? error)
    {
        return TrySwapInventories(first, second, (_, _) => firstTargetContext, (_, _) => secondTargetContext, out error);
    }

    /// <summary>
    /// Attempts to swap all contents between two compatible inventories using per-entry target contexts.
    /// </summary>
    /// <typeparam name="TKey">The item definition identifier type.</typeparam>
    /// <param name="first">The first inventory.</param>
    /// <param name="second">The second inventory.</param>
    /// <param name="firstTargetContextSelector">Optional selector for entries entering the first inventory.</param>
    /// <param name="secondTargetContextSelector">Optional selector for entries entering the second inventory.</param>
    /// <param name="error">A consumer-facing reason when the swap is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the inventory swap succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TrySwapInventories<TKey>(
        Inventory<TKey> first,
        Inventory<TKey> second,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? firstTargetContextSelector,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? secondTargetContextSelector,
        out string? error)
    {
        if (!TryValidateCompatibility(first, second, out error))
            return false;

        var firstEntries = BuildAllEntries(first);
        var secondEntries = BuildAllEntries(second);
        if (firstEntries.Count == 0 && secondEntries.Count == 0)
        {
            error = null;
            return true;
        }

        if (!TryCreateInventoryExchangeTransaction(first, firstEntries, secondEntries, firstTargetContextSelector, out var firstTx, out error))
            return false;
        if (!TryCreateInventoryExchangeTransaction(second, secondEntries, firstEntries, secondTargetContextSelector, out var secondTx, out error))
            return false;

        first.CommitTransaction(firstTx!);
        second.CommitTransaction(secondTx!);
        return true;
    }

    /// <summary>
    /// Attempts to move every item from a source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveAll<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        return TryMoveAll(source, target, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Attempts to move every item from a source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveAll<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        return TryMoveWhere(source, target, _ => true, targetContextSelector, out error);
    }

    /// <summary>
    /// Attempts to move every matching item from a source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveWhere<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        Func<ItemInstance<TKey>, bool> predicate,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        return TryMoveWhere(source, target, predicate, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Attempts to move every matching item from a source inventory to a target inventory as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveWhere<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        Func<ItemInstance<TKey>, bool> predicate,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (predicate == null)
        {
            error = "Predicate cannot be null.";
            return false;
        }

        var builder = new InventoryTransferBuilder<TKey>(source);
        foreach (var item in new List<ItemInstance<TKey>>(source.Items))
        {
            if (predicate(item) && !builder.TryTake(item, item.Amount, out error))
                return false;
        }

        return TryTransfer(builder, target, targetContextSelector, out error);
    }

    /// <summary>
    /// Attempts to move every item with a catalog-resolved tag as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveByTag<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        TagKey tag,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        return TryMoveByTag(source, target, tag, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Attempts to move every item with a catalog-resolved tag as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveByTag<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        TagKey tag,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (tag == null)
        {
            error = "Tag cannot be null.";
            return false;
        }

        return TryMoveWhere(source, target, item => source.Catalog.Satisfies(item.Definition, tag), targetContextSelector, out error);
    }

    /// <summary>
    /// Attempts to move every item satisfying all catalog-resolved tags as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveAllTags<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        TagKey[] tags,
        ILayoutContext<TKey>? targetContext,
        out string? error)
    {
        return TryMoveAllTags(source, target, tags, (_, _) => targetContext, out error);
    }

    /// <summary>
    /// Attempts to move every item satisfying all catalog-resolved tags as one all-or-nothing operation.
    /// </summary>
    public static bool TryMoveAllTags<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        TagKey[] tags,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (tags == null || tags.Length == 0)
        {
            error = "At least one tag is required.";
            return false;
        }
        foreach (var tag in tags)
        {
            if (tag == null)
            {
                error = "Tags cannot contain null.";
                return false;
            }
        }

        return TryMoveWhere(
            source,
            target,
            item =>
            {
                foreach (var tag in tags)
                {
                    if (!source.Catalog.Satisfies(item.Definition, tag))
                        return false;
                }
                return true;
            },
            targetContextSelector,
            out error);
    }

    /// <summary>
    /// Attempts to transfer the largest valid amount up to a requested amount.
    /// </summary>
    public static bool TryTransferMaximum<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        ItemInstance<TKey> item,
        int requestedAmount,
        ILayoutContext<TKey>? targetContext,
        out int transferredAmount,
        out string? error)
    {
        transferredAmount = 0;
        if (requestedAmount <= 0)
        {
            error = "Amount must be greater than zero.";
            return false;
        }
        if (item == null)
        {
            error = "Item cannot be null.";
            return false;
        }

        int low = 1;
        int high = Math.Min(requestedAmount, item.Amount);
        int best = 0;
        string? lastError = null;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (CanTransfer(source, target, item, mid, targetContext, out lastError))
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (best <= 0)
        {
            transferredAmount = 0;
            error = lastError;
            return false;
        }

        if (!TryTransfer(source, target, item, best, targetContext, out error))
        {
            transferredAmount = 0;
            return false;
        }

        transferredAmount = best;
        return true;
    }

    /// <summary>
    /// Attempts to move as much matching item amount as possible in source storage order.
    /// </summary>
    public static bool TryMoveMaximumWhere<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        Func<ItemInstance<TKey>, bool> predicate,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out int transferredAmount,
        out string? error)
    {
        transferredAmount = 0;
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (predicate == null)
        {
            error = "Predicate cannot be null.";
            return false;
        }

        string? lastError = null;
        int entryIndex = 0;
        foreach (var item in new List<ItemInstance<TKey>>(source.Items))
        {
            if (!predicate(item))
                continue;

            var entry = new InventoryTransferEntry<TKey>(item.Definition, item.Amount, CloneMetadataOrNull(item.Metadata), item);
            var context = targetContextSelector != null ? targetContextSelector(entry, entryIndex) : null;
            entryIndex++;
            if (TryTransferMaximum(source, target, item, item.Amount, context, out var moved, out lastError))
                transferredAmount += moved;
        }

        error = transferredAmount > 0 ? null : lastError ?? "Transfer contains no items.";
        return transferredAmount > 0;
    }

    /// <summary>
    /// Attempts to move as much item amount with a catalog-resolved tag as possible in source storage order.
    /// </summary>
    public static bool TryMoveMaximumByTag<TKey>(
        Inventory<TKey> source,
        Inventory<TKey> target,
        TagKey tag,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out int transferredAmount,
        out string? error)
    {
        transferredAmount = 0;
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (tag == null)
        {
            error = "Tag cannot be null.";
            return false;
        }

        return TryMoveMaximumWhere(source, target, item => source.Catalog.Satisfies(item.Definition, tag), targetContextSelector, out transferredAmount, out error);
    }

    private static bool TryValidateCompatibility<TKey>(Inventory<TKey> source, Inventory<TKey> target, out string? error)
    {
        if (source == null)
        {
            error = "Source inventory cannot be null.";
            return false;
        }
        if (target == null)
        {
            error = "Target inventory cannot be null.";
            return false;
        }
        if (ReferenceEquals(source, target))
        {
            error = "Cannot transfer between the same inventory.";
            return false;
        }
        if (!ReferenceEquals(source.Manager, target.Manager) && !ReferenceEquals(source.Catalog, target.Catalog))
        {
            error = "Inventories must share the same item catalog.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryCreateTransferPlan<TKey>(
        InventoryTransferBuilder<TKey> builder,
        Inventory<TKey> target,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? targetContextSelector,
        out InventoryTransferPlan<TKey>? plan,
        out string? error)
    {
        plan = null;
        if (builder == null)
        {
            error = "Transfer builder cannot be null.";
            return false;
        }
        if (!TryValidateCompatibility(builder.Source, target, out error))
            return false;

        var sourceTransaction = builder.ToSourceTransaction();
        var entries = InventoryTransferBuilder<TKey>.BuildEntries(sourceTransaction);
        if (entries.Count == 0)
        {
            error = "Transfer contains no items.";
            return false;
        }

        var targetBuilder = target.CreateTransactionBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var context = targetContextSelector != null ? targetContextSelector(entry, i) : null;
            if (!targetBuilder.TryAdd(entry.Definition, entry.Amount, context, CloneMetadataOrNull(entry.Metadata), out error))
                return false;
        }

        plan = new InventoryTransferPlan<TKey>(
            builder.Source,
            target,
            sourceTransaction,
            targetBuilder.ToInventoryTransaction(),
            entries);
        error = null;
        return true;
    }

    private static bool TryCommitPlan<TKey>(InventoryTransferPlan<TKey> plan, out string? error)
    {
        error = null;
        plan.Source.CommitTransaction(plan.SourceTransaction);
        plan.Target.CommitTransaction(plan.TargetTransaction);
        return true;
    }

    private static bool TryCreateInventoryExchangeTransaction<TKey>(
        Inventory<TKey> inventory,
        IReadOnlyList<InventoryTransferEntry<TKey>> outgoing,
        IReadOnlyList<InventoryTransferEntry<TKey>> incoming,
        Func<InventoryTransferEntry<TKey>, int, ILayoutContext<TKey>?>? incomingContextSelector,
        out InventoryTransaction<TKey>? transaction,
        out string? error)
    {
        transaction = null;
        var builder = inventory.CreateTransactionBuilder();

        foreach (var entry in outgoing)
        {
            if (!builder.TryRemove(entry.SourceInstance, out error, entry.Amount))
                return false;
        }

        for (int i = 0; i < incoming.Count; i++)
        {
            var entry = incoming[i];
            var context = incomingContextSelector != null ? incomingContextSelector(entry, i) : null;
            if (!builder.TryAdd(entry.Definition, entry.Amount, context, CloneMetadataOrNull(entry.Metadata), out error))
                return false;
        }

        transaction = builder.ToInventoryTransaction();
        error = null;
        return true;
    }

    private static IReadOnlyList<InventoryTransferEntry<TKey>> BuildAllEntries<TKey>(Inventory<TKey> inventory)
    {
        var entries = new List<InventoryTransferEntry<TKey>>();
        foreach (var item in inventory.Items)
        {
            entries.Add(new InventoryTransferEntry<TKey>(
                item.Definition,
                item.Amount,
                CloneMetadataOrNull(item.Metadata),
                item));
        }

        return entries;
    }

    private static InstanceMetadata? CloneMetadataOrNull(InstanceMetadata? source)
    {
        if (source == null || source.IsEmpty)
            return null;

        var clone = new InstanceMetadata();
        clone.RestoreMetadata(new Dictionary<string, object>(source.ToDictionary()));
        return clone;
    }
}
