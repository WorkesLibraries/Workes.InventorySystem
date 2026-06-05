using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes optional inventory-owned rebuild actions for runtime parameter mutation.
/// </summary>
[Flags]
public enum InventoryParameterMutationActions
{
    /// <summary>
    /// Preserve current stack shape and layout placement.
    /// </summary>
    None = 0,

    /// <summary>
    /// Re-place current stack entries in storage order using normal layout placement.
    /// </summary>
    RepackLayout = 1,

    /// <summary>
    /// Split stacks that exceed the proposed max stack size.
    /// </summary>
    SplitOversizedStacks = 2,

    /// <summary>
    /// Merge compatible stack amounts into fuller earlier stacks.
    /// </summary>
    CompressCompatibleStacks = 4
}
