using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
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
    /// Gets the layout context before modification, when available.
    /// </summary>
    public ILayoutContext<TKey>? BeforeLayoutContext { get; }

    /// <summary>
    /// Gets the layout context after the full transaction is applied, when available.
    /// </summary>
    public ILayoutContext<TKey>? AfterLayoutContext { get; }

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
    {
        Instance = instance;
        Index = index;
        BeforeAmount = beforeAmount;
        AfterAmount = afterAmount;
        BeforeLayoutContext = beforeLayoutContext;
        AfterLayoutContext = afterLayoutContext;
    }
}
