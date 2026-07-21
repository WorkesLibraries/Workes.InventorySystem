using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Structured, non-exception result data for an expected inventory-system rejection.
/// </summary>
public sealed class InventoryFailure : IEquatable<InventoryFailure>
{
    /// <summary>
    /// Creates a structured inventory failure.
    /// </summary>
    /// <param name="kind">Broad failure category.</param>
    /// <param name="code">Stable machine-readable failure code.</param>
    /// <param name="message">Human-readable failure message.</param>
    /// <param name="component">Optional component that produced or wrapped the failure.</param>
    /// <param name="source">Optional stable source identifier, such as a rule id.</param>
    /// <param name="cause">Optional nested lower-level failure.</param>
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

    /// <summary>Broad failure category.</summary>
    public InventoryFailureKind Kind { get; }
    /// <summary>Stable machine-readable failure code.</summary>
    public string Code { get; }
    /// <summary>Human-readable failure message.</summary>
    public string Message { get; }
    /// <summary>Optional component that produced or wrapped the failure.</summary>
    public string? Component { get; }
    /// <summary>Optional stable source identifier, such as a rule id.</summary>
    public string? Source { get; }
    /// <summary>Optional nested lower-level failure.</summary>
    public InventoryFailure? Cause { get; }

    /// <summary>
    /// Creates a structured inventory failure.
    /// </summary>
    /// <param name="kind">Broad failure category.</param>
    /// <param name="code">Stable machine-readable failure code.</param>
    /// <param name="message">Human-readable failure message.</param>
    /// <param name="component">Optional component that produced or wrapped the failure.</param>
    /// <param name="source">Optional stable source identifier, such as a rule id.</param>
    /// <param name="cause">Optional nested lower-level failure.</param>
    /// <returns>The created failure.</returns>
    public static InventoryFailure Create(
        InventoryFailureKind kind,
        string code,
        string message,
        string? component = null,
        string? source = null,
        InventoryFailure? cause = null) =>
        new(kind, code, message, component, source, cause);

    /// <summary>
    /// Creates a higher-level failure that preserves a lower-level cause.
    /// </summary>
    /// <param name="kind">Broad failure category.</param>
    /// <param name="code">Stable machine-readable failure code.</param>
    /// <param name="message">Human-readable wrapping message.</param>
    /// <param name="cause">Optional lower-level cause.</param>
    /// <param name="component">Optional component that wrapped the failure.</param>
    /// <param name="source">Optional stable source identifier, such as a rule id.</param>
    /// <returns>The wrapped failure.</returns>
    public static InventoryFailure Wrap(
        InventoryFailureKind kind,
        string code,
        string message,
        InventoryFailure? cause,
        string? component = null,
        string? source = null) =>
        new(kind, code, cause == null ? message : $"{message}: {cause.Message}", component, source, cause);

    /// <summary>
    /// Converts an exception from an expected extension path into a structured failure.
    /// </summary>
    /// <param name="exception">The exception to convert.</param>
    /// <param name="kind">Failure category to use for non-inventory exceptions.</param>
    /// <param name="code">Optional stable code to use for non-inventory exceptions.</param>
    /// <returns>The structured failure.</returns>
    public static InventoryFailure FromException(Exception exception, InventoryFailureKind kind = InventoryFailureKind.Extension, string? code = null)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));
        if (exception is InventorySystemException inventoryException)
            return inventoryException.Failure;

        return new InventoryFailure(kind, code ?? InventoryFailureCodes.ExtensionRejected, exception.Message);
    }

    /// <summary>
    /// Returns the human-readable message with concise cause context when present.
    /// </summary>
    /// <returns>The displayable failure string.</returns>
    public override string ToString()
    {
        if (Cause == null)
            return Message;

        return $"{Message} Cause: {Cause.Message}";
    }

    /// <summary>
    /// Determines whether this failure is structurally equal to another failure.
    /// </summary>
    /// <param name="other">The other failure.</param>
    /// <returns><see langword="true"/> when all failure fields are equal.</returns>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as InventoryFailure);
    }

    /// <inheritdoc />
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

}
