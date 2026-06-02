using Workes.InventorySystem.Core;
using System.Collections.Generic;
namespace Workes.InventorySystem.Stacking;

/// <summary>
/// Stack resolver that returns the same maximum stack size for every item.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public class DefaultStackResolver<TKey> : IParameterizedStackResolver<TKey>
{
    private readonly int _defaultMaxStack;
    private static readonly IReadOnlyCollection<InventoryParameterDefinition> s_parameters =
        new[]
        {
            new InventoryParameterDefinition("maxStack", typeof(int), "Maximum amount allowed in each compatible stack.")
        };

    /// <summary>
    /// Gets the fixed maximum amount allowed in each compatible stack.
    /// </summary>
    public int DefaultMaxStack => _defaultMaxStack;

    /// <inheritdoc />
    public IReadOnlyCollection<InventoryParameterDefinition> Parameters => s_parameters;

    /// <summary>
    /// Creates a stack resolver with a fixed maximum stack size.
    /// </summary>
    /// <param name="defaultMaxStack">The maximum amount allowed in each stack.</param>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="defaultMaxStack"/> is less than or equal to zero.</exception>
    public DefaultStackResolver(int defaultMaxStack)
    {
        if (defaultMaxStack <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(defaultMaxStack), "Maximum stack size must be greater than zero.");

        _defaultMaxStack = defaultMaxStack;
    }

    /// <inheritdoc />
    public int ResolveMaxStackSize(Inventory<TKey> inventory, ItemInstance<TKey> instance) => _defaultMaxStack;

    /// <inheritdoc />
    public bool TryCreateWithParameter(
        Inventory<TKey> inventory,
        string parameterId,
        object? value,
        out IStackResolver<TKey>? resolver,
        out string? error)
    {
        resolver = null;
        if (parameterId != "maxStack")
        {
            error = $"Parameter '{parameterId}' is not supported by DefaultStackResolver.";
            return false;
        }

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

        resolver = new DefaultStackResolver<TKey>(maxStack);
        error = null;
        return true;
    }
}
