using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Resolves the maximum stack size for item instances added to an inventory.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// This is an extension contract for custom stacking behavior. Normal code should
/// add and merge items through <see cref="Inventory{TKey}"/> so the inventory can
/// apply stack sizes together with layout, capacity, and rule validation.
/// </remarks>
public interface IStackResolver<TKey>
{
    /// <summary>
    /// Gets the maximum amount that the specified item instance may contain in one stack.
    /// </summary>
    /// <param name="inventory">The inventory requesting the stack size.</param>
    /// <param name="instance">The item instance or prototype being evaluated.</param>
    /// <returns>The maximum allowed amount for a compatible stack.</returns>
    int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance);
}
