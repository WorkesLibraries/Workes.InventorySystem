using Workes.InventorySystem.Core;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that added items have a metadata entry with the given key,
/// regardless of its value.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireMetadataKeyRule<TKey> : IRulePolicy<TKey>
{
    private readonly string _key;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates a metadata-key rule.
    /// </summary>
    /// <param name="key">The metadata key that must exist.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    public RequireMetadataKeyRule(string key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(_key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
        Id = $"RequireMetadataKey[{_key}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? failure)
    {
        foreach (var (_, metadata, _) in transaction.Added)
        {
            if (metadata == null || metadata.AsReadOnly().ContainsKey(_key) == false)
            {
                failure = InventoryFailures.Metadata($"Expected item metadata to contain key '{_key}'.");
                return false;
            }
        }

        failure = null;
        return true;
    }
}
