namespace Workes.InventorySystem.Core;

/// <summary>
/// Controls optional rebuild behavior for runtime inventory parameter changes.
/// </summary>
/// <remarks>
/// Preserve-only changes keep the existing stack shape and layout placement. Stack resolver parameter changes support
/// all actions. Layout parameter changes support only <see cref="InventoryParameterMutationActions.RepackLayout"/>.
/// Capacity policy parameter changes reject mutation actions.
/// </remarks>
public sealed class InventoryParameterMutationOptions
{
    /// <summary>
    /// Gets options that preserve current stack shape and layout placement.
    /// </summary>
    public static InventoryParameterMutationOptions PreserveOnly => new InventoryParameterMutationOptions();

    /// <summary>
    /// Gets options that allow layout repacking without changing stack amounts.
    /// </summary>
    public static InventoryParameterMutationOptions RepackOnly => new InventoryParameterMutationOptions
    {
        Actions = InventoryParameterMutationActions.RepackLayout
    };

    /// <summary>
    /// Gets options that allow both layout repacking and oversized stack splitting.
    /// </summary>
    public static InventoryParameterMutationOptions RepackAndCompress => new InventoryParameterMutationOptions
    {
        Actions = InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks
    };

    /// <summary>
    /// Gets options that allow layout repacking and compatible stack compression.
    /// </summary>
    public static InventoryParameterMutationOptions RepackAndCompact => new InventoryParameterMutationOptions
    {
        Actions = InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.CompressCompatibleStacks
    };

    /// <summary>
    /// Gets options that allow layout repacking, oversized stack splitting, and compatible stack compression.
    /// </summary>
    public static InventoryParameterMutationOptions RepackCompressAndCompact => new InventoryParameterMutationOptions
    {
        Actions = InventoryParameterMutationActions.RepackLayout |
            InventoryParameterMutationActions.SplitOversizedStacks |
            InventoryParameterMutationActions.CompressCompatibleStacks
    };

    /// <summary>
    /// Gets or sets the optional actions to run during the parameter mutation.
    /// </summary>
    /// <remarks>
    /// Stack resolver parameter changes support all defined actions. Layout parameter changes support
    /// <see cref="InventoryParameterMutationActions.RepackLayout"/> only. Capacity policy parameter changes support
    /// <see cref="InventoryParameterMutationActions.None"/> only.
    /// </remarks>
    public InventoryParameterMutationActions Actions { get; set; }

    /// <summary>
    /// Gets or sets whether the inventory may re-place current items using normal context-less layout placement.
    /// </summary>
    public bool RepackLayout
    {
        get => HasAction(InventoryParameterMutationActions.RepackLayout);
        set => SetAction(InventoryParameterMutationActions.RepackLayout, value);
    }

    /// <summary>
    /// Gets or sets whether oversized stacks may be split into multiple valid stacks.
    /// </summary>
    public bool SplitOversizedStacks
    {
        get => HasAction(InventoryParameterMutationActions.SplitOversizedStacks);
        set => SetAction(InventoryParameterMutationActions.SplitOversizedStacks, value);
    }

    /// <summary>
    /// Gets or sets whether stack-compatible entries may be compressed into fuller stacks.
    /// </summary>
    public bool CompressCompatibleStacks
    {
        get => HasAction(InventoryParameterMutationActions.CompressCompatibleStacks);
        set => SetAction(InventoryParameterMutationActions.CompressCompatibleStacks, value);
    }

    /// <summary>
    /// Gets or sets whether stack-compatible entries may be compressed into fuller stacks.
    /// </summary>
    /// <remarks>
    /// This property maps directly to <see cref="CompressCompatibleStacks"/>.
    /// </remarks>
    public bool CompactCompatibleStacks
    {
        get => CompressCompatibleStacks;
        set => CompressCompatibleStacks = value;
    }

    /// <summary>
    /// Gets or sets whether oversized stacks may be split into multiple valid stacks.
    /// </summary>
    /// <remarks>
    /// This property maps directly to <see cref="SplitOversizedStacks"/>.
    /// </remarks>
    public bool CompressStacks
    {
        get => SplitOversizedStacks;
        set => SplitOversizedStacks = value;
    }

    private bool HasAction(InventoryParameterMutationActions action)
    {
        return (Actions & action) == action;
    }

    private void SetAction(InventoryParameterMutationActions action, bool enabled)
    {
        Actions = enabled ? Actions | action : Actions & ~action;
    }
}
