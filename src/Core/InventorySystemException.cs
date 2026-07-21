using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Base exception for expected-success inventory-system wrappers that fail due to domain rejection.
/// </summary>
public class InventorySystemException : InvalidOperationException
{
    /// <summary>
    /// Creates an exception for a structured inventory failure.
    /// </summary>
    /// <param name="failure">The structured failure.</param>
    public InventorySystemException(InventoryFailure failure)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Message)
    {
        Failure = failure;
    }

    /// <summary>
    /// Creates an exception for a structured inventory failure with an inner exception.
    /// </summary>
    /// <param name="failure">The structured failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public InventorySystemException(InventoryFailure failure, Exception? innerException)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Message, innerException)
    {
        Failure = failure;
    }

    /// <summary>The structured inventory failure.</summary>
    public InventoryFailure Failure { get; }
}
