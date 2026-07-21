using System;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Base exception for expected-success inventory-system wrappers that fail due to domain rejection.
/// </summary>
public class InventorySystemException : InvalidOperationException
{
    public InventorySystemException(InventoryFailure failure)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Message)
    {
        Failure = failure;
    }

    public InventorySystemException(InventoryFailure failure, Exception? innerException)
        : base((failure ?? throw new ArgumentNullException(nameof(failure))).Message, innerException)
    {
        Failure = failure;
    }

    public InventoryFailure Failure { get; }
}
