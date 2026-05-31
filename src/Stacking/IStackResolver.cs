using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Stacking;

public interface IStackResolver<TKey>
{
    int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance);
}
