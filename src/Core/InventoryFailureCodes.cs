namespace Workes.InventorySystem.Core;

/// <summary>
/// Stable package-owned failure codes. Callers should branch on codes or kinds rather than display messages.
/// </summary>
public static class InventoryFailureCodes
{
    /// <summary>Prefix reserved for built-in package failures.</summary>
    public const string PackagePrefix = "workes.inventory.";

    /// <summary>Unknown or unclassified failure.</summary>
    public const string Unknown = PackagePrefix + "unknown";
    /// <summary>General validation rejection.</summary>
    public const string ValidationRejected = PackagePrefix + "validation.rejected";
    /// <summary>Invalid definition, schema, tag, or attribute state.</summary>
    public const string DefinitionInvalid = PackagePrefix + "definition.invalid";
    /// <summary>Definition id could not be resolved.</summary>
    public const string DefinitionUnresolved = PackagePrefix + "definition.unresolved";
    /// <summary>General metadata rejection.</summary>
    public const string MetadataRejected = PackagePrefix + "metadata.rejected";
    /// <summary>Required metadata key was missing.</summary>
    public const string MetadataMissingKey = PackagePrefix + "metadata.missing_key";
    /// <summary>Metadata value type was incompatible with the requested type.</summary>
    public const string MetadataTypeMismatch = PackagePrefix + "metadata.type_mismatch";
    /// <summary>Metadata value is outside the supported portable value model.</summary>
    public const string MetadataUnsupportedValue = PackagePrefix + "metadata.unsupported_value";
    /// <summary>Stacking or max-stack resolution rejected the operation.</summary>
    public const string StackingRejected = PackagePrefix + "stacking.rejected";
    /// <summary>Capacity policy rejected the operation.</summary>
    public const string CapacityRejected = PackagePrefix + "capacity.rejected";
    /// <summary>Inventory rule rejected the operation.</summary>
    public const string RulesRejected = PackagePrefix + "rules.rejected";
    /// <summary>Layout rejected the operation.</summary>
    public const string LayoutRejected = PackagePrefix + "layout.rejected";
    /// <summary>Layout context was invalid for the active layout.</summary>
    public const string LayoutInvalidContext = PackagePrefix + "layout.invalid_context";
    /// <summary>Mapped added-entry index was outside the transaction's added entries.</summary>
    public const string LayoutMappedIndexOutOfRange = PackagePrefix + "layout.mapped_index_out_of_range";
    /// <summary>Transfer operation rejected the request.</summary>
    public const string TransferRejected = PackagePrefix + "transfer.rejected";
    /// <summary>Source and target inventories are incompatible.</summary>
    public const string TransferIncompatibleInventories = PackagePrefix + "transfer.incompatible_inventories";
    /// <summary>Transfer contains no items.</summary>
    public const string TransferEmpty = PackagePrefix + "transfer.empty";
    /// <summary>Transaction operation rejected the request.</summary>
    public const string TransactionRejected = PackagePrefix + "transaction.rejected";
    /// <summary>Snapshot operation rejected the request.</summary>
    public const string SnapshotRejected = PackagePrefix + "snapshot.rejected";
    /// <summary>Snapshot DTO shape or content is malformed.</summary>
    public const string SnapshotMalformed = PackagePrefix + "snapshot.malformed";
    /// <summary>Snapshot version is unsupported.</summary>
    public const string SnapshotUnsupportedVersion = PackagePrefix + "snapshot.unsupported_version";
    /// <summary>Required snapshot codec is missing.</summary>
    public const string SnapshotMissingCodec = PackagePrefix + "snapshot.missing_codec";
    /// <summary>Snapshot codec rejected data or produced invalid data.</summary>
    public const string SnapshotCodecRejected = PackagePrefix + "snapshot.codec_rejected";
    /// <summary>Persistence operation rejected the request.</summary>
    public const string PersistenceRejected = PackagePrefix + "persistence.rejected";
    /// <summary>Runtime configuration change rejected the request.</summary>
    public const string ConfigurationRejected = PackagePrefix + "configuration.rejected";
    /// <summary>Unsupported or invalid runtime parameter.</summary>
    public const string ConfigurationUnsupportedParameter = PackagePrefix + "configuration.unsupported_parameter";
    /// <summary>Extension contract rejected the request.</summary>
    public const string ExtensionRejected = PackagePrefix + "extension.rejected";
}
