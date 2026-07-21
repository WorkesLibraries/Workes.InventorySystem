using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Core;

internal interface IInventoryMetadataOwner
{
    bool TryApplyMetadataMutation(
        InventoryMetadata metadata,
        InventoryMetadata proposedMetadata,
        out InventoryFailure? error);
}

/// <summary>
/// Stores portable, schema-free metadata about an inventory.
/// </summary>
/// <remarks>
/// Detached metadata mutates directly. Metadata attached to an inventory routes mutations through that inventory's
/// validation, layout reconciliation, persistence, and event pipeline. Collection values use snapshot semantics:
/// inputs and returned values are recursively detached.
/// </remarks>
public sealed class InventoryMetadata
{
    private delegate bool Mutation(InventoryMetadata proposed, out InventoryFailure? error);

    private readonly MetadataStore _store;
    private IInventoryMetadataOwner? _owner;
    private bool _captureMutationErrors;

    /// <summary>Creates empty detached inventory metadata.</summary>
    public InventoryMetadata() : this(new MetadataStore())
    {
    }

    internal InventoryMetadata(MetadataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Gets whether this metadata container has no stored values.</summary>
    public bool IsEmpty => _store.IsEmpty;

    /// <summary>Adds a metadata value and throws when the key exists or the mutation is rejected.</summary>
    public void Add(string key, object? value) =>
        ThrowIfRejected(TryAdd(key, value, out var error), error);

    /// <summary>Attempts to add a metadata value when the key is absent.</summary>
    public bool TryAdd(string key, object? value, out InventoryFailure? error) =>
        TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryAdd(key, value, out mutationError),
            out error);

    /// <summary>Stores or replaces a metadata value.</summary>
    public void Set(string key, object? value) =>
        ThrowIfRejected(TrySet(key, value, out var error), error);

    /// <summary>Attempts to store or replace a metadata value.</summary>
    public bool TrySet(string key, object? value, out InventoryFailure? error) =>
        TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TrySet(key, value, out mutationError),
            out error);

    /// <summary>Replaces an existing metadata value.</summary>
    public void Change(string key, object? value) =>
        ThrowIfRejected(TryChange(key, value, out var error), error);

    /// <summary>Attempts to replace an existing metadata value.</summary>
    public bool TryChange(string key, object? value, out InventoryFailure? error) =>
        TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryChange(key, value, out mutationError),
            out error);

    /// <summary>Derives a replacement for an existing typed metadata value.</summary>
    public void Update<T>(string key, Func<T, T> updater) =>
        ThrowIfRejected(TryUpdate(key, updater, out var error), error);

    /// <summary>Attempts to derive and commit a replacement for an existing typed metadata value.</summary>
    /// <remarks>Unexpected exceptions from <paramref name="updater"/> propagate without changing metadata.</remarks>
    public bool TryUpdate<T>(string key, Func<T, T> updater, out InventoryFailure? error)
    {
        if (updater == null)
            throw new ArgumentNullException(nameof(updater));

        return TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
            {
                if (!proposed._store.ContainsKey(key))
                {
                    mutationError = $"Metadata key '{key}' was not found.";
                    return false;
                }
                if (!proposed._store.TryGetDetached<T>(key, out var current))
                {
                    mutationError =
                        $"Metadata key '{key}' does not contain a value compatible with '{typeof(T).Name}'.";
                    return false;
                }
                return proposed._store.TryChange(key, updater(current), out mutationError);
            },
            out error);
    }

    /// <summary>Attempts to read a recursively detached metadata value with the requested type.</summary>
    public bool TryGet<T>(string key, out T value) =>
        _store.TryGetDetached(key, out value);

    /// <summary>Removes an existing metadata value.</summary>
    public void Remove(string key) =>
        ThrowIfRejected(TryRemove(key, out var error), error);

    /// <summary>Attempts to remove an existing metadata value.</summary>
    public bool TryRemove(string key, out InventoryFailure? error) =>
        TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryRemove(key, out mutationError),
            out error);

    /// <summary>Removes every metadata value.</summary>
    public void Clear() =>
        ThrowIfRejected(TryClear(out var error), error);

    /// <summary>Attempts to remove every metadata value.</summary>
    public bool TryClear(out InventoryFailure? error)
    {
        if (IsEmpty)
        {
            error = null;
            return true;
        }
        return TryReplace(null, out error);
    }

    /// <summary>Replaces the complete metadata dictionary.</summary>
    public void Replace(IReadOnlyDictionary<string, object?>? values) =>
        ThrowIfRejected(TryReplace(values, out var error), error);

    /// <summary>Attempts to replace the complete metadata dictionary.</summary>
    public bool TryReplace(IReadOnlyDictionary<string, object?>? values, out InventoryFailure? error) =>
        TryMutate(
            (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
                proposed._store.TryReplace(values, out mutationError),
            out error);

    /// <summary>Transforms a detached proposed metadata copy and commits the complete result.</summary>
    public void Transform(Action<InventoryMetadata> transform) =>
        ThrowIfRejected(TryTransform(transform, out var error), error);

    /// <summary>Attempts to transform and commit a detached proposed metadata copy.</summary>
    /// <remarks>Unexpected exceptions from <paramref name="transform"/> propagate without changing metadata.</remarks>
    public bool TryTransform(Action<InventoryMetadata> transform, out InventoryFailure? error)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));
        try
        {
            return TryMutate(
                (InventoryMetadata proposed, out InventoryFailure? mutationError) =>
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
                out error);
        }
        catch (MetadataMutationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Returns a recursively detached, read-only metadata snapshot.</summary>
    public IReadOnlyDictionary<string, object?> AsReadOnly() =>
        _store.AsReadOnlyDetached();

    /// <summary>Copies metadata into a recursively detached mutable dictionary.</summary>
    public Dictionary<string, object?> ToDictionary() =>
        _store.ToDictionaryDetached();

    /// <summary>Determines whether another inventory metadata container has recursively equal values.</summary>
    public bool StructuralEquals(InventoryMetadata other) =>
        other != null && _store.StructuralEquals(other._store);

    internal InventoryMetadata Clone() =>
        new(_store.Clone());

    internal IReadOnlyList<string> GetChangedKeys(InventoryMetadata other) =>
        _store.GetChangedKeys(other._store);

    internal IEnumerable<KeyValuePair<string, object?>> EnumerateStored() =>
        _store.EnumerateStored();

    internal void AttachOwner(IInventoryMetadataOwner owner) =>
        _owner = owner;

    internal void ReplaceDirect(InventoryMetadata source) =>
        _store.ReplaceFrom(source._store);

    private bool TryMutate(Mutation mutation, out InventoryFailure? error)
    {
        var proposed = Clone();
        if (!mutation(proposed, out error))
            return false;
        if (_store.StructuralEquals(proposed._store))
        {
            error = null;
            return true;
        }

        if (_owner != null)
            return _owner.TryApplyMetadataMutation(this, proposed, out error);

        _store.ReplaceFrom(proposed._store);
        error = null;
        return true;
    }

    private void ThrowIfRejected(bool accepted, InventoryFailure? error)
    {
        if (!accepted)
        {
            if (_captureMutationErrors)
                throw new MetadataMutationException(error ?? InventoryFailure.FromMessage("Metadata mutation was rejected.", InventoryFailureKind.Metadata));
            throw new InventoryOperationException(error ?? InventoryFailure.FromMessage("Metadata mutation was rejected.", InventoryFailureKind.Metadata));
        }
    }
}
