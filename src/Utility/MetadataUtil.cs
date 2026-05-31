using System;
using Workes.InventorySystem.Core;
namespace Workes.InventorySystem.Utility;

/// <summary>
/// Provides helpers for reading item instance metadata.
/// </summary>
public static class MetadataUtil
{
    /// <summary>
    /// If the metadata contains the given key and the value is of type <typeparamref name="T"/>,
    /// invokes the action with that value.
    /// </summary>
    /// <typeparam name="T">The expected metadata value type.</typeparam>
    /// <param name="metadata">The metadata container to read.</param>
    /// <param name="key">The metadata key to read.</param>
    /// <param name="action">The action to invoke when a compatible value is present.</param>
    public static void IfPresent<T>(InstanceMetadata metadata, string key, Action<T> action)
    {
        if (metadata == null || action == null)
            return;

        if (metadata.TryGet(key, out T value))
            action(value);
    }
}
