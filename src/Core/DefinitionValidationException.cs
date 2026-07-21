using System;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Represents a domain validation failure found while validating an item definition against its schema.
/// </summary>
public class DefinitionValidationException : InventorySystemException
{
    /// <summary>
    /// Creates a definition validation exception.
    /// </summary>
    public DefinitionValidationException()
        : this("Definition validation failed.")
    {
    }

    /// <summary>
    /// Creates a definition validation exception with a message.
    /// </summary>
    /// <param name="message">The validation failure message.</param>
    public DefinitionValidationException(string message)
        : this(InventoryFailure.Create(InventoryFailureKind.Definition, InventoryFailureCodes.DefinitionInvalid, message))
    {
    }

    /// <summary>
    /// Creates a definition validation exception with a structured failure.
    /// </summary>
    /// <param name="failure">The structured definition validation failure.</param>
    public DefinitionValidationException(InventoryFailure failure)
        : base(failure)
    {
    }

    /// <summary>
    /// Creates a definition validation exception with a message and inner exception.
    /// </summary>
    /// <param name="message">The validation failure message.</param>
    /// <param name="innerException">The exception that caused this validation exception.</param>
    public DefinitionValidationException(string message, Exception innerException)
        : base(InventoryFailure.Create(InventoryFailureKind.Definition, InventoryFailureCodes.DefinitionInvalid, message), innerException)
    {
    }
}
