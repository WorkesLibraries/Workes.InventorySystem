using Workes.InventorySystem.Stacking;
using Workes.InventorySystem.Layout;
using Workes.InventorySystem.Capacity;
using Workes.InventorySystem.Rules;
using System;
namespace Workes.InventorySystem.Core;

/// <summary>
/// Creates inventories that share a catalog and default inventory policies.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public class InventoryManager<TKey>
{
    /// <summary>
    /// Gets or sets the stack resolver used when an inventory does not override it.
    /// </summary>
    public IStackResolver<TKey> DefaultStackResolver { get; set; }

    /// <summary>
    /// Gets or sets the capacity policy used when an inventory does not override it.
    /// </summary>
    public ICapacityPolicy<TKey> DefaultCapacityPolicy { get; set; }

    /// <summary>
    /// Gets or sets the layout cloned for inventories that do not override it.
    /// </summary>
    public IInventoryLayout<TKey> DefaultLayout { get; set; }

    /// <summary>
    /// Gets or sets the rule container used when an inventory does not override it.
    /// </summary>
    public RuleContainer<TKey> DefaultRules { get; set; }

    /// <summary>
    /// Gets the item catalog shared by inventories from this manager.
    /// </summary>
    public ItemCatalog<TKey> Catalog { get; }

    /// <summary>
    /// Gets the item registry from <see cref="Catalog"/>.
    /// </summary>
    public ItemRegistry<TKey> Registry => Catalog.Registry;

    /// <summary>
    /// Creates an inventory manager.
    /// </summary>
    /// <param name="defaultStackResolver">The default stack resolver.</param>
    /// <param name="defaultCapacityPolicy">The default capacity policy.</param>
    /// <param name="defaultLayout">The default layout cloned for new inventories.</param>
    /// <param name="defaultRules">Optional default rules.</param>
    /// <param name="catalog">Optional item catalog; a new catalog is created when omitted.</param>
    public InventoryManager(
        IStackResolver<TKey> defaultStackResolver,
        ICapacityPolicy<TKey> defaultCapacityPolicy,
        IInventoryLayout<TKey> defaultLayout,
        RuleContainer<TKey>? defaultRules = null,
        ItemCatalog<TKey>? catalog = null)
    {
        DefaultStackResolver = defaultStackResolver;
        DefaultCapacityPolicy = defaultCapacityPolicy;
        DefaultLayout = defaultLayout;
        Catalog = catalog ?? new ItemCatalog<TKey>();
        if (defaultRules != null)
            DefaultRules = defaultRules;
        else
            DefaultRules = new RuleContainer<TKey>();
    }

    /// <summary>
    /// Creates an inventory using the manager's default stack resolver, capacity policy, and layout.
    /// </summary>
    /// <returns>A new inventory with cloned layout state.</returns>
    /// <exception cref="InvalidOperationException">The item registry has not been frozen.</exception>
    public Inventory<TKey> CreateInventory()
    {
        EnsureFrozen();

        return new Inventory<TKey>(
            this,
            DefaultStackResolver,
            DefaultCapacityPolicy,
            DefaultLayout.Clone(),
            DefaultRules.Clone());
    }

    /// <summary>
    /// Creates an inventory with the given layout and capacity policy.
    /// </summary>
    /// <param name="stackResolver">Optional stack resolver override.</param>
    /// <param name="layout">Optional layout override. The layout is cloned for the new inventory.</param>
    /// <param name="capacityPolicy">Optional capacity policy override.</param>
    /// <param name="rules">Optional rules override.</param>
    /// <returns>A new inventory using the resolved policies.</returns>
    /// <exception cref="InvalidOperationException">The item registry has not been frozen.</exception>
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
            (rules ?? DefaultRules).Clone());
    }

    private void EnsureFrozen()
    {
        if (!Registry.Frozen)
            throw new InvalidOperationException("Item registry has not yet been frozen. Inventory creation is not allowed until the registry is frozen.");
    }
}
