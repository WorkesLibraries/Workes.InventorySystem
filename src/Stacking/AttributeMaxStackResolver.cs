using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using System.ComponentModel;

namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that reads each definition's maximum stack size from an integer definition attribute.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Attributes are read from item definitions, not instance metadata. Missing stack-size
/// attributes do not fail catalog freeze by themselves. When <see cref="MissingAttributeMaxStack"/>
/// is <see langword="null"/>, resolving a definition missing the attribute throws
/// <see cref="InvalidOperationException"/>.
/// </remarks>
public sealed class AttributeMaxStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("missingAttributeMaxStack", typeof(int), "Fallback max stack size for definitions missing the stack-size attribute; null enables strict mode.")
        };

    /// <summary>
    /// Gets the integer definition attribute id that provides maximum stack size.
    /// </summary>
    public string MaxStackAttributeId { get; }

    /// <summary>
    /// Gets the fallback max stack size for definitions missing the attribute, or <see langword="null"/> for strict mode.
    /// </summary>
    public int? MissingAttributeMaxStack { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates an attribute-defined max-stack resolver.
    /// </summary>
    /// <param name="maxStackAttributeId">The integer definition attribute id that provides maximum stack size.</param>
    /// <param name="missingAttributeMaxStack">Optional fallback for definitions missing the attribute. <see langword="null"/> enables strict mode.</param>
    /// <exception cref="ArgumentException"><paramref name="maxStackAttributeId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="missingAttributeMaxStack"/> is less than or equal to zero.</exception>
    public AttributeMaxStackResolver(
        string maxStackAttributeId,
        int? missingAttributeMaxStack = null)
    {
        if (string.IsNullOrWhiteSpace(maxStackAttributeId))
            throw new ArgumentException("Max stack attribute id cannot be null or empty.", nameof(maxStackAttributeId));
        if (missingAttributeMaxStack.HasValue && missingAttributeMaxStack.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(missingAttributeMaxStack), "Missing-attribute fallback must be greater than zero.");

        MaxStackAttributeId = maxStackAttributeId;
        MissingAttributeMaxStack = missingAttributeMaxStack;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (instance.Definition.Attributes.TryGet<int>(MaxStackAttributeId, out var maxStack))
        {
            if (maxStack <= 0)
                throw new InvalidOperationException($"Item definition '{instance.Definition.Id}' attribute '{MaxStackAttributeId}' must be greater than zero.");

            return maxStack;
        }

        if (MissingAttributeMaxStack.HasValue)
            return MissingAttributeMaxStack.Value;

        throw new InvalidOperationException($"Item definition '{instance.Definition.Id}' is missing stack-size attribute '{MaxStackAttributeId}'.");
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IStackResolver<TKey>? resolver,
        out InventoryFailure? failure)
    {
        resolver = null;
        if (parameterId != "missingAttributeMaxStack")
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' is not supported by AttributeMaxStackResolver.");
            return false;
        }

        if (value == null)
        {
            resolver = new AttributeMaxStackResolver<TKey>(MaxStackAttributeId, null);
            failure = null;
            return true;
        }

        if (value is not int missingAttributeMaxStack)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter 'missingAttributeMaxStack' expects value type 'Int32' or null.");
            return false;
        }

        if (missingAttributeMaxStack <= 0)
        {
            failure = InventoryFailures.Stacking("Missing-attribute fallback must be greater than zero.");
            return false;
        }

        resolver = new AttributeMaxStackResolver<TKey>(MaxStackAttributeId, missingAttributeMaxStack);
        failure = null;
        return true;
    }
}
