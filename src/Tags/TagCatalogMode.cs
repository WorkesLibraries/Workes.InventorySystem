namespace Workes.InventorySystem.Tags;

/// <summary>
/// Controls whether a tag catalog accepts namespaced or non-namespaced tag ids.
/// </summary>
public enum TagCatalogMode
{
    /// <summary>
    /// Accepts tag ids in the form <c>namespace:path.segment</c>.
    /// </summary>
    Namespaced,

    /// <summary>
    /// Accepts tag ids without a namespace, with optional dot-separated hierarchy.
    /// </summary>
    NonNamespaced
}
