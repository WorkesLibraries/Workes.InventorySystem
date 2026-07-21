using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Structured, non-exception result data for an expected inventory-system rejection.
/// </summary>
public sealed class InventoryFailure : IEquatable<InventoryFailure>
{
    public InventoryFailure(
        InventoryFailureKind kind,
        string code,
        string message,
        string? component = null,
        string? source = null,
        InventoryFailure? cause = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Failure code cannot be null or empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Failure message cannot be null or empty.", nameof(message));

        Kind = kind;
        Code = code;
        Message = message;
        Component = component;
        Source = source;
        Cause = cause;
    }

    public InventoryFailureKind Kind { get; }
    public string Code { get; }
    public string Message { get; }
    public string? Component { get; }
    public string? Source { get; }
    public InventoryFailure? Cause { get; }

    public static InventoryFailure Create(
        InventoryFailureKind kind,
        string code,
        string message,
        string? component = null,
        string? source = null,
        InventoryFailure? cause = null) =>
        new(kind, code, message, component, source, cause);

    public static InventoryFailure Wrap(
        InventoryFailureKind kind,
        string code,
        string message,
        InventoryFailure? cause,
        string? component = null,
        string? source = null) =>
        new(kind, code, cause == null ? message : $"{message}: {cause.Message}", component, source, cause);

    public static InventoryFailure FromException(Exception exception, InventoryFailureKind kind = InventoryFailureKind.Extension, string? code = null)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
        if (exception is InventorySystemException inventoryException)
            return inventoryException.Failure;

        return new InventoryFailure(kind, code ?? InventoryFailureCodes.ExtensionRejected, exception.Message);
    }

    public static InventoryFailure FromMessage(string? message, InventoryFailureKind kind = InventoryFailureKind.Unknown, string? code = null)
    {
        var resolvedKind = kind == InventoryFailureKind.Unknown ? InferKind(message) : kind;
        return new InventoryFailure(
            resolvedKind,
            code ?? InferCode(resolvedKind, message),
            string.IsNullOrWhiteSpace(message) ? "Inventory operation failed." : message!);
    }

    public static implicit operator InventoryFailure?(string? message)
    {
        return message == null ? null : FromMessage(message);
    }

    public override string ToString()
    {
        if (Cause == null)
            return Message;

        return $"{Message} Cause: {Cause.Message}";
    }

    public bool Equals(InventoryFailure? other)
    {
        if (other == null)
            return false;

        return Kind == other.Kind &&
               Code == other.Code &&
               Message == other.Message &&
               Component == other.Component &&
               Source == other.Source &&
               Equals(Cause, other.Cause);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as InventoryFailure);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)Kind;
            hash = (hash * 397) ^ Code.GetHashCode();
            hash = (hash * 397) ^ Message.GetHashCode();
            hash = (hash * 397) ^ (Component?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (Source?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (Cause?.GetHashCode() ?? 0);
            return hash;
        }
    }

    private static string InferCode(InventoryFailureKind kind, string? message)
    {
        if (message != null)
        {
            if (message.IndexOf("definition", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.DefinitionInvalid;
            if (message.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.MetadataRejected;
            if (message.IndexOf("capacity", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.CapacityRejected;
            if (message.IndexOf("rule", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.RulesRejected;
            if (message.IndexOf("layout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("placement", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.LayoutRejected;
            if (message.IndexOf("transfer", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.TransferRejected;
            if (message.IndexOf("snapshot", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.SnapshotRejected;
            if (message.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) >= 0)
                return InventoryFailureCodes.ConfigurationRejected;
        }

        return kind switch
        {
            InventoryFailureKind.Definition => InventoryFailureCodes.DefinitionInvalid,
            InventoryFailureKind.Metadata => InventoryFailureCodes.MetadataRejected,
            InventoryFailureKind.Stacking => InventoryFailureCodes.StackingRejected,
            InventoryFailureKind.Capacity => InventoryFailureCodes.CapacityRejected,
            InventoryFailureKind.Rules => InventoryFailureCodes.RulesRejected,
            InventoryFailureKind.Layout => InventoryFailureCodes.LayoutRejected,
            InventoryFailureKind.Transfer => InventoryFailureCodes.TransferRejected,
            InventoryFailureKind.Transaction => InventoryFailureCodes.TransactionRejected,
            InventoryFailureKind.Persistence => InventoryFailureCodes.PersistenceRejected,
            InventoryFailureKind.Snapshot => InventoryFailureCodes.SnapshotRejected,
            InventoryFailureKind.Configuration => InventoryFailureCodes.ConfigurationRejected,
            InventoryFailureKind.Extension => InventoryFailureCodes.ExtensionRejected,
            InventoryFailureKind.Validation => InventoryFailureCodes.ValidationRejected,
            _ => InventoryFailureCodes.Unknown
        };
    }

    private static InventoryFailureKind InferKind(string? message)
    {
        if (message == null)
            return InventoryFailureKind.Unknown;
        if (message.IndexOf("definition", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Definition;
        if (message.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Metadata;
        if (message.IndexOf("capacity", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Capacity;
        if (message.IndexOf("rule", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Rules;
        if (message.IndexOf("layout", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("context", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("placement", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Layout;
        if (message.IndexOf("transfer", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Transfer;
        if (message.IndexOf("snapshot", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Snapshot;
        if (message.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) >= 0)
            return InventoryFailureKind.Configuration;
        return InventoryFailureKind.Unknown;
    }
}
