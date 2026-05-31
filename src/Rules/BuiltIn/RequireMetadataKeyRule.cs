using Workes.InventorySystem.Core;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that added items have a metadata entry with the given key,
/// regardless of its value.
/// </summary>
public class RequireMetadataKeyRule<TKey> : IRulePolicy<TKey>
{
    private readonly string _key;
    public string Id { get; }

    public RequireMetadataKeyRule(string key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(_key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));
        Id = $"RequireMetadataKey[{_key}]";
    }

    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        foreach (var (_, metadata, _) in transaction.Added)
        {
            if (metadata == null || metadata.AsReadOnly().ContainsKey(_key) == false)
            {
                error = $"Expected item metadata to contain key '{_key}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
