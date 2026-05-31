using System;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Utility;

public static class MetadataUtil
{
    /// <summary>
    /// If the metadata contains the given key and the value is of type <typeparamref name="T"/>,
    /// invokes the action with that value.
    /// </summary>
    public static void IfPresent<T>(InstanceMetadata metadata, string key, Action<T> action)
    {
        if (metadata == null || action == null)
            return;

        if (metadata.TryGet(key, out T value))
            action(value);
    }
}
