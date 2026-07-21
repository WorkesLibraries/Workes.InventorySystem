using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Events.Dto;

/// <summary>Describes a committed change to inventory-owned metadata.</summary>
public sealed class InventoryMetadataChanged
{
    private readonly MetadataStore _before;
    private readonly MetadataStore _after;

    /// <summary>Gets a deeply detached snapshot of metadata before the change.</summary>
    public IReadOnlyDictionary<string, object?> BeforeMetadata => _before.AsReadOnlyDetached();

    /// <summary>Gets a deeply detached snapshot of metadata after the change.</summary>
    public IReadOnlyDictionary<string, object?> AfterMetadata => _after.AsReadOnlyDetached();

    /// <summary>Gets added, removed, or recursively changed keys in ordinal order.</summary>
    public IReadOnlyList<string> ChangedKeys { get; }

    /// <summary>Creates an inventory-metadata change payload from detached snapshots.</summary>
    public InventoryMetadataChanged(
        IReadOnlyDictionary<string, object?>? beforeMetadata,
        IReadOnlyDictionary<string, object?>? afterMetadata)
    {
        _before = CreateStore(beforeMetadata, nameof(beforeMetadata));
        _after = CreateStore(afterMetadata, nameof(afterMetadata));
        ChangedKeys = Array.AsReadOnly(new List<string>(_before.GetChangedKeys(_after)).ToArray());
    }

    internal InventoryMetadataChanged(InventoryMetadata before, InventoryMetadata after)
        : this(before?.AsReadOnly(), after?.AsReadOnly())
    {
    }

    private static MetadataStore CreateStore(
        IReadOnlyDictionary<string, object?>? values,
        string parameterName)
    {
        var store = new MetadataStore();
        if (!store.TryReplace(values, out var error))
            throw new ArgumentException(error?.Message, parameterName);
        return store;
    }
}
