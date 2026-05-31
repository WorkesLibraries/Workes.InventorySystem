using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

public class ItemInstance<TKey>
{
    public ItemDefinition<TKey> Definition { get; }
    public int Amount { get; private set; }
    public Guid InstanceId { get; }

    public InstanceMetadata Metadata { get; } = new();

    public ItemInstance(ItemDefinition<TKey> definition, int amount = 1, InstanceMetadata? metadata = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        Definition = definition;
        Amount = amount;
        InstanceId = Guid.NewGuid();
        Metadata = metadata ?? new InstanceMetadata();
    }

    public void SetAmount(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        Amount = amount;
    }

    public void AddAmount(int amount)
    {
        SetAmount(Amount + amount);
    }

    public void ReduceAmount(int amount)
    {
        if (amount <= 0 || amount > Amount)
            throw new ArgumentException("Invalid reduction amount.");

        Amount -= amount;
    }

    public bool IsStackCompatible(ItemInstance<TKey> other)
    {
        if (!EqualityComparer<TKey>.Default.Equals(
                Definition.Id, other.Definition.Id))
            return false;

        return Metadata.StructuralEquals(other.Metadata);
    }
}
