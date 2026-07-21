using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;
using System;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that added items have at least one of the provided tags.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireAnyTagRule<TKey> : IRulePolicy<TKey>
{
    private readonly string[] _tagIds;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an any-tag rule.
    /// </summary>
    /// <param name="tagIds">The tag ids of which each added item must satisfy at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="tagIds"/> is null, empty, or contains invalid ids.</exception>
    public RequireAnyTagRule(params string[] tagIds)
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
        Id = $"RequireAnyTag[{tagsDescription}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? failure)
    {
        var requiredTagsDescription = string.Join(", ", _tagIds);
        foreach (var (definition, _, _) in transaction.Added)
        {
            bool hasAny = false;
            foreach (var tagId in _tagIds)
            {
                if (inventory.Catalog.Satisfies(definition, tagId))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
            {
                failure = InventoryFailures.Definition($"Expected item to have at least one of the required tags ({requiredTagsDescription}), but it had none.");
                return false;
            }
        }

        failure = null;
        return true;
    }
}
