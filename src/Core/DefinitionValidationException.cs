using System;
namespace Workes.InventorySystem.Core;

public class DefinitionValidationException : Exception
{
    public DefinitionValidationException()
    {
    }

    public DefinitionValidationException(string message)
        : base(message)
    {
    }

    public DefinitionValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
