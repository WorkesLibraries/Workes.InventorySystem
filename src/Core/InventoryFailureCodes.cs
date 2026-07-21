namespace Workes.InventorySystem.Core;

/// <summary>
/// Stable package-owned failure codes. Callers should branch on codes or kinds rather than display messages.
/// </summary>
public static class InventoryFailureCodes
{
    /// <summary>Prefix reserved for built-in package failures.</summary>
    public const string PackagePrefix = "workes.inventory.";

    public const string Unknown = PackagePrefix + "unknown";
    public const string ValidationRejected = PackagePrefix + "validation.rejected";
    public const string DefinitionInvalid = PackagePrefix + "definition.invalid";
    public const string DefinitionUnresolved = PackagePrefix + "definition.unresolved";
    public const string MetadataRejected = PackagePrefix + "metadata.rejected";
    public const string MetadataMissingKey = PackagePrefix + "metadata.missing_key";
    public const string MetadataTypeMismatch = PackagePrefix + "metadata.type_mismatch";
    public const string MetadataUnsupportedValue = PackagePrefix + "metadata.unsupported_value";
    public const string StackingRejected = PackagePrefix + "stacking.rejected";
    public const string CapacityRejected = PackagePrefix + "capacity.rejected";
    public const string RulesRejected = PackagePrefix + "rules.rejected";
    public const string LayoutRejected = PackagePrefix + "layout.rejected";
    public const string LayoutInvalidContext = PackagePrefix + "layout.invalid_context";
    public const string LayoutMappedIndexOutOfRange = PackagePrefix + "layout.mapped_index_out_of_range";
    public const string TransferRejected = PackagePrefix + "transfer.rejected";
    public const string TransferIncompatibleInventories = PackagePrefix + "transfer.incompatible_inventories";
    public const string TransferEmpty = PackagePrefix + "transfer.empty";
    public const string TransactionRejected = PackagePrefix + "transaction.rejected";
    public const string SnapshotRejected = PackagePrefix + "snapshot.rejected";
    public const string SnapshotMalformed = PackagePrefix + "snapshot.malformed";
    public const string SnapshotUnsupportedVersion = PackagePrefix + "snapshot.unsupported_version";
    public const string SnapshotMissingCodec = PackagePrefix + "snapshot.missing_codec";
    public const string SnapshotCodecRejected = PackagePrefix + "snapshot.codec_rejected";
    public const string PersistenceRejected = PackagePrefix + "persistence.rejected";
    public const string ConfigurationRejected = PackagePrefix + "configuration.rejected";
    public const string ConfigurationUnsupportedParameter = PackagePrefix + "configuration.unsupported_parameter";
    public const string ExtensionRejected = PackagePrefix + "extension.rejected";
}
