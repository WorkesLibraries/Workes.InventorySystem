using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes one prefixed input delta for semantic delta combination.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryItemDeltaPart<TKey>
{
    /// <summary>Gets the delta to combine.</summary>
    public InventoryItemDelta<TKey> Delta { get; }

    /// <summary>Gets the unique prefix assigned to labels from this delta.</summary>
    public string Prefix { get; }

    /// <summary>Gets how many times the delta contributes to the semantic combination.</summary>
    public int Count { get; }

    /// <summary>Creates a prefixed combination part.</summary>
    public InventoryItemDeltaPart(InventoryItemDelta<TKey> delta, string prefix, int count = 1)
    {
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null, empty, or whitespace.", nameof(prefix));
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");

        Prefix = prefix;
        Count = count;
    }

    /// <summary>Creates a prefixed combination part.</summary>
    public static InventoryItemDeltaPart<TKey> From(InventoryItemDelta<TKey> delta, string prefix, int count = 1) =>
        new(delta, prefix, count);
}
