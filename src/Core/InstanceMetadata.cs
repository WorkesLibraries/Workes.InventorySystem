using System;
using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

internal interface IInstanceMetadataOwner
{
    bool TryApplyMetadataMutation(
        InstanceMetadata metadata,
        Func<InstanceMetadata, bool> mutate,
        out string? error);
}

/// <summary>
/// Stores per-instance item metadata used for structural equality and serialization.
/// </summary>
/// <remarks>
/// Detached metadata mutates directly. Metadata owned by an inventory item routes
/// mutation through the owning inventory so rules, layout, capacity, and events
/// remain consistent.
/// </remarks>
public class InstanceMetadata
{
    private Dictionary<string, object>? _data;
    private IInstanceMetadataOwner? _owner;

    private Dictionary<string, object> Data =>
        _data ??= new Dictionary<string, object>();

    /// <summary>
    /// Gets whether this metadata container has no stored values.
    /// </summary>
    public bool IsEmpty => _data == null || _data.Count == 0;

    /// <summary>
    /// Adds a metadata value.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. Use <see cref="TryAdd"/> for conditional flows.
    /// </remarks>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="InvalidOperationException">The key already exists or the owning inventory rejects the mutation.</exception>
    public void Add(string key, object? value)
    {
        ThrowIfRejected(TryAdd(key, value, out var error), error);
    }

    /// <summary>
    /// Stores or replaces a metadata value.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. Use <see cref="TrySet"/> for conditional flows.
    /// </remarks>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="InvalidOperationException">The owning inventory rejects the mutation.</exception>
    public void Set(string key, object? value)
    {
        ThrowIfRejected(TrySet(key, value, out var error), error);
    }

    /// <summary>
    /// Attempts to add a metadata value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is added; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(string key, object? value, out string? error)
    {
        return TryMutate(
            clone =>
            {
                if (clone.AsReadOnly().ContainsKey(key))
                    return false;

                clone.SetDirect(key, value);
                return true;
            },
            $"Metadata key '{key}' already exists.",
            out error);
    }

    /// <summary>
    /// Attempts to add or replace a metadata value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is stored; otherwise, <see langword="false"/>.</returns>
    public bool TrySet(string key, object? value, out string? error)
    {
        return TryMutate(
            clone =>
            {
                clone.SetDirect(key, value);
                return true;
            },
            null,
            out error);
    }

    /// <summary>
    /// Replaces an existing metadata value.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. Use <see cref="TryChange"/> for conditional flows.
    /// </remarks>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="InvalidOperationException">The key does not exist or the owning inventory rejects the mutation.</exception>
    public void Change(string key, object? value)
    {
        ThrowIfRejected(TryChange(key, value, out var error), error);
    }

    /// <summary>
    /// Attempts to replace an existing metadata value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is changed; otherwise, <see langword="false"/>.</returns>
    public bool TryChange(string key, object? value, out string? error)
    {
        return TryMutate(
            clone =>
            {
                if (!clone.AsReadOnly().ContainsKey(key))
                    return false;

                clone.SetDirect(key, value);
                return true;
            },
            $"Metadata key '{key}' was not found.",
            out error);
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
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. Use <see cref="TryRemove"/> for conditional flows.
    /// </remarks>
    /// <param name="key">The metadata key to remove.</param>
    /// <exception cref="InvalidOperationException">The key does not exist or the owning inventory rejects the mutation.</exception>
    public void Remove(string key)
    {
        ThrowIfRejected(TryRemove(key, out var error), error);
    }

    /// <summary>
    /// Attempts to remove a metadata value.
    /// </summary>
    /// <param name="key">The metadata key to remove.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is removed; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(string key, out string? error)
    {
        return TryMutate(
            clone =>
            {
                if (!clone.RemoveDirect(key))
                    return false;

                return true;
            },
            $"Metadata key '{key}' was not found.",
            out error);
    }

    /// <summary>
    /// Removes every metadata value.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. Use <see cref="TryClear"/> for conditional flows.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The owning inventory rejects the mutation.</exception>
    public void Clear()
    {
        ThrowIfRejected(TryClear(out var error), error);
    }

    /// <summary>
    /// Attempts to remove every metadata value.
    /// </summary>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when metadata is cleared or already empty; otherwise, <see langword="false"/>.</returns>
    public bool TryClear(out string? error)
    {
        if (IsEmpty)
        {
            error = null;
            return true;
        }

        return TryReplace(null, out error);
    }

    /// <summary>
    /// Attempts to replace the entire metadata dictionary.
    /// </summary>
    /// <param name="values">The replacement values.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when metadata is replaced; otherwise, <see langword="false"/>.</returns>
    public bool TryReplace(IReadOnlyDictionary<string, object>? values, out string? error)
    {
        return TryMutate(
            clone =>
            {
                clone.ReplaceDirect(values);
                return true;
            },
            null,
            out error);
    }

    /// <summary>
    /// Replaces the entire metadata dictionary.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates through the owning inventory and throws
    /// when the mutation is rejected. The dictionary container is copied, but stored values are not deep-cloned. Use
    /// <see cref="TryReplace"/> for conditional flows.
    /// </remarks>
    /// <param name="values">The replacement values.</param>
    /// <exception cref="InvalidOperationException">The owning inventory rejects the mutation.</exception>
    public void Replace(IReadOnlyDictionary<string, object>? values)
    {
        ThrowIfRejected(TryReplace(values, out var error), error);
    }

    /// <summary>
    /// Transforms metadata using a mutable clone.
    /// </summary>
    /// <remarks>
    /// Detached metadata mutates directly. Inventory-owned metadata validates the transformed result through the owning
    /// inventory and throws when the mutation is rejected. Use <see cref="TryTransform"/> for conditional flows.
    /// </remarks>
    /// <param name="transform">The mutation to apply to a proposed metadata copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The owning inventory rejects the mutation.</exception>
    public void Transform(Action<InstanceMetadata> transform)
    {
        ThrowIfRejected(TryTransform(transform, out var error), error);
    }

    /// <summary>
    /// Attempts to transform metadata using a mutable clone.
    /// </summary>
    /// <param name="transform">The mutation to apply to a proposed metadata copy.</param>
    /// <param name="error">A consumer-facing reason when the mutation is rejected; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the transformed metadata is committed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> is <see langword="null"/>.</exception>
    public bool TryTransform(Action<InstanceMetadata> transform, out string? error)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        return TryMutate(
            clone =>
            {
                transform(clone);
                return true;
            },
            null,
            out error);
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
    /// <remarks>
    /// The dictionary container is copied, but stored values are not deep-cloned. Inventory-owned metadata validates
    /// through the owning inventory and throws when the mutation is rejected.
    /// </remarks>
    /// <param name="data">The metadata dictionary to restore.</param>
    /// <exception cref="InvalidOperationException">The owning inventory rejects the mutation.</exception>
    public void RestoreMetadata(Dictionary<string, object> data)
    {
        Replace(data);
    }

    /// <summary>
    /// Copies the stored metadata into a mutable dictionary.
    /// </summary>
    /// <remarks>The dictionary container is copied, but stored values are not deep-cloned.</remarks>
    /// <returns>A dictionary containing the current metadata values.</returns>
    public Dictionary<string, object> ToDictionary() => new(Data);

    internal void AttachOwner(IInstanceMetadataOwner owner)
    {
        _owner = owner;
    }

    internal void DetachOwner(IInstanceMetadataOwner owner)
    {
        if (ReferenceEquals(_owner, owner))
            _owner = null;
    }

    internal void SetDirect(string key, object? value)
    {
        Data[key] = value!;
    }

    internal bool RemoveDirect(string key)
    {
        return _data != null && _data.Remove(key);
    }

    internal void ReplaceDirect(IReadOnlyDictionary<string, object>? values)
    {
        if (values == null || values.Count == 0)
        {
            _data?.Clear();
            _data = null;
            return;
        }

        if (_data == null)
        {
            _data = new Dictionary<string, object>(values);
            return;
        }

        _data.Clear();
        foreach (var pair in values)
            _data[pair.Key] = pair.Value;
    }

    internal InstanceMetadata Clone()
    {
        var clone = new InstanceMetadata();
        clone.ReplaceDirect(_data);
        return clone;
    }

    private bool TryMutate(Func<InstanceMetadata, bool> mutate, string? falseError, out string? error)
    {
        if (mutate == null)
            throw new ArgumentNullException(nameof(mutate));

        if (_owner != null)
        {
            var rejectedByMutation = false;
            bool accepted = _owner.TryApplyMetadataMutation(
                this,
                clone =>
                {
                    bool mutated = mutate(clone);
                    if (!mutated)
                        rejectedByMutation = true;
                    return mutated;
                },
                out error);

            if (!accepted && rejectedByMutation && !string.IsNullOrWhiteSpace(falseError))
                error = falseError;

            return accepted;
        }

        var clone = Clone();
        if (!mutate(clone))
        {
            error = falseError ?? "Metadata mutation was rejected.";
            return false;
        }

        ReplaceDirect(clone.AsReadOnly());
        error = null;
        return true;
    }

    private static void ThrowIfRejected(bool accepted, string? error)
    {
        if (!accepted)
            throw new InvalidOperationException(error ?? "Metadata mutation was rejected.");
    }
}
