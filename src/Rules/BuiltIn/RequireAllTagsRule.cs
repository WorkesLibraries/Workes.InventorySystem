using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;
using System;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added items to satisfy every specified tag.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireAllTagsRule<TKey> : IRulePolicy<TKey>
{
    private readonly TagKey[] _tags;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an all-tags rule.
    /// </summary>
    /// <param name="tags">The tags every added item must satisfy.</param>
    /// <exception cref="ArgumentException"><paramref name="tags"/> is null, empty, or contains <see langword="null"/>.</exception>
    public RequireAllTagsRule(params TagKey[] tags)
    {
        if (tags == null || tags.Length == 0)
            throw new ArgumentException("At least one tag is required.", nameof(tags));

        foreach (var tag in tags)
        {
            if (tag == null)
                throw new ArgumentException("Required tags cannot contain null.", nameof(tags));
        }

        var tagsDescription = string.Join(", ", Array.ConvertAll(tags, t => t.ToString()));
        _tags = tags;
        Id = $"RequireAllTags[{tagsDescription}]";
    }

    /// <inheritdoc />
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out string? error)
    {
        var requiredTagsDescription = string.Join(", ", Array.ConvertAll(_tags, t => t.ToString()));
        foreach (var (definition, _, _) in transaction.Added)
        {
            foreach (var tag in _tags)
            {
                if (!inventory.Catalog.Satisfies(definition, tag))
                {
                    error = $"Expected item to have all required tags ({requiredTagsDescription}), but it was missing '{tag}'.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
