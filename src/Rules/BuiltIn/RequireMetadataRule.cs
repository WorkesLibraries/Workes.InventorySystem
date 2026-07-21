using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added items to contain a metadata value equal to an expected value.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireMetadataRule<TKey> : IRulePolicy<TKey>
{
    private readonly string _key;
    private readonly object? _value;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a metadata equality rule.
    /// </summary>
    /// <param name="key">The metadata key that must exist.</param>
    /// <param name="value">The expected metadata value.</param>
    public RequireMetadataRule(string key, object? value)
    {
        _key = key;
        _value = value;
        Id = $"RequireMetadata[{_key}={_value?.ToString()}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? error)
    {
        foreach (var (_, metadata, _) in transaction.Added)
        {
            if (metadata == null || !metadata.TryGet<object>(_key, out var val) || !Equals(val, _value))
            {
                error = $"Expected item metadata '{_key}' to equal '{_value}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
