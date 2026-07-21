using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that a numeric metadata value lies within a closed range [min, max].
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <typeparam name="T">The comparable metadata value type.</typeparam>
public class MetadataRangeRule<TKey, T> : IRulePolicy<TKey>
    where T : struct, IComparable<T>
{
    private readonly string _key;
    private readonly T _min;
    private readonly T _max;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a metadata range rule.
    /// </summary>
    /// <param name="key">The metadata key that must exist.</param>
    /// <param name="min">The inclusive minimum value.</param>
    /// <param name="max">The inclusive maximum value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty, or <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public MetadataRangeRule(string key, T min, T max)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(_key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
        if (min.CompareTo(max) > 0)
            throw new ArgumentException("min must be less than or equal to max.");

        _min = min;
        _max = max;
        Id = $"MetadataRange[{_key}={_min}-{_max}]";
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
            if (metadata == null || !metadata.TryGet<T>(_key, out var value))
            {
                error = $"Expected item to have numeric metadata '{_key}' with a value in range [{_min}, {_max}] (inclusive), but it was missing or not of the expected type.";
                return false;
            }

            if (value.CompareTo(_min) < 0 || value.CompareTo(_max) > 0)
            {
                error = $"Expected metadata '{_key}' to be in range [{_min}, {_max}] (inclusive), but it was '{value}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
