using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to contain a typed attribute.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class RequireAttributeRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly AttributeKey<TValue> _attribute;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a required-attribute rule.
    /// </summary>
    /// <param name="attribute">The attribute key that must be present.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/>.</exception>
    public RequireAttributeRule(AttributeKey<TValue> attribute)
    {
        _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        Id = $"RequireAttribute[{_attribute}]";
    }

    /// <inheritdoc />
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
