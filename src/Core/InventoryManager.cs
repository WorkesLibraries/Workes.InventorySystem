using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Rules;
using System;
namespace Workes.InventorySystem.Core;

public class InventoryManager<TKey>
{
    public IStackResolver<TKey> DefaultStackResolver { get; set; }
    public ICapacityPolicy<TKey> DefaultCapacityPolicy { get; set; }
    public IInventoryLayout<TKey> DefaultLayout { get; set; }
    public RuleContainer<TKey> DefaultRules { get; set; }

    private readonly ItemRegistry<TKey> _registry = new();

    public ItemRegistry<TKey> Registry => _registry;

    public InventoryManager(IStackResolver<TKey> defaultStackResolver, ICapacityPolicy<TKey> defaultCapacityPolicy, IInventoryLayout<TKey> defaultLayout, RuleContainer<TKey>? defaultRules = null)
    {
        DefaultStackResolver = defaultStackResolver;
        DefaultCapacityPolicy = defaultCapacityPolicy;
        DefaultLayout = defaultLayout;
        if (defaultRules != null)
            DefaultRules = defaultRules;
        else
            DefaultRules = new RuleContainer<TKey>();
    }

    /// <summary>
    /// Creates an inventory using the manager's default stack resolver, capacity policy, and layout.
    /// </summary>
    public Inventory<TKey> CreateInventory()
    {
        EnsureFrozen();

        return new Inventory<TKey>(
            this,
            DefaultStackResolver,
            DefaultCapacityPolicy,
            DefaultLayout.Clone(),
            DefaultRules);
    }

    /// <summary>
    /// Creates an inventory with the given layout and capacity policy.
    /// </summary>
    public Inventory<TKey> CreateInventory(
        IStackResolver<TKey>? stackResolver = null,
        IInventoryLayout<TKey>? layout = null,
        ICapacityPolicy<TKey>? capacityPolicy = null,
        RuleContainer<TKey>? rules = null)
    {
        EnsureFrozen();

        return new Inventory<TKey>(
            this,
            stackResolver ?? DefaultStackResolver,
            capacityPolicy ?? DefaultCapacityPolicy,
            (layout ?? DefaultLayout).Clone(),
            rules ?? DefaultRules);
    }

    private void EnsureFrozen()
    {
        if (!Registry.Frozen)
            throw new InvalidOperationException("Item registry has not yet been frozen. Inventory creation is not allowed until the registry is frozen.");
    }
}
