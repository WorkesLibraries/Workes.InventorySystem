using System.Collections.Generic;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Layout;

/// <summary>
/// Configures tag and definition compatibility for an equipment slot.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Tag restrictions use catalog-resolved tags. Definition restrictions compare item definition ids with
/// <see cref="EqualityComparer{T}.Default"/>. When both tag and definition restrictions are configured,
/// matching either restriction makes an item valid for the slot.
/// </remarks>
public sealed class EquipmentSlotOptions<TKey>
{
    /// <summary>
    /// Gets or sets the tag ids an item definition can satisfy to fit the slot.
    /// </summary>
    public IEnumerable<string>? RequiredTags { get; set; }

    /// <summary>
    /// Gets or sets the item definition ids that are explicitly allowed in the slot.
    /// </summary>
    public IEnumerable<TKey>? AllowedDefinitionIds { get; set; }

    /// <summary>
    /// Gets or sets item definitions whose ids are explicitly allowed in the slot.
    /// </summary>
    public IEnumerable<ItemDefinition<TKey>>? AllowedDefinitions { get; set; }
}
