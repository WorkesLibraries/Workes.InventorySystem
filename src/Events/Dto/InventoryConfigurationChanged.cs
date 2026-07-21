using System;

namespace Workes.InventorySystem.Events.Dto;

/// <summary>
/// Describes a runtime inventory configuration change.
/// </summary>
/// <typeparam name="TKey">The item definition identifier type used by the inventory.</typeparam>
public sealed class InventoryConfigurationChanged<TKey>
{
    /// <summary>
    /// Gets the kind of component that changed.
    /// </summary>
    public InventoryConfigurationChangeKind Kind { get; }

    /// <summary>
    /// Gets the configuration id that was changed.
    /// </summary>
    public string ConfigurationId { get; }

    /// <summary>
    /// Gets the parameter id that was changed.
    /// </summary>
    [Obsolete("Use ConfigurationId. ParameterId is retained as a compatibility alias and will be removed in a future major version.")]
    public string ParameterId => ConfigurationId;

    /// <summary>
    /// Gets the committed parameter value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the component instance before the change.
    /// </summary>
    public object PreviousComponent { get; }

    /// <summary>
    /// Gets the component instance after the change.
    /// </summary>
    public object CurrentComponent { get; }

    /// <summary>
    /// Gets whether this configuration change includes observable state that the containing event does not completely
    /// describe through semantic payloads and affected contexts.
    /// </summary>
    public bool RequiresFullRefresh { get; }

    /// <summary>
    /// Gets typed rule-change details when <see cref="Kind"/> is <see cref="InventoryConfigurationChangeKind.Rules"/>.
    /// </summary>
    public InventoryRuleConfigurationChanged<TKey>? RuleChange { get; }

    /// <summary>
    /// Creates an inventory configuration change payload.
    /// </summary>
    /// <param name="kind">The kind of component that changed.</param>
    /// <param name="parameterId">The parameter id that changed.</param>
    /// <param name="value">The committed parameter value.</param>
    /// <param name="previousComponent">The component instance before the change.</param>
    /// <param name="currentComponent">The component instance after the change.</param>
    /// <param name="requiresFullRefresh">
    /// Whether the containing event cannot completely describe the observable configuration change.
    /// </param>
    /// <param name="ruleChange">Typed rule-change details when <paramref name="kind"/> is <see cref="InventoryConfigurationChangeKind.Rules"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="parameterId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="previousComponent"/> or <paramref name="currentComponent"/> is <see langword="null"/>.</exception>
    public InventoryConfigurationChanged(
        InventoryConfigurationChangeKind kind,
        string parameterId,
        object? value,
        object previousComponent,
        object currentComponent,
        bool requiresFullRefresh,
        InventoryRuleConfigurationChanged<TKey>? ruleChange = null)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
            throw new ArgumentException("Parameter id cannot be null or empty.", nameof(parameterId));

        Kind = kind;
        ConfigurationId = parameterId;
        Value = value;
        PreviousComponent = previousComponent ?? throw new ArgumentNullException(nameof(previousComponent));
        CurrentComponent = currentComponent ?? throw new ArgumentNullException(nameof(currentComponent));
        RequiresFullRefresh = requiresFullRefresh;
        RuleChange = ruleChange;
    }
}
