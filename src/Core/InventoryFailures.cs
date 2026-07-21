namespace Workes.InventorySystem.Core;

internal static class InventoryFailures
{
    public static InventoryFailure Unknown(string? message = null) =>
        Create(InventoryFailureKind.Unknown, InventoryFailureCodes.Unknown, message);

    public static InventoryFailure Validation(string? message = null) =>
        Create(InventoryFailureKind.Validation, InventoryFailureCodes.ValidationRejected, message);

    public static InventoryFailure Definition(string? message = null) =>
        Create(InventoryFailureKind.Definition, InventoryFailureCodes.DefinitionInvalid, message);

    public static InventoryFailure DefinitionUnresolved(string? message = null) =>
        Create(InventoryFailureKind.Definition, InventoryFailureCodes.DefinitionUnresolved, message);

    public static InventoryFailure Metadata(string? message = null) =>
        Create(InventoryFailureKind.Metadata, InventoryFailureCodes.MetadataRejected, message);

    public static InventoryFailure MetadataMissingKey(string? message = null) =>
        Create(InventoryFailureKind.Metadata, InventoryFailureCodes.MetadataMissingKey, message);

    public static InventoryFailure MetadataTypeMismatch(string? message = null) =>
        Create(InventoryFailureKind.Metadata, InventoryFailureCodes.MetadataTypeMismatch, message);

    public static InventoryFailure MetadataUnsupportedValue(string? message = null) =>
        Create(InventoryFailureKind.Metadata, InventoryFailureCodes.MetadataUnsupportedValue, message);

    public static InventoryFailure Stacking(string? message = null) =>
        Create(InventoryFailureKind.Stacking, InventoryFailureCodes.StackingRejected, message);

    public static InventoryFailure Capacity(string? message = null) =>
        Create(InventoryFailureKind.Capacity, InventoryFailureCodes.CapacityRejected, message);

    public static InventoryFailure Rules(string? message = null) =>
        Create(InventoryFailureKind.Rules, InventoryFailureCodes.RulesRejected, message);

    public static InventoryFailure Layout(string? message = null) =>
        Create(InventoryFailureKind.Layout, InventoryFailureCodes.LayoutRejected, message);

    public static InventoryFailure LayoutInvalidContext(string? message = null) =>
        Create(InventoryFailureKind.Layout, InventoryFailureCodes.LayoutInvalidContext, message);

    public static InventoryFailure LayoutMappedIndexOutOfRange(string? message = null) =>
        Create(InventoryFailureKind.Layout, InventoryFailureCodes.LayoutMappedIndexOutOfRange, message);

    public static InventoryFailure Transfer(string? message = null) =>
        Create(InventoryFailureKind.Transfer, InventoryFailureCodes.TransferRejected, message);

    public static InventoryFailure TransferIncompatibleInventories(string? message = null) =>
        Create(InventoryFailureKind.Transfer, InventoryFailureCodes.TransferIncompatibleInventories, message);

    public static InventoryFailure TransferEmpty(string? message = null) =>
        Create(InventoryFailureKind.Transfer, InventoryFailureCodes.TransferEmpty, message);

    public static InventoryFailure Transaction(string? message = null) =>
        Create(InventoryFailureKind.Transaction, InventoryFailureCodes.TransactionRejected, message);

    public static InventoryFailure Persistence(string? message = null) =>
        Create(InventoryFailureKind.Persistence, InventoryFailureCodes.PersistenceRejected, message);

    public static InventoryFailure Snapshot(string? message = null) =>
        Create(InventoryFailureKind.Snapshot, InventoryFailureCodes.SnapshotRejected, message);

    public static InventoryFailure SnapshotMalformed(string? message = null) =>
        Create(InventoryFailureKind.Snapshot, InventoryFailureCodes.SnapshotMalformed, message);

    public static InventoryFailure SnapshotUnsupportedVersion(string? message = null) =>
        Create(InventoryFailureKind.Snapshot, InventoryFailureCodes.SnapshotUnsupportedVersion, message);

    public static InventoryFailure SnapshotMissingCodec(string? message = null) =>
        Create(InventoryFailureKind.Snapshot, InventoryFailureCodes.SnapshotMissingCodec, message);

    public static InventoryFailure SnapshotCodecRejected(string? message = null) =>
        Create(InventoryFailureKind.Snapshot, InventoryFailureCodes.SnapshotCodecRejected, message);

    public static InventoryFailure Configuration(string? message = null) =>
        Create(InventoryFailureKind.Configuration, InventoryFailureCodes.ConfigurationRejected, message);

    public static InventoryFailure ConfigurationUnsupportedParameter(string? message = null) =>
        Create(InventoryFailureKind.Configuration, InventoryFailureCodes.ConfigurationUnsupportedParameter, message);

    public static InventoryFailure Extension(string? message = null) =>
        Create(InventoryFailureKind.Extension, InventoryFailureCodes.ExtensionRejected, message);

    private static InventoryFailure Create(InventoryFailureKind kind, string code, string? message) =>
        InventoryFailure.Create(
            kind,
            code,
            string.IsNullOrWhiteSpace(message) ? "Inventory operation failed." : message!);
}
