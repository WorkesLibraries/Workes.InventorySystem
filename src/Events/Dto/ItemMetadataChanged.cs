using Workes.InventorySystem.Core;
using Workes.InventorySystem.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes an item instance whose metadata changed.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class ItemMetadataChanged<TKey>
{
    private readonly MetadataStore _beforeMetadata;
    private readonly MetadataStore _afterMetadata;

    /// <summary>
    /// Gets the item instance after metadata mutation.
    /// </summary>
    public ItemInstance<TKey> Instance { get; }

    /// <summary>
    /// Gets the storage index of the item instance.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the metadata before mutation.
    /// </summary>
    public IReadOnlyDictionary<string, object?> BeforeMetadata => _beforeMetadata.AsReadOnlyDetached();

    /// <summary>
    /// Gets the metadata after mutation.
    /// </summary>
    public IReadOnlyDictionary<string, object?> AfterMetadata => _afterMetadata.AsReadOnlyDetached();

    /// <summary>
    /// Gets the layout contexts occupied by the item.
    /// </summary>
    public IReadOnlyList<ILayoutContext<TKey>> LayoutContexts { get; }

    /// <summary>
    /// Gets the single layout context, when exactly one is available.
    /// </summary>
    public ILayoutContext<TKey>? LayoutContext => LayoutContexts.Count == 1 ? LayoutContexts[0] : null;

    /// <summary>
    /// Creates an item-metadata-changed event payload.
    /// </summary>
    /// <param name="instance">The item instance after metadata mutation.</param>
    /// <param name="index">The storage index of the item instance.</param>
    /// <param name="beforeMetadata">The metadata before mutation.</param>
    /// <param name="afterMetadata">The metadata after mutation.</param>
    /// <param name="layoutContexts">The layout contexts occupied by the item.</param>
    public ItemMetadataChanged(
        ItemInstance<TKey> instance,
        int index,
        IReadOnlyDictionary<string, object?> beforeMetadata,
        IReadOnlyDictionary<string, object?> afterMetadata,
        IEnumerable<ILayoutContext<TKey>>? layoutContexts)
    {
        Instance = instance;
        Index = index;
        _beforeMetadata = new MetadataStore();
        if (!_beforeMetadata.TryReplace(beforeMetadata, out var beforeError))
            throw new ArgumentException(beforeError, nameof(beforeMetadata));
        _afterMetadata = new MetadataStore();
        if (!_afterMetadata.TryReplace(afterMetadata, out var afterError))
            throw new ArgumentException(afterError, nameof(afterMetadata));
        LayoutContexts = layoutContexts != null ? layoutContexts.ToList() : new List<ILayoutContext<TKey>>();
    }
}
