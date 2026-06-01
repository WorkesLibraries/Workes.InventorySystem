using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a concrete amount of an item definition in an inventory.
/// </summary>
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
    public int Amount { get; private set; }

    /// <summary>
    /// Gets the unique runtime identifier for this instance.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the per-instance metadata for this item.
    /// </summary>
    /// <remarks>
    /// The returned metadata object is live and mutable. Mutating it after insertion
    /// is visible to future stack compatibility and structural equality checks, but
    /// does not currently fire <see cref="Inventory{TKey}.Changed"/>.
    /// </remarks>
    public InstanceMetadata Metadata { get; } = new();

    /// <summary>
    /// Creates an item instance.
    /// </summary>
    /// <param name="definition">The item definition represented by this instance.</param>
    /// <param name="amount">The amount stored in this instance.</param>
    /// <param name="metadata">Optional per-instance metadata.</param>
    /// <remarks>The provided metadata object is stored by reference.</remarks>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is less than or equal to zero.</exception>
    public ItemInstance(ItemDefinition<TKey> definition, int amount = 1, InstanceMetadata? metadata = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        Definition = definition;
        Amount = amount;
        InstanceId = Guid.NewGuid();
        Metadata = metadata ?? new InstanceMetadata();
    }

    /// <summary>
    /// Replaces the amount stored in this instance.
    /// </summary>
    /// <param name="amount">The new amount.</param>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is less than or equal to zero.</exception>
    public void SetAmount(int amount)
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
    public void AddAmount(int amount)
    {
        SetAmount(Amount + amount);
    }

    /// <summary>
    /// Reduces the amount stored in this instance.
    /// </summary>
    /// <param name="amount">The amount to remove.</param>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is invalid or greater than the current amount.</exception>
    public void ReduceAmount(int amount)
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
}
