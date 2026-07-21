using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;
using System;
using System.ComponentModel;

namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires added items to satisfy every specified tag.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireAllTagsRule<TKey> : IRulePolicy<TKey>
{
    private readonly string[] _tagIds;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an all-tags rule.
    /// </summary>
    /// <param name="tagIds">The tag ids every added item must satisfy.</param>
    /// <exception cref="ArgumentException"><paramref name="tagIds"/> is null, empty, or contains invalid ids.</exception>
    public RequireAllTagsRule(params string[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            throw new ArgumentException("At least one tag is required.", nameof(tagIds));

        var tags = new string[tagIds.Length];
        for (var i = 0; i < tagIds.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(tagIds[i]))
                throw new ArgumentException("Required tags cannot contain null.", nameof(tagIds));

            tags[i] = tagIds[i];
        }

        var tagsDescription = string.Join(", ", tags);
        _tagIds = tags;
        Id = $"RequireAllTags[{tagsDescription}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? error)
    {
        var requiredTagsDescription = string.Join(", ", _tagIds);
        foreach (var (definition, _, _) in transaction.Added)
        {
            foreach (var tagId in _tagIds)
            {
                if (!inventory.Catalog.Satisfies(definition, tagId))
                {
                    error = $"Expected item to have all required tags ({requiredTagsDescription}), but it was missing '{tagId}'.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
