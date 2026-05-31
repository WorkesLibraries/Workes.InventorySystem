using Workes.InventorySystem.Core;
using Workes.InventorySystem.Tags;
using System;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Requires that added items have at least one of the provided tags.
/// </summary>
public class RequireAnyTagRule<TKey> : IRulePolicy<TKey>
{
    private readonly TagKey[] _tags;
    public string Id { get; }

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
