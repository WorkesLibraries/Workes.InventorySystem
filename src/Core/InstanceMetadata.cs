using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Core;

internal interface IInstanceMetadataOwner
{
    bool TryApplyMetadataMutation(
        InstanceMetadata metadata,
        InstanceMetadata proposedMetadata,
        out InventoryFailure? failure);
}

/// <summary>
/// Stores portable per-instance item metadata used for structural equality and persistence.
/// </summary>
/// <remarks>
/// Detached metadata mutates directly. Metadata owned by an inventory item routes mutation through the owning
/// inventory so stacking, capacity, rules, layout, and events remain consistent. Collection values have snapshot
/// semantics: inputs and returned values are recursively detached.
/// </remarks>
public class InstanceMetadata
{
    private delegate bool Mutation(InstanceMetadata proposed, out InventoryFailure? failure);

    private readonly MetadataStore _store;
    private IInstanceMetadataOwner? _owner;
    private bool _captureMutationErrors;

    /// <summary>Creates empty detached item-instance metadata.</summary>
    public InstanceMetadata() : this(new MetadataStore())
    {
    }

    internal InstanceMetadata(MetadataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Gets whether this metadata container has no stored values.</summary>
    public bool IsEmpty => _store.IsEmpty;

    /// <summary>Adds a metadata value and throws when the key exists or the mutation is rejected.</summary>
    public void Add(string key, object? value) =>
        ThrowIfRejected(TryAdd(key, value, out var failure), failure);

    /// <summary>Attempts to add a metadata value when the key is absent.</summary>
    public bool TryAdd(string key, object? value, out InventoryFailure? failure) =>
        TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryAdd(key, value, out mutationError),
            out failure);

    /// <summary>Stores or replaces a metadata value.</summary>
    public void Set(string key, object? value) =>
        ThrowIfRejected(TrySet(key, value, out var failure), failure);

    /// <summary>Attempts to store or replace a metadata value.</summary>
    public bool TrySet(string key, object? value, out InventoryFailure? failure) =>
        TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TrySet(key, value, out mutationError),
            out failure);

    /// <summary>Replaces an existing metadata value.</summary>
    public void Change(string key, object? value) =>
        ThrowIfRejected(TryChange(key, value, out var failure), failure);

    /// <summary>Attempts to replace an existing metadata value.</summary>
    public bool TryChange(string key, object? value, out InventoryFailure? failure) =>
        TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryChange(key, value, out mutationError),
            out failure);

    /// <summary>Derives a replacement for an existing typed metadata value.</summary>
    public void Update<T>(string key, Func<T, T> updater) =>
        ThrowIfRejected(TryUpdate(key, updater, out var failure), failure);

    /// <summary>Attempts to derive and commit a replacement for an existing typed metadata value.</summary>
    /// <remarks>Unexpected exceptions from <paramref name="updater"/> propagate without changing metadata.</remarks>
    public bool TryUpdate<T>(string key, Func<T, T> updater, out InventoryFailure? failure)
    {
        if (updater == null)
            throw new ArgumentNullException(nameof(updater));

        return TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
            {
                if (!proposed._store.ContainsKey(key))
                {
                    mutationError = InventoryFailures.MetadataMissingKey($"Metadata key '{key}' was not found.");
                    return false;
                }
                if (!proposed._store.TryGetDetached<T>(key, out var current))
                {
                    mutationError =
                        InventoryFailures.MetadataTypeMismatch($"Metadata key '{key}' does not contain a value compatible with '{typeof(T).Name}'.");
                    return false;
                }
                return proposed._store.TryChange(key, updater(current), out mutationError);
            },
            out failure);
    }

    /// <summary>Attempts to read a recursively detached metadata value with the requested type.</summary>
    public bool TryGet<T>(string key, out T value) =>
        _store.TryGetDetached(key, out value);

    /// <summary>Removes an existing metadata value.</summary>
    public void Remove(string key) =>
        ThrowIfRejected(TryRemove(key, out var failure), failure);

    /// <summary>Attempts to remove an existing metadata value.</summary>
    public bool TryRemove(string key, out InventoryFailure? failure) =>
        TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryRemove(key, out mutationError),
            out failure);

    /// <summary>Removes every metadata value.</summary>
    public void Clear() =>
        ThrowIfRejected(TryClear(out var failure), failure);

    /// <summary>Attempts to remove every metadata value.</summary>
    public bool TryClear(out InventoryFailure? failure)
    {
        if (IsEmpty)
        {
            failure = null;
            return true;
        }
        return TryReplace(null, out failure);
    }

    /// <summary>Replaces the complete metadata dictionary.</summary>
    public void Replace(IReadOnlyDictionary<string, object?>? values) =>
        ThrowIfRejected(TryReplace(values, out var failure), failure);

    /// <summary>Attempts to replace the complete metadata dictionary.</summary>
    public bool TryReplace(IReadOnlyDictionary<string, object?>? values, out InventoryFailure? failure) =>
        TryMutate(
            (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryReplace(values, out mutationError),
            out failure);

    /// <summary>Transforms a detached proposed metadata copy and commits the complete result.</summary>
    public void Transform(Action<InstanceMetadata> transform) =>
        ThrowIfRejected(TryTransform(transform, out var failure), failure);

    /// <summary>Attempts to transform and commit a detached proposed metadata copy.</summary>
    /// <remarks>Unexpected exceptions from <paramref name="transform"/> propagate without changing metadata.</remarks>
    public bool TryTransform(Action<InstanceMetadata> transform, out InventoryFailure? failure)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));
        try
        {
            return TryMutate(
                (InstanceMetadata proposed, out InventoryFailure? mutationError) =>
                {
                    proposed._captureMutationErrors = true;
                    try
                    {
                        transform(proposed);
                    }
                    finally
                    {
                        proposed._captureMutationErrors = false;
                    }
                    mutationError = null;
                    return true;
                },
                out failure);
        }
        catch (MetadataMutationException ex)
        {
            failure = ex.Failure ?? InventoryFailures.Metadata(ex.Message);
            return false;
        }
    }

    /// <summary>Returns a recursively detached, read-only metadata snapshot.</summary>
    public IReadOnlyDictionary<string, object?> AsReadOnly() =>
        _store.AsReadOnlyDetached();

    /// <summary>Copies metadata into a recursively detached mutable dictionary.</summary>
    public Dictionary<string, object?> ToDictionary() =>
        _store.ToDictionaryDetached();

    /// <summary>Determines whether another item metadata container has recursively equal values.</summary>
    public bool StructuralEquals(InstanceMetadata other) =>
        other != null && _store.StructuralEquals(other._store);

    /// <summary>Replaces metadata from an existing dictionary.</summary>
    public void RestoreMetadata(Dictionary<string, object> data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        var nullable = new Dictionary<string, object?>(data.Count, StringComparer.Ordinal);
        foreach (var pair in data)
            nullable.Add(pair.Key, pair.Value);
        Replace(nullable);
    }

    internal InstanceMetadata Clone() =>
        new(_store.Clone());

    internal MetadataStore CloneStore() =>
        _store.Clone();

    internal IReadOnlyList<string> GetChangedKeys(InstanceMetadata other) =>
        _store.GetChangedKeys(other._store);

    internal IEnumerable<KeyValuePair<string, object?>> EnumerateStored() =>
        _store.EnumerateStored();

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
        if (!_store.TrySet(key, value, out var failure))
            throw new MetadataMutationException(failure ?? InventoryFailures.Metadata("Metadata value was rejected."));
    }

    internal void ReplaceDirect(IReadOnlyDictionary<string, object?>? values)
    {
        if (!_store.TryReplace(values, out var failure))
            throw new MetadataMutationException(failure ?? InventoryFailures.Metadata("Metadata replacement was rejected."));
    }

    internal void ReplaceDirect(InstanceMetadata source) =>
        _store.ReplaceFrom(source._store);

    private bool TryMutate(Mutation mutation, out InventoryFailure? failure)
    {
        if (mutation == null)
            throw new ArgumentNullException(nameof(mutation));

        var proposed = Clone();
        if (!mutation(proposed, out failure))
            return false;
        if (_store.StructuralEquals(proposed._store))
        {
            failure = null;
            return true;
        }

        if (_owner != null)
            return _owner.TryApplyMetadataMutation(this, proposed, out failure);

        _store.ReplaceFrom(proposed._store);
        failure = null;
        return true;
    }

    private void ThrowIfRejected(bool accepted, InventoryFailure? failure)
    {
        if (!accepted)
        {
            if (_captureMutationErrors)
                throw new MetadataMutationException(failure ?? InventoryFailures.Metadata("Metadata mutation was rejected."));
            throw new InventoryOperationException(failure ?? InventoryFailures.Metadata("Metadata mutation was rejected."));
        }
    }
}
