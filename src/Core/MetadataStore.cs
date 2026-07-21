using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Workes.InventorySystem.Persistence;

namespace Workes.InventorySystem.Core;

internal sealed class MetadataMutationException : InvalidOperationException
{
    public MetadataMutationException(string message) : base(message)
    {
    }

    public MetadataMutationException(InventoryFailure failure)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Message)
    {
        Failure = failure;
    }

    public InventoryFailure? Failure { get; }
}

internal sealed class MetadataStore
{
    private Dictionary<string, object?>? _data;

    public bool IsEmpty => _data == null || _data.Count == 0;

    public bool ContainsKey(string key) =>
        _data != null && _data.ContainsKey(key);

    public bool TryAdd(string key, object? value, out InventoryFailure? error)
    {
        if (!TryValidateKey(key, out error))
            return false;
        if (_data != null && _data.ContainsKey(key))
        {
            error = $"Metadata key '{key}' already exists.";
            return false;
        }
        if (!TryCloneValue(value, out var detached, out error))
        {
            error = $"Metadata value '{key}' is not portable: {error}";
            return false;
        }
        Data.Add(key, detached);
        error = null;
        return true;
    }

    public bool TrySet(string key, object? value, out InventoryFailure? error)
    {
        if (!TryValidateKey(key, out error))
            return false;
        if (!TryCloneValue(value, out var detached, out error))
        {
            error = $"Metadata value '{key}' is not portable: {error}";
            return false;
        }
        Data[key] = detached;
        error = null;
        return true;
    }

    public bool TryChange(string key, object? value, out InventoryFailure? error)
    {
        if (!TryValidateKey(key, out error))
            return false;
        if (_data == null || !_data.ContainsKey(key))
        {
            error = $"Metadata key '{key}' was not found.";
            return false;
        }
        return TrySet(key, value, out error);
    }

    public bool TryRemove(string key, out InventoryFailure? error)
    {
        if (!TryValidateKey(key, out error))
            return false;
        if (_data == null || !_data.Remove(key))
        {
            error = $"Metadata key '{key}' was not found.";
            return false;
        }
        if (_data.Count == 0)
            _data = null;
        error = null;
        return true;
    }

    public bool TryReplace(IReadOnlyDictionary<string, object?>? values, out InventoryFailure? error)
    {
        if (values == null || values.Count == 0)
        {
            _data = null;
            error = null;
            return true;
        }

        var replacement = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (!TryValidateKey(pair.Key, out error))
                return false;
            if (!TryCloneValue(pair.Value, out var detached, out error))
            {
                error = $"Metadata value '{pair.Key}' is not portable: {error}";
                return false;
            }
            replacement.Add(pair.Key, detached);
        }

        _data = replacement;
        error = null;
        return true;
    }

    public bool TryGetDetached<T>(string key, out T value)
    {
        value = default!;
        if (_data == null || !_data.TryGetValue(key, out var stored))
            return false;
        if (stored == null)
            return default(T) is null;
        if (stored is not T typed)
            return false;
        if (!TryCloneValue(typed, out var detached, out _) || detached is not T detachedTyped)
            return false;
        value = detachedTyped;
        return true;
    }

    public IReadOnlyDictionary<string, object?> AsReadOnlyDetached()
    {
        return new ReadOnlyDictionary<string, object?>(ToDictionaryDetached());
    }

    public Dictionary<string, object?> ToDictionaryDetached()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_data == null)
            return result;
        foreach (var pair in _data)
        {
            if (!TryCloneValue(pair.Value, out var detached, out var error))
                throw new InventoryOperationException(error ?? InventoryFailure.FromMessage(null));
            result.Add(pair.Key, detached);
        }
        return result;
    }

    public MetadataStore Clone()
    {
        var clone = new MetadataStore();
        clone.ReplaceFrom(this);
        return clone;
    }

    public void ReplaceFrom(MetadataStore source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!TryReplace(source._data, out var error))
            throw new InventoryOperationException(error ?? InventoryFailure.FromMessage(null));
    }

    public bool StructuralEquals(MetadataStore other)
    {
        if (other == null)
            return false;
        if (IsEmpty)
            return other.IsEmpty;
        if (other._data == null || _data!.Count != other._data.Count)
            return false;
        foreach (var pair in _data)
        {
            if (!other._data.TryGetValue(pair.Key, out var value) ||
                !StructuralValueEquals(pair.Value, value))
                return false;
        }
        return true;
    }

    public IReadOnlyList<string> GetChangedKeys(MetadataStore other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        if (_data != null)
        {
            foreach (var pair in _data)
            {
                if (other._data == null ||
                    !other._data.TryGetValue(pair.Key, out var value) ||
                    !StructuralValueEquals(pair.Value, value))
                    keys.Add(pair.Key);
            }
        }
        if (other._data != null)
        {
            foreach (var pair in other._data)
            {
                if (_data == null || !_data.ContainsKey(pair.Key))
                    keys.Add(pair.Key);
            }
        }
        return new List<string>(keys);
    }

    public IEnumerable<KeyValuePair<string, object?>> EnumerateStored()
    {
        if (_data == null)
            yield break;
        foreach (var pair in _data)
            yield return pair;
    }

    private Dictionary<string, object?> Data =>
        _data ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    private static bool TryValidateKey(string key, out InventoryFailure? error)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            error = "Metadata keys cannot be null, empty, or whitespace.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryCloneValue(object? value, out object? clone, out InventoryFailure? error)
    {
        clone = null;
        if (!IsSupportedMetadataValue(
                value,
                new HashSet<object>(MetadataReferenceComparer.Instance),
                out error))
        {
            return false;
        }
        if (!InventorySnapshotCodecs.TryEncodeObject(value, out var encoded, out error) || encoded == null)
            return false;
        if (!InventorySnapshotCodecs.TryDecodeRuntime(encoded, out clone, out _, out error))
            return false;
        return true;
    }

    private static bool IsSupportedMetadataValue(
        object? value,
        HashSet<object> path,
        out InventoryFailure? error)
    {
        if (value == null)
        {
            error = null;
            return true;
        }

        Type type = value.GetType();
        if (type == typeof(string) ||
            type == typeof(char) ||
            type == typeof(bool) ||
            type == typeof(byte) ||
            type == typeof(sbyte) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(decimal) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan))
        {
            error = null;
            return true;
        }

        bool isArray = type.IsArray && type.GetArrayRank() == 1;
        bool isList = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        if (!isArray && !isList)
        {
            error = type == typeof(object)
                ? "Literal System.Object values are not supported metadata."
                : $"unsupported: Type '{type.FullName}' is not a supported portable snapshot value for metadata; expected a " +
                  "portable scalar, one-dimensional array, or List<T>.";
            return false;
        }
        Type elementType = isArray ? type.GetElementType()! : type.GetGenericArguments()[0];
        if (elementType != typeof(object) &&
            !IsSupportedMetadataElementType(elementType))
        {
            error = $"Collection element type '{elementType.FullName}' is not supported metadata.";
            return false;
        }
        if (!path.Add(value))
        {
            error = $"Metadata value graph contains a cycle at type '{type.FullName}'.";
            return false;
        }
        try
        {
            foreach (var item in (IEnumerable)value)
            {
                if (!IsSupportedMetadataValue(item, path, out error))
                    return false;
            }
        }
        finally
        {
            path.Remove(value);
        }
        error = null;
        return true;
    }

    private static bool IsSupportedMetadataElementType(Type type)
    {
        if (type.IsEnum)
            return false;
        if (type.IsArray)
            return type.GetArrayRank() == 1 && IsSupportedMetadataElementType(type.GetElementType()!);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return IsSupportedMetadataElementType(type.GetGenericArguments()[0]);
        return type == typeof(string) ||
               type == typeof(char) ||
               type == typeof(bool) ||
               type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan);
    }

    private sealed class MetadataReferenceComparer : IEqualityComparer<object>
    {
        public static MetadataReferenceComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private static bool StructuralValueEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.GetType() != right.GetType())
            return false;
        if (left is IList leftList && right is IList rightList)
        {
            if (leftList.Count != rightList.Count)
                return false;
            for (int i = 0; i < leftList.Count; i++)
            {
                if (!StructuralValueEquals(leftList[i], rightList[i]))
                    return false;
            }
            return true;
        }
        return Equals(left, right);
    }
}
