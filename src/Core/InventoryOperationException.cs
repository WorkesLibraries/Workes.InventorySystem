namespace Workes.InventorySystem.Core;

/// <summary>
/// Exception thrown by expected-success inventory operation wrappers when validation rejects the operation.
/// </summary>
public sealed class InventoryOperationException : InventorySystemException
{
    /// <summary>
    /// Creates an inventory operation exception.
    /// </summary>
    /// <param name="failure">The structured operation failure.</param>
    public InventoryOperationException(InventoryFailure failure)
        : base(failure)
    {
    }
}
