using System;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents an error found while validating an item definition against its schema.
/// </summary>
public class DefinitionValidationException : Exception
{
    /// <summary>
    /// Creates a definition validation exception.
    /// </summary>
    public DefinitionValidationException()
    {
    }

    /// <summary>
    /// Creates a definition validation exception with a message.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    public DefinitionValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a definition validation exception with a message and inner exception.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    /// <param name="innerException">The exception that caused this validation exception.</param>
    public DefinitionValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
