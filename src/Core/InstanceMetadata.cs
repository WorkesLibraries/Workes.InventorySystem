using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Stores per-instance item metadata used for structural equality and serialization.
/// </summary>
/// <remarks>
/// Metadata is mutable caller-owned state. Mutating metadata on an inserted
/// item changes future structural equality checks but does not currently fire
/// inventory change events.
/// </remarks>
public class InstanceMetadata
{
    private Dictionary<string, object>? _data;

    private Dictionary<string, object> Data =>
        _data ??= new Dictionary<string, object>();

    /// <summary>
    /// Gets whether this metadata container has no stored values.
    /// </summary>
    public bool IsEmpty => _data == null || _data.Count == 0;

    /// <summary>
    /// Stores or replaces a metadata value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void Set(string key, object value)
    {
        Data[key] = value;
    }

    /// <summary>
    /// Attempts to read a metadata value with the requested type.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The value when present and compatible.</param>
    /// <returns><see langword="true"/> when a compatible value is present; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Removes a metadata value.
    /// </summary>
    /// <param name="key">The metadata key to remove.</param>
    /// <returns><see langword="true"/> when a value was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string key)
    {
        return _data != null && _data.Remove(key);
    }

    /// <summary>
    /// Returns a read-only view of the stored metadata.
    /// </summary>
    /// <remarks>When metadata exists, the returned view reflects later mutations. Stored values are not deep-cloned.</remarks>
    /// <returns>A read-only dictionary of metadata values.</returns>
    public IReadOnlyDictionary<string, object> AsReadOnly()
    {
        return _data ?? (IReadOnlyDictionary<string, object>)
               new Dictionary<string, object>();
    }

    /// <summary>
    /// Determines whether this metadata has the same keys and values as another metadata container.
    /// </summary>
    /// <param name="other">The metadata container to compare with this one.</param>
    /// <returns><see langword="true"/> when both containers are structurally equal; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Replaces the stored metadata with a copy of an existing dictionary.
    /// </summary>
    /// <remarks>The dictionary container is copied, but stored values are not deep-cloned.</remarks>
    /// <param name="data">The metadata dictionary to restore.</param>
    public void RestoreMetadata(Dictionary<string, object> data) => _data = data != null ? new Dictionary<string, object>(data) : null;

    /// <summary>
    /// Copies the stored metadata into a mutable dictionary.
    /// </summary>
    /// <remarks>The dictionary container is copied, but stored values are not deep-cloned.</remarks>
    /// <returns>A dictionary containing the current metadata values.</returns>
    public Dictionary<string, object> ToDictionary() => new(Data);
}
