namespace Workes.InventorySystem.Sorting;

/// <summary>
/// Describes layout-specific sorting instructions for an inventory layout.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Layouts interpret sort contexts themselves. Simple layouts typically use
/// <see cref="ItemSortContext{TKey}"/>, while complex layouts can expose richer
/// sort contexts without changing inventory storage order.
/// </remarks>
public interface IInventorySortContext<TKey>
{
}
