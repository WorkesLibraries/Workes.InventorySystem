using System;
using System.Collections.Generic;
using System.ComponentModel;
using Workes.InventorySystem.Core;

namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that reads a definition attribute as a base stack value and multiplies it by resolver state.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
/// <remarks>
/// Attributes are read from item definitions, not instance metadata. The definition attribute must be an
/// <see cref="int"/>. Missing base-stack attributes do not fail catalog freeze by themselves. When
/// <see cref="MissingAttributeBaseStack"/> is <see langword="null"/>, resolving a definition missing the
/// attribute throws <see cref="InvalidOperationException"/>. Computed values are floored and clamped to a
/// minimum max stack size of one.
/// </remarks>
public sealed class MultipliedAttributeStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("multiplier", typeof(double), "Multiplier applied to each definition's base stack attribute."),
            new InventoryParameterDefinition("missingAttributeBaseStack", typeof(int), "Fallback base stack value for definitions missing the stack-ratio attribute; null enables strict mode.")
        };

    /// <summary>
    /// Gets the integer definition attribute id that provides the base stack value.
    /// </summary>
    public string BaseStackAttributeId { get; }

    /// <summary>
    /// Gets the multiplier applied to each definition's base stack value.
    /// </summary>
    public double Multiplier { get; }

    /// <summary>
    /// Gets the fallback base stack value for definitions missing the attribute, or <see langword="null"/> for strict mode.
    /// </summary>
    public int? MissingAttributeBaseStack { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates a multiplied attribute stack resolver.
    /// </summary>
    /// <param name="baseStackAttributeId">The integer definition attribute id that provides the base stack value.</param>
    /// <param name="multiplier">The inventory-specific multiplier applied to the base stack value.</param>
    /// <param name="missingAttributeBaseStack">Optional fallback for definitions missing the attribute. <see langword="null"/> enables strict mode.</param>
    /// <exception cref="ArgumentException"><paramref name="baseStackAttributeId"/> is null, empty, or whitespace, or <paramref name="multiplier"/> is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiplier"/> or <paramref name="missingAttributeBaseStack"/> is less than or equal to zero.</exception>
    public MultipliedAttributeStackResolver(
        string baseStackAttributeId,
        double multiplier,
        int? missingAttributeBaseStack = null)
    {
        if (string.IsNullOrWhiteSpace(baseStackAttributeId))
            throw new ArgumentException("Base stack attribute id cannot be null or empty.", nameof(baseStackAttributeId));
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            throw new ArgumentException("Multiplier must be a finite number.", nameof(multiplier));
        if (multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than zero.");
        if (missingAttributeBaseStack.HasValue && missingAttributeBaseStack.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(missingAttributeBaseStack), "Missing-attribute fallback must be greater than zero.");

        BaseStackAttributeId = baseStackAttributeId;
        Multiplier = multiplier;
        MissingAttributeBaseStack = missingAttributeBaseStack;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var baseStack = ResolveBaseStack(instance);
        var computed = baseStack * Multiplier;
        if (computed > int.MaxValue)
            throw new InvalidOperationException($"Item definition '{instance.Definition.Id}' computed max stack size exceeds Int32.MaxValue.");

        return Math.Max(1, (int)Math.Floor(computed));
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
        if (parameterId == "multiplier")
        {
            if (value is not double multiplier)
            {
                error = "Parameter 'multiplier' expects value type 'Double'.";
                return false;
            }

            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                error = "Multiplier must be a finite number.";
                return false;
            }

            if (multiplier <= 0)
            {
                error = "Multiplier must be greater than zero.";
                return false;
            }

            resolver = new MultipliedAttributeStackResolver<TKey>(BaseStackAttributeId, multiplier, MissingAttributeBaseStack);
            error = null;
            return true;
        }

        if (parameterId == "missingAttributeBaseStack")
        {
            if (value == null)
            {
                resolver = new MultipliedAttributeStackResolver<TKey>(BaseStackAttributeId, Multiplier, null);
                error = null;
                return true;
            }

            if (value is not int missingAttributeBaseStack)
            {
                error = "Parameter 'missingAttributeBaseStack' expects value type 'Int32' or null.";
                return false;
            }

            if (missingAttributeBaseStack <= 0)
            {
                error = "Missing-attribute fallback must be greater than zero.";
                return false;
            }

            resolver = new MultipliedAttributeStackResolver<TKey>(BaseStackAttributeId, Multiplier, missingAttributeBaseStack);
            error = null;
            return true;
        }

        error = $"Parameter '{parameterId}' is not supported by MultipliedAttributeStackResolver.";
        return false;
    }

    private int ResolveBaseStack(ItemInstance<TKey> instance)
    {
        if (instance.Definition.Attributes.TryGet<int>(BaseStackAttributeId, out var baseStack))
        {
            if (baseStack <= 0)
                throw new InvalidOperationException($"Item definition '{instance.Definition.Id}' attribute '{BaseStackAttributeId}' must be greater than zero.");

            return baseStack;
        }

        if (MissingAttributeBaseStack.HasValue)
            return MissingAttributeBaseStack.Value;

        throw new InvalidOperationException($"Item definition '{instance.Definition.Id}' is missing stack-ratio attribute '{BaseStackAttributeId}'.");
    }
}
