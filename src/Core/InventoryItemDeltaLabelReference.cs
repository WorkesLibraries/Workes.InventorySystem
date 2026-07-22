using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes a label identity that contributed to a semantic delta operation.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryItemDeltaLabelReference<TKey>
{
    /// <summary>Gets the label as it appeared on the original delta operation.</summary>
    public string OriginalLabel { get; }

    /// <summary>Gets the combine prefix that produced this reference, or <see langword="null"/> for non-combined deltas.</summary>
    public string? Prefix { get; }

    /// <summary>
    /// Gets the globally unique combined label, or <see langword="null"/> when this operation was not produced by a
    /// prefixed combine.
    /// </summary>
    public string? CombinedLabel { get; }

    /// <summary>Gets the amount of the final semantic operation associated with this label reference.</summary>
    public int Amount { get; }

    /// <summary>Creates a label reference.</summary>
    public InventoryItemDeltaLabelReference(
        string originalLabel,
        int amount,
        string? prefix = null,
        string? combinedLabel = null)
    {
        if (string.IsNullOrWhiteSpace(originalLabel))
            throw new ArgumentException("Label cannot be null, empty, or whitespace.", nameof(originalLabel));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");

        OriginalLabel = originalLabel;
        Prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix;
        CombinedLabel = string.IsNullOrWhiteSpace(combinedLabel) ? null : combinedLabel;
        Amount = amount;
    }

    internal InventoryItemDeltaLabelReference<TKey> WithAmount(int amount) =>
        new(OriginalLabel, amount, Prefix, CombinedLabel);
}
