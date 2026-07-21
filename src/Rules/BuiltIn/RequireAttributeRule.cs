using Workes.InventorySystem.Attributes;
using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added item definitions to contain a typed attribute.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="TValue">The attribute value type.</typeparam>
public class RequireAttributeRule<TKey, TValue> : IRulePolicy<TKey>
{
    private readonly string _attributeId;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a required-attribute rule.
    /// </summary>
    /// <param name="attributeId">The attribute id that must be present.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeId"/> is null, empty, or whitespace.</exception>
    public RequireAttributeRule(string attributeId)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            throw new ArgumentException("Attribute id cannot be null or empty.", nameof(attributeId));

        _attributeId = attributeId;
        Id = $"RequireAttribute[{_attributeId}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? failure)
    {
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!definition.Attributes.Contains<TValue>(_attributeId))
            {
                failure = InventoryFailures.Definition($"Expected item definition '{definition.Id}' to have attribute '{_attributeId}'.");
                return false;
            }
        }

        failure = null;
        return true;
    }
}
