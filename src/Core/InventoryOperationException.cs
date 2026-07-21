namespace Workes.InventorySystem.Core;

/// <summary>
/// Exception thrown by expected-success inventory operation wrappers when validation rejects the operation.
/// </summary>
public sealed class InventoryOperationException : InventorySystemException
{
    public InventoryOperationException(InventoryFailure failure)
        : base(failure)
    {
    }
}
