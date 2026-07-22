using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Workes.InventorySystem.Layout;

namespace Workes.InventorySystem.Core;

/// <summary>
/// Describes optional label-based guidance for applying an <see cref="InventoryItemDelta{TKey}"/>.
/// </summary>
/// <remarks>
/// This type is intentionally inert in the delta-core and initial transaction-application slice. Placement and removal
/// selector behavior is implemented by the full application-plan pass.
/// </remarks>
/// <typeparam name="TKey">The item definition identifier type.</typeparam>
public sealed class InventoryDeltaApplicationPlan<TKey>
{
    private readonly List<AdditionRule> _additionRules = new();
    private readonly List<RemovalRule> _removalRules = new();

    /// <summary>Creates an empty application plan.</summary>
    public InventoryDeltaApplicationPlan()
    {
    }

    /// <summary>Creates an empty application plan.</summary>
    public static InventoryDeltaApplicationPlan<TKey> Create() => new();

    /// <summary>Gets addition rules in insertion order.</summary>
    public IReadOnlyList<string> AdditionRuleDescriptions =>
        new ReadOnlyCollection<string>(_additionRules.Select(rule => rule.Description).ToList());

    /// <summary>Gets removal rules in insertion order.</summary>
    public IReadOnlyList<string> RemovalRuleDescriptions =>
        new ReadOnlyCollection<string>(_removalRules.Select(rule => rule.Description).ToList());

    /// <summary>Adds an addition rule matching an original operation label.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForAdditionLabel(
        string label,
        Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> selector)
    {
        ValidateLabel(label, nameof(label));
        return AddAdditionRule($"addition label '{label}'", operation => MatchesOriginalLabel(operation, label), selector);
    }

    /// <summary>Adds an addition rule matching a combine prefix.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForAdditionPrefix(
        string prefix,
        Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> selector)
    {
        ValidateLabel(prefix, nameof(prefix));
        return AddAdditionRule($"addition prefix '{prefix}'", operation => MatchesPrefix(operation, prefix), selector);
    }

    /// <summary>Adds an addition rule matching an exact combined label.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForAdditionCombinedLabel(
        string combinedLabel,
        Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> selector)
    {
        ValidateLabel(combinedLabel, nameof(combinedLabel));
        return AddAdditionRule($"addition combined label '{combinedLabel}'", operation => MatchesCombinedLabel(operation, combinedLabel), selector);
    }

    /// <summary>Adds a removal rule matching an original operation label.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForRemovalLabel(
        string label,
        Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> selector)
    {
        ValidateLabel(label, nameof(label));
        return AddRemovalRule($"removal label '{label}'", operation => MatchesOriginalLabel(operation, label), selector);
    }

    /// <summary>Adds a removal rule matching a combine prefix.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForRemovalPrefix(
        string prefix,
        Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> selector)
    {
        ValidateLabel(prefix, nameof(prefix));
        return AddRemovalRule($"removal prefix '{prefix}'", operation => MatchesPrefix(operation, prefix), selector);
    }

    /// <summary>Adds a removal rule matching an exact combined label.</summary>
    public InventoryDeltaApplicationPlan<TKey> ForRemovalCombinedLabel(
        string combinedLabel,
        Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> selector)
    {
        ValidateLabel(combinedLabel, nameof(combinedLabel));
        return AddRemovalRule($"removal combined label '{combinedLabel}'", operation => MatchesCombinedLabel(operation, combinedLabel), selector);
    }

    internal bool TryResolvePlacement(
        Inventory<TKey> inventory,
        InventoryItemDeltaOperation<TKey> operation,
        out ILayoutContext<TKey>? context,
        out InventoryFailure? failure)
    {
        context = null;
        failure = null;
        var rule = _additionRules.FirstOrDefault(candidate => candidate.Matches(operation));
        if (rule == null)
            return true;

        var decision = rule.Selector(new InventoryDeltaAdditionRequest<TKey>(inventory, operation));
        if (decision == null)
        {
            failure = InventoryFailures.Transaction($"Application plan rule '{rule.Description}' returned no placement decision.");
            return false;
        }
        if (decision.IsRejected)
        {
            failure = decision.Failure ?? InventoryFailures.Transaction($"Application plan rule '{rule.Description}' rejected addition.");
            return false;
        }

        context = decision.Context;
        return true;
    }

    internal bool TryAcceptRemovalCandidate(
        Inventory<TKey> inventory,
        InventoryItemDeltaOperation<TKey> operation,
        ItemInstance<TKey> instance,
        int storageIndex,
        int plannedAmount,
        IReadOnlyList<ILayoutContext<TKey>> contexts,
        out bool accepted,
        out InventoryFailure? failure)
    {
        accepted = true;
        failure = null;
        var rule = _removalRules.FirstOrDefault(candidate => candidate.Matches(operation));
        if (rule == null)
            return true;

        var decision = rule.Selector(new InventoryDeltaRemovalCandidate<TKey>(
            inventory,
            operation,
            instance,
            storageIndex,
            plannedAmount,
            contexts));
        if (decision == null)
        {
            failure = InventoryFailures.Transaction($"Application plan rule '{rule.Description}' returned no removal decision.");
            return false;
        }
        if (decision.IsRejected)
        {
            failure = decision.Failure ?? InventoryFailures.Transaction($"Application plan rule '{rule.Description}' rejected removal.");
            return false;
        }

        accepted = decision.Accepted;
        return true;
    }

    private InventoryDeltaApplicationPlan<TKey> AddAdditionRule(
        string description,
        Func<InventoryItemDeltaOperation<TKey>, bool> matches,
        Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> selector)
    {
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));
        _additionRules.Add(new AdditionRule(description, matches, selector));
        return this;
    }

    private InventoryDeltaApplicationPlan<TKey> AddRemovalRule(
        string description,
        Func<InventoryItemDeltaOperation<TKey>, bool> matches,
        Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> selector)
    {
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));
        _removalRules.Add(new RemovalRule(description, matches, selector));
        return this;
    }

    private static bool MatchesOriginalLabel(InventoryItemDeltaOperation<TKey> operation, string label)
    {
        ValidateLabel(label, nameof(label));
        return operation.Label == label
            || operation.LabelReferences.Any(reference => reference.OriginalLabel == label);
    }

    private static bool MatchesPrefix(InventoryItemDeltaOperation<TKey> operation, string prefix)
    {
        ValidateLabel(prefix, nameof(prefix));
        return operation.LabelReferences.Any(reference => reference.Prefix == prefix);
    }

    private static bool MatchesCombinedLabel(InventoryItemDeltaOperation<TKey> operation, string combinedLabel)
    {
        ValidateLabel(combinedLabel, nameof(combinedLabel));
        return operation.LabelReferences.Any(reference => reference.CombinedLabel == combinedLabel);
    }

    private static void ValidateLabel(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Label cannot be null, empty, or whitespace.", parameterName);
    }

    private sealed class AdditionRule
    {
        public AdditionRule(
            string description,
            Func<InventoryItemDeltaOperation<TKey>, bool> matches,
            Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> selector)
        {
            Description = description;
            Matches = matches;
            Selector = selector;
        }

        public string Description { get; }

        public Func<InventoryItemDeltaOperation<TKey>, bool> Matches { get; }

        public Func<InventoryDeltaAdditionRequest<TKey>, InventoryPlacementDecision<TKey>> Selector { get; }
    }

    private sealed class RemovalRule
    {
        public RemovalRule(
            string description,
            Func<InventoryItemDeltaOperation<TKey>, bool> matches,
            Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> selector)
        {
            Description = description;
            Matches = matches;
            Selector = selector;
        }

        public string Description { get; }

        public Func<InventoryItemDeltaOperation<TKey>, bool> Matches { get; }

        public Func<InventoryDeltaRemovalCandidate<TKey>, InventoryRemovalDecision> Selector { get; }
    }
}
