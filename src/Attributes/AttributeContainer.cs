using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Attributes;

public sealed class AttributeContainer
{
    private readonly Dictionary<object, object?> _values = new();

    public void Set<T>(AttributeKey<T> key, T value)
    {
        _values[key] = value;
    }

    public bool TryGet<T>(AttributeKey<T> key, out T value)
    {
        if (_values.TryGetValue(key, out var obj) && obj is T casted)
        {
            value = casted;
            return true;
        }

        value = default!;
        return false;
    }

    public T GetOrDefault<T>(AttributeKey<T> key, T defaultValue = default!)
    {
        return TryGet(key, out T value) ? value : defaultValue;
    }

    public bool Contains<T>(AttributeKey<T> key)
    {
        return _values.ContainsKey(key);
    }

    public IEnumerable<object> GetAllKeys()
    {
        return _values.Keys;
    }
}
