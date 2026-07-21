using Workes.InventorySystem.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
namespace Workes.InventorySystem.Rules;

/// <summary>
/// Allows added items only when their definitions are in a fixed allow-list.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class OnlyAllowItemsRule<TKey> : IRulePolicy<TKey>
{
    private readonly HashSet<ItemDefinition<TKey>> _allowed;
    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Creates an allow-list rule for item definitions.
    /// </summary>
    /// <param name="allowed">The item definitions that may be added.</param>
    /// <exception cref="ArgumentNullException"><paramref name="allowed"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="allowed"/> contains <see langword="null"/>.</exception>
    public OnlyAllowItemsRule(params ItemDefinition<TKey>[] allowed)
    {
        if (allowed == null)
            throw new ArgumentNullException(nameof(allowed));
        foreach (var item in allowed)
        {
            if (item == null)
                throw new ArgumentException("Allowed items cannot contain null.", nameof(allowed));
        }

        _allowed = new HashSet<ItemDefinition<TKey>>(allowed!);
        var allowedDescription = string.Join(", ", allowed.Select(x => $"{x!.Id}"));
        Id = $"OnlyAllowItems[{allowedDescription}]";
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool CanApply(
        Inventory<TKey> inventory,
        NormalizedInventoryTransaction<TKey> transaction,
        out InventoryFailure? error)
    {
        var allowedDescription = string.Join(", ", _allowed.Select(x => $"{x!.Id}"));
        foreach (var (definition, _, _) in transaction.Added)
        {
            if (!_allowed.Contains(definition))
            {
                error = $"Expected transaction to only add allowed items. OnlyAllowItemsRule allows: {allowedDescription}, but it attempted to add '{definition.Id}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
