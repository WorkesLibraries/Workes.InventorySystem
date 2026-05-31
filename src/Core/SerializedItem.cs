using System.Collections.Generic;
namespace Workes.InventorySystem.Core;

[System.Serializable]
/// <summary>
/// Serialized representation of a single item instance.
/// </summary>
public class SerializedItem<TKey>
{
    public TKey DefinitionId { get; set; } = default!;
    public int Amount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
