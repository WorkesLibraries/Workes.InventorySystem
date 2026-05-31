using System.Collections.Generic;
namespace Workes.InventorySystem.Layout;

public class SlotLayoutPersistentData : ILayoutPersistentData
{
    public List<int?> SlotMap { get; set; } = new();

    public object? GetPersistentContext() => SlotMap;
}
