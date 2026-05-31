using System.Collections.Generic;

namespace Workes.InventorySystem.Attributes;

public interface IAttributeView
{
    bool TryGet<T>(AttributeKey<T> key, out T value);
    T GetOrDefault<T>(AttributeKey<T> key, T defaultValue = default!);
    bool Contains<T>(AttributeKey<T> key);
    IEnumerable<object> GetAllKeys();
}
