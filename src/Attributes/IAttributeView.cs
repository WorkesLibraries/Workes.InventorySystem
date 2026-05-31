using System.Collections.Generic;

namespace Workes.InventorySystem.Attributes;

/// <summary>
/// Provides read-only access to typed attributes stored by <see cref="AttributeKey{T}"/>.
/// </summary>
public interface IAttributeView
{
    /// <summary>
    /// Attempts to read the value associated with the specified typed attribute key.
    /// </summary>
    /// <typeparam name="T">The value type associated with the attribute key.</typeparam>
    /// <param name="key">The typed attribute key to look up.</param>
    /// <param name="value">The attribute value when the key exists and the stored value has the requested type.</param>
    /// <returns><see langword="true"/> when a compatible value is present; otherwise, <see langword="false"/>.</returns>
    bool TryGet<T>(AttributeKey<T> key, out T value);

    /// <summary>
    /// Gets the value associated with the specified typed attribute key, or a fallback value when it is absent.
    /// </summary>
    /// <typeparam name="T">The value type associated with the attribute key.</typeparam>
    /// <param name="key">The typed attribute key to look up.</param>
    /// <param name="defaultValue">The value to return when the key is not present or has an incompatible value type.</param>
    /// <returns>The stored value, or <paramref name="defaultValue"/>.</returns>
    T GetOrDefault<T>(AttributeKey<T> key, T defaultValue = default!);

    /// <summary>
    /// Determines whether the view contains a compatible value for the specified typed attribute key.
    /// </summary>
    /// <typeparam name="T">The value type associated with the attribute key.</typeparam>
    /// <param name="key">The typed attribute key to look up.</param>
    /// <returns><see langword="true"/> when a compatible value is present; otherwise, <see langword="false"/>.</returns>
    bool Contains<T>(AttributeKey<T> key);

    /// <summary>
    /// Returns all attribute keys currently present in the view.
    /// </summary>
    /// <returns>The stored attribute keys as their key objects.</returns>
    IEnumerable<object> GetAllKeys();
}
