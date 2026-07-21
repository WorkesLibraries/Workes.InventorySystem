using Workes.InventorySystem.Core;
using System.Collections.Generic;
using System.ComponentModel;
namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that returns the same maximum stack size for every item.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class FixedSizeStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private readonly int _maxStack;
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("maxStack", typeof(int), "Maximum amount allowed in each compatible stack.")
        };

    /// <summary>
    /// Gets the fixed maximum amount allowed in each compatible stack.
    /// </summary>
    public int MaxStack => _maxStack;

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates a stack resolver with a fixed maximum stack size.
    /// </summary>
    /// <param name="maxStack">The maximum amount allowed in each stack.</param>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="maxStack"/> is less than or equal to zero.</exception>
    public FixedSizeStackResolver(int maxStack)
    {
        if (maxStack <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(maxStack), "Maximum stack size must be greater than zero.");

        _maxStack = maxStack;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance) => _maxStack;

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
        if (parameterId != "maxStack")
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter($"Parameter '{parameterId}' is not supported by FixedSizeStackResolver.");
            return false;
        }

        if (value is not int maxStack)
        {
            failure = InventoryFailures.ConfigurationUnsupportedParameter("Parameter 'maxStack' expects value type 'Int32'.");
            return false;
        }

        if (maxStack <= 0)
        {
            failure = InventoryFailures.Stacking("Maximum stack size must be greater than zero.");
            return false;
        }

        resolver = new FixedSizeStackResolver<TKey>(maxStack);
        failure = null;
        return true;
    }
}
