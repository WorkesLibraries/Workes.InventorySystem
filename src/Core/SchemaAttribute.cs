namespace Workes.InventorySystem.Core;

public sealed class SchemaAttribute
{
    public object Key { get; }
    public bool Inherited { get; }

    public SchemaAttribute(object key, bool inherited)
    {
        Key = key;
        Inherited = inherited;
    }
}
