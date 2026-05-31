using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;

namespace Workes.InventorySystem.Rules;

public class RequireAttributeRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    public string Id { get; }

    public RequireAttributeRule(AttributeKey<TValue> attribute)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        Id = $"RequireAttribute[{_attribute}]";
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.Contains(_attribute))
            {
                error = $"Expected item definition '{definition.Id}' to have attribute '{_attribute}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
