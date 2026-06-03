namespace Workes.InventorySystem.Core;

/// <summary>
/// Controls optional rebuild behavior for runtime inventory parameter changes.
/// </summary>
/// <remarks>
/// Preserve-only changes keep the existing stack shape and layout placement. Repack and compression options allow the
/// inventory to rebuild current contents when a safe parameter change would otherwise require moving items or splitting stacks.
/// </remarks>
public sealed class InventoryParameterMutationOptions
{
    /// <summary>
    /// Gets options that preserve current stack shape and layout placement.
    /// </summary>
    public static InventoryParameterMutationOptions PreserveOnly => new InventoryParameterMutationOptions();

    /// <summary>
    /// Gets options that allow both layout repacking and stack compression.
    /// </summary>
    public static InventoryParameterMutationOptions RepackAndCompress => new InventoryParameterMutationOptions
    {
        RepackLayout = true,
        CompressStacks = true
    };

    /// <summary>
    /// Gets or sets whether the inventory may re-place current items using normal context-less layout placement.
    /// </summary>
    public bool RepackLayout { get; set; }

    /// <summary>
    /// Gets or sets whether oversized stacks may be split into multiple valid stacks.
    /// </summary>
    public bool CompressStacks { get; set; }
}
