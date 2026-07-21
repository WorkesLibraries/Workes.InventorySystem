using System;

namespace Workes.InventorySystem.Persistence;

/// <summary>
/// Assigns the one portable snapshot key codec for a custom inventory key type.
/// The codec type must implement <see cref="IInventorySnapshotKeyCodec{TKey}"/> for the attributed type
/// and expose a public parameterless constructor.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum,
    AllowMultiple = false,
    Inherited = false)]
public sealed class InventorySnapshotKeyCodecAttribute : Attribute
{
    /// <summary>Creates an association with a separate, stateless codec type.</summary>
    public InventorySnapshotKeyCodecAttribute(Type codecType)
    {
        CodecType = codecType ?? throw new ArgumentNullException(nameof(codecType));
    }

    /// <summary>Gets the assigned codec type.</summary>
    public Type CodecType { get; }
}

/// <summary>
/// Converts one custom inventory key type to and from a portable snapshot value.
/// Implementations must be stateless and safe for concurrent use.
/// </summary>
public interface IInventorySnapshotKeyCodec<TKey>
{
    /// <summary>Gets the globally unique, stable persisted format identifier.</summary>
    string FormatId { get; }

    /// <summary>Gets the version written by <see cref="TryEncode"/>.</summary>
    int CurrentVersion { get; }

    /// <summary>Attempts to encode a key.</summary>
    bool TryEncode(TKey value, out SnapshotValue? encoded, out InventoryFailure? failure);

    /// <summary>Attempts to decode a supported historical version.</summary>
    bool TryDecode(SnapshotValue encoded, int version, out TKey value, out InventoryFailure? failure);
}

internal interface ISnapshotValueCodec<T>
{
    string FormatId { get; }
    int CurrentVersion { get; }
    bool TryEncode(T value, out SnapshotValue? encoded, out InventoryFailure? failure);
    bool TryDecode(SnapshotValue encoded, int version, out T value, out InventoryFailure? failure);
}
