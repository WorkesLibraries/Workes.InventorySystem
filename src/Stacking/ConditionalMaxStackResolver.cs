using System;
using System.Collections.Generic;
using Workes.InventorySystem.Core;
using System.ComponentModel;

namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that uses a boolean definition attribute to decide whether an item can stack.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Attributes are read from item definitions, not instance metadata. Missing attributes
/// do not fail catalog freeze; they are handled by <see cref="MissingAttributeIsStackable"/>
/// when this resolver is used.
/// </remarks>
public sealed class ConditionalMaxStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("maxStack", typeof(int), "Maximum amount allowed when the stackable attribute is true."),
            new InventoryParameterDefinition("missingAttributeIsStackable", typeof(bool), "Whether definitions missing the stackable attribute should stack.")
        };

    /// <summary>
    /// Gets the boolean definition attribute id that controls stackability.
    /// </summary>
    public string StackableAttributeId { get; }

    /// <summary>
    /// Gets the maximum stack size used for stackable definitions.
    /// </summary>
    public int MaxStack { get; }

    /// <summary>
    /// Gets whether definitions missing the stackable attribute should be treated as stackable.
    /// </summary>
    public bool MissingAttributeIsStackable { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates a conditional max-stack resolver.
    /// </summary>
    /// <param name="stackableAttributeId">The boolean definition attribute id that controls stackability.</param>
    /// <param name="maxStack">The maximum stack size used when a definition is stackable.</param>
    /// <param name="missingAttributeIsStackable">Whether definitions missing the attribute should be stackable.</param>
    /// <exception cref="ArgumentException"><paramref name="stackableAttributeId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxStack"/> is less than or equal to zero.</exception>
    public ConditionalMaxStackResolver(
        string stackableAttributeId,
        int maxStack,
        bool missingAttributeIsStackable = false)
    {
        if (string.IsNullOrWhiteSpace(stackableAttributeId))
            throw new ArgumentException("Stackable attribute id cannot be null or empty.", nameof(stackableAttributeId));
        if (maxStack <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxStack), "Maximum stack size must be greater than zero.");

        StackableAttributeId = stackableAttributeId;
        MaxStack = maxStack;
        MissingAttributeIsStackable = missingAttributeIsStackable;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (instance.Definition.Attributes.TryGet<bool>(StackableAttributeId, out var stackable))
            return stackable ? MaxStack : 1;

        return MissingAttributeIsStackable ? MaxStack : 1;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IStackResolver<TKey>? resolver,
        out InventoryFailure? error)
    {
        resolver = null;
        if (parameterId == "maxStack")
        {
            if (value is not int maxStack)
            {
                error = "Parameter 'maxStack' expects value type 'Int32'.";
                return false;
            }

            if (maxStack <= 0)
            {
                error = "Maximum stack size must be greater than zero.";
                return false;
            }

            resolver = new ConditionalMaxStackResolver<TKey>(StackableAttributeId, maxStack, MissingAttributeIsStackable);
            error = null;
            return true;
        }

        if (parameterId == "missingAttributeIsStackable")
        {
            if (value is not bool missingAttributeIsStackable)
            {
                error = "Parameter 'missingAttributeIsStackable' expects value type 'Boolean'.";
                return false;
            }

            resolver = new ConditionalMaxStackResolver<TKey>(StackableAttributeId, MaxStack, missingAttributeIsStackable);
            error = null;
            return true;
        }

        error = $"Parameter '{parameterId}' is not supported by ConditionalMaxStackResolver.";
        return false;
    }
}
