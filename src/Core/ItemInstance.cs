using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a concrete amount of an item definition owned by an inventory.
/// </summary>
/// <remarks>
/// Item instances are readable stack handles created by inventories, transactions, transfers, deserialization, and
/// internal rebuild flows. Callers should mutate item amounts through inventory-owned APIs rather than constructing
/// or editing instances directly.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class ItemInstance<TKey>
{
    /// <summary>
    /// Gets the item definition represented by this instance.
    /// </summary>
    public ItemDefinition<TKey> Definition { get; }

    /// <summary>
    /// Gets the amount contained in this instance or stack.
    /// </summary>
    /// <remarks>Amount changes are controlled by inventory, transaction, transfer, split, merge, remove, and rebuild operations.</remarks>
    public int Amount { get; private set; }

    /// <summary>
    /// Gets the unique runtime identifier for this instance.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the per-instance metadata for this item.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Once the item belongs to an inventory,
    /// metadata mutations validate through that inventory and can fire
    /// <see cref="Inventory{TKey}.Changed"/>.
    /// </remarks>
    public InstanceMetadata Metadata { get; } = new();

    internal Inventory<TKey>? Owner { get; private set; }

    /// <summary>
    /// Creates an item instance for inventory-owned internal workflows.
    /// </summary>
    /// <param name="definition">The item definition represented by this instance.</param>
    /// <param name="amount">The amount stored in this instance.</param>
    /// <param name="metadata">Optional per-instance metadata.</param>
    /// <remarks>The provided metadata object is stored by reference.</remarks>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is less than or equal to zero.</exception>
    internal ItemInstance(ItemDefinition<TKey> definition, int amount = 1, InstanceMetadata? metadata = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        Definition = definition;
        Amount = amount;
        InstanceId = Guid.NewGuid();
        Metadata = metadata ?? new InstanceMetadata();
    }

    /// <summary>
    /// Attempts to split this stack and set metadata on the split amount.
    /// </summary>
    /// <param name="amount">The amount to split and mutate.</param>
    /// <param name="key">The metadata key to add or replace.</param>
    /// <param name="value">The metadata value.</param>
    /// <param name="metadataStack">The stack that received the metadata when the operation succeeds.</param>
    /// <param name="error">A consumer-facing reason when the operation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the operation succeeds; otherwise, <see langword="false"/>.</returns>
    /// <remarks>This operation is routed through the owning inventory and fails when the instance is not inventory-owned.</remarks>
    public bool TrySplitAndSetMetadata(
        int amount,
        string key,
        object? value,
        out ItemInstance<TKey>? metadataStack,
        out InventoryFailure? error)
    {
        metadataStack = null;
        if (Owner == null)
        {
            error = "Item instance does not belong to an inventory.";
            return false;
        }

        return Owner.TrySplitAndSetMetadata(this, amount, key, value, out metadataStack, out error);
    }

    /// <summary>
    /// Splits this stack and sets metadata on the split amount, or throws when the operation is rejected.
    /// </summary>
    /// <param name="amount">The amount to split and mutate.</param>
    /// <param name="key">The metadata key to add or replace.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The stack that received the metadata.</returns>
    /// <exception cref="InvalidOperationException">The split or metadata mutation is rejected.</exception>
    /// <remarks>This operation is routed through the owning inventory.</remarks>
    public ItemInstance<TKey> SplitAndSetMetadata(int amount, string key, object? value)
    {
        if (!TrySplitAndSetMetadata(amount, key, value, out var metadataStack, out var error) || metadataStack == null)
            throw new InventoryOperationException(error ?? InventoryFailure.FromMessage(null));

        return metadataStack;
    }

    /// <summary>
    /// Replaces the amount stored in this instance.
    /// </summary>
    /// <param name="amount">The new amount.</param>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is less than or equal to zero.</exception>
    internal void SetAmount(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        Amount = amount;
    }

    /// <summary>
    /// Adds an amount to this instance.
    /// </summary>
    /// <param name="amount">The amount delta to add.</param>
    /// <exception cref="ArgumentException">The resulting amount is less than or equal to zero.</exception>
    internal void AddAmount(int amount)
    {
        SetAmount(Amount + amount);
    }

    /// <summary>
    /// Reduces the amount stored in this instance.
    /// </summary>
    /// <param name="amount">The amount to remove.</param>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is invalid or greater than the current amount.</exception>
    internal void ReduceAmount(int amount)
    {
        if (amount <= 0 || amount > Amount)
            throw new ArgumentException("Invalid reduction amount.");

        Amount -= amount;
    }

    /// <summary>
    /// Determines whether this instance can stack with another instance.
    /// </summary>
    /// <param name="other">The other item instance.</param>
    /// <returns>
    /// <see langword="true"/> when both instances have the same definition id and structurally equal metadata;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsStackCompatible(ItemInstance<TKey> other)
    {
        if (!EqualityComparer<TKey>.Default.Equals(
                Definition.Id, other.Definition.Id))
            return false;

        return Metadata.StructuralEquals(other.Metadata);
    }

    internal void AttachOwner(Inventory<TKey> inventory)
    {
        Owner = inventory ?? throw new ArgumentNullException(nameof(inventory));
        Metadata.AttachOwner(inventory);
    }

    internal void DetachOwner(Inventory<TKey> inventory)
    {
        if (ReferenceEquals(Owner, inventory))
        {
            Owner = null;
            Metadata.DetachOwner(inventory);
        }
    }
}
