using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that a metadata key exists and its value is one of the allowed values.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireMetadataOneOfValuesRule<TKey> : IRulePolicy<TKey>
{
    private readonly string _key;
    private readonly HashSet<object> _allowedValues;
    private readonly string _allowedValuesDescription;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a metadata allowed-values rule.
    /// </summary>
    /// <param name="key">The metadata key that must exist.</param>
    /// <param name="allowedValues">The allowed metadata values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or <paramref name="allowedValues"/> is null or empty.</exception>
    public RequireMetadataOneOfValuesRule(string key, params object[] allowedValues)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(_key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
        if (allowedValues == null || allowedValues.Length == 0)
            throw new ArgumentException("At least one allowed value must be provided.", nameof(allowedValues));

        _allowedValues = new HashSet<object>(allowedValues);
        _allowedValuesDescription = string.Join(", ", allowedValues);
        Id = $"RequireMetadataOneOf[{_key}:{_allowedValuesDescription}]";
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (_, metadata, _) in transaction.Added)
        {
            if (metadata == null || !metadata.AsReadOnly().TryGetValue(_key, out var value))
            {
                error = $"Expected item metadata '{_key}' to exist and be one of: {_allowedValuesDescription}.";
                return false;
            }

            if (!_allowedValues.Contains(value))
            {
                error = $"Expected item metadata '{_key}' to be one of: {_allowedValuesDescription}, but it was '{value}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
