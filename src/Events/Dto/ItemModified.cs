using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item instance whose amount changed.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class ItemModified<TKey>
{
    /// <summary>
    /// Gets the item instance after modification.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the storage index of the modified item instance.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the item amount before modification.
    /// </summary>
    public int BeforeAmount { get; }

    /// <summary>
    /// Gets the item amount after modification.
    /// </summary>
    public int AfterAmount { get; }

    /// <summary>
    /// Gets the layout contexts before modification.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> BeforeLayoutContexts { get; }

    /// <summary>
    /// Gets the layout contexts after the full transaction is applied.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> AfterLayoutContexts { get; }

    /// <summary>
    /// Gets the single layout context before modification, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? BeforeLayoutContext => BeforeLayoutContexts.Count == 1 ? BeforeLayoutContexts[0] : null;

    /// <summary>
    /// Gets the single layout context after modification, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? AfterLayoutContext => AfterLayoutContexts.Count == 1 ? AfterLayoutContexts[0] : null;

    /// <summary>
    /// Creates an item-modified event payload.
    /// </summary>
    /// <param name="instance">The item instance after modification.</param>
    /// <param name="index">The storage index of the modified item instance.</param>
    /// <param name="beforeAmount">The item amount before modification.</param>
    /// <param name="afterAmount">The item amount after modification.</param>
    /// <param name="beforeLayoutContext">The layout context before modification, when available.</param>
    /// <param name="afterLayoutContext">The layout context after the full transaction is applied, when available.</param>
    public ItemModified(
        ItemInstance<TKey> instance,
        int index,
        int beforeAmount,
        int afterAmount,
        ILayoutContext<TKey>? beforeLayoutContext = null,
        ILayoutContext<TKey>? afterLayoutContext = null)
        : this(
            instance,
            index,
            beforeAmount,
            afterAmount,
            beforeLayoutContext != null ? new[] { beforeLayoutContext } : null,
            afterLayoutContext != null ? new[] { afterLayoutContext } : null)
    {
    }

    /// <summary>
    /// Creates an item-modified event payload.
    /// </summary>
    /// <param name="instance">The item instance after modification.</param>
    /// <param name="index">The storage index of the modified item instance.</param>
    /// <param name="beforeAmount">The item amount before modification.</param>
    /// <param name="afterAmount">The item amount after modification.</param>
    /// <param name="beforeLayoutContexts">The layout contexts before modification.</param>
    /// <param name="afterLayoutContexts">The layout contexts after the full transaction is applied.</param>
    public ItemModified(
        ItemInstance<TKey> instance,
        int index,
        int beforeAmount,
        int afterAmount,
        IEnumerable<ILayoutContext<TKey>>? beforeLayoutContexts,
        IEnumerable<ILayoutContext<TKey>>? afterLayoutContexts)
    {
        Instance = instance;
        Index = index;
        BeforeAmount = beforeAmount;
        AfterAmount = afterAmount;
        BeforeLayoutContexts = beforeLayoutContexts != null ? beforeLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
        AfterLayoutContexts = afterLayoutContexts != null ? afterLayoutContexts.ToList() : new List<ILayoutContext<TKey>>();
    }
}
