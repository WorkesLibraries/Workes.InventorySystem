using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

public class InstanceMetadata
{
    private Dictionary<string, object>? _data;

    private Dictionary<string, object> Data =>
        _data ??= new Dictionary<string, object>();

    public bool IsEmpty => _data == null || _data.Count == 0;

    public void Set(string key, object value)
    {
        Data[key] = value;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (_data != null &&
            _data.TryGetValue(key, out var obj) &&
            obj is T casted)
        {
            value = casted;
            return true;
        }

        value = default!;
        return false;
    }

    public bool Remove(string key)
    {
        return _data != null && _data.Remove(key);
    }

    public IReadOnlyDictionary<string, object> AsReadOnly()
    {
        return _data ?? (IReadOnlyDictionary<string, object>)
               new Dictionary<string, object>();
    }

    public bool StructuralEquals(InstanceMetadata other)
    {
        if (other == null)
            return false;

        if (IsEmpty && other.IsEmpty)
            return true;

        if (_data == null || other._data == null)
            return false;

        if (_data.Count != other._data.Count)
            return false;

        foreach (var pair in _data)
        {
            if (!other._data.TryGetValue(pair.Key, out var value))
                return false;

            if (!Equals(pair.Value, value))
                return false;
        }

        return true;
    }

    public void RestoreMetadata(Dictionary<string, object> data) => _data = data;

    public Dictionary<string, object> ToDictionary() => new(Data);
}
