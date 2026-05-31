using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that added items have at least one of the provided tags.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class RequireAnyTagRule<TKey> : IRulePolicy<TKey>
{
    private readonly TagKey[] _tags;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an any-tag rule.
    /// </summary>
    /// <param name="tags">The tags of which each added item must satisfy at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="tags"/> is null, empty, or contains <see langword="null"/>.</exception>
    public RequireAnyTagRule(params TagKey[] tags)
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
        Id = $"RequireAnyTag[{tagsDescription}]";
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
            bool hasAny = false;
            foreach (var tag in _tags)
            {
                if (inventory.Catalog.Satisfies(definition, tag))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
            {
                error = $"Expected item to have at least one of the required tags ({requiredTagsDescription}), but it had none.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
