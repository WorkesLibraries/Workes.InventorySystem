using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that returns the same maximum stack size for every item.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class DefaultStackResolver<TKey> : IStackResolver<TKey>
{
    private readonly int _defaultMaxStack;

    /// <summary>
    /// Creates a stack resolver with a fixed maximum stack size.
    /// </summary>
    /// <param name="defaultMaxStack">The maximum amount allowed in each stack.</param>
    public DefaultStackResolver(int defaultMaxStack)
    {
        _defaultMaxStack = defaultMaxStack;
    }

    /// <inheritdoc />
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance) => _defaultMaxStack;
}
