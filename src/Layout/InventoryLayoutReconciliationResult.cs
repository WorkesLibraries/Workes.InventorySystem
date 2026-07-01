using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Describes supplemental UI reconciliation information produced by a layout after an inventory mutation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Inventory computes moved surviving items by comparing their contexts before and after reconciliation. Layouts use
/// this result only for affected contexts that are not represented by item movement and for changes that cannot be
/// described completely without a full refresh.
/// </remarks>
public sealed class InventoryLayoutReconciliationResult<TKey>
{
    /// <summary>
    /// Gets an empty reconciliation result.
    /// </summary>
    public static InventoryLayoutReconciliationResult<TKey> None { get; } = new();

    /// <summary>
    /// Gets additional layout contexts affected by layout-owned state.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> AffectedLayoutContexts { get; }

    /// <summary>
    /// Gets whether the layout change cannot be represented completely through item contexts.
    /// </summary>
    public bool RequiresFullRefresh { get; }

    /// <summary>
    /// Creates a layout reconciliation result.
    /// </summary>
    /// <param name="affectedLayoutContexts">Additional affected contexts not already represented by item changes.</param>
    /// <param name="requiresFullRefresh">
    /// Whether layout-owned observable state changed beyond what item movements and affected contexts can describe.
    /// </param>
    public InventoryLayoutReconciliationResult(
        IEnumerable<ILayoutContext<TKey>>? affectedLayoutContexts = null,
        bool requiresFullRefresh = false)
    {
        AffectedLayoutContexts = affectedLayoutContexts != null
            ? affectedLayoutContexts.Where(context => context != null).Distinct().ToList()
            : new List<ILayoutContext<TKey>>();
        RequiresFullRefresh = requiresFullRefresh;
    }
}
