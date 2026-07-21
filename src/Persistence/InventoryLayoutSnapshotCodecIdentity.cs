using System;
using System.Collections.Generic;

namespace Workes.InventorySystem.Persistence;

internal static class InventoryLayoutSnapshotCodecIdentity
{
    private static readonly object s_gate = new();
    private static readonly Dictionary<Type, string> s_kindsByCodec = new();
    private static readonly Dictionary<string, Type> s_codecsByKind = new(StringComparer.Ordinal);

    internal static bool TryAssociate(Type closedCodecType, string layoutKind, out InventoryFailure? failure)
    {
        Type codecIdentity = closedCodecType.IsGenericType
            ? closedCodecType.GetGenericTypeDefinition()
            : closedCodecType;

        lock (s_gate)
        {
            if (s_kindsByCodec.TryGetValue(codecIdentity, out var existingKind) &&
                !string.Equals(existingKind, layoutKind, StringComparison.Ordinal))
            {
                failure = InventoryFailures.Layout($"Layout codec '{codecIdentity.FullName}' is already associated with kind '{existingKind}'.");
                return false;
            }
            if (s_codecsByKind.TryGetValue(layoutKind, out var existingCodec) &&
                existingCodec != codecIdentity)
            {
                failure = InventoryFailures.Layout($"Layout kind '{layoutKind}' is already associated with codec '{existingCodec.FullName}'.");
                return false;
            }
            s_kindsByCodec[codecIdentity] = layoutKind;
            s_codecsByKind[layoutKind] = codecIdentity;
        }

        failure = null;
        return true;
    }
}
