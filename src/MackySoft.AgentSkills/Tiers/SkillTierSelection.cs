using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tiers;

/// <summary> Normalizes product-owned SKILL tier literal selections. </summary>
public static class SkillTierSelection
{
    /// <summary> Parses selected tier literals against product-owned defined tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTierLiterals"> The selected tier literals. </param>
    /// <param name="requireAny"> Whether an empty selection is rejected. </param>
    /// <returns> The normalized selected tiers or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<SkillTier>> Parse (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<string>? selectedTierLiterals,
        bool requireAny = true)
    {
        var definedTiersResult = ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return definedTiersResult;
        }

        if (selectedTierLiterals is null || selectedTierLiterals.Count == 0)
        {
            return requireAny
                ? SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    "At least one SKILL tier must be selected.")
                : SkillOperationResult<IReadOnlyList<SkillTier>>.Success(Array.Empty<SkillTier>());
        }

        var definedTiers = definedTiersResult.Value!;
        var definedTierSet = definedTiers.ToHashSet();
        var selectedTiers = new List<SkillTier>(selectedTierLiterals.Count);
        var selectedTierSet = new HashSet<SkillTier>();
        foreach (var literal in selectedTierLiterals)
        {
            if (!SkillTier.TryCreate(literal, out var tier) || tier is null || !definedTierSet.Contains(tier))
            {
                return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Unsupported SKILL tier: {literal}. Supported tiers: {string.Join(", ", definedTiers.Select(static item => item.Value))}.");
            }

            if (selectedTierSet.Add(tier))
            {
                selectedTiers.Add(tier);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillTier>>.Success(selectedTiers);
    }

    /// <summary> Parses product-owned defined tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <returns> The defined tiers or an input failure. </returns>
    public static SkillOperationResult<IReadOnlyList<SkillTier>> ParseDefinedTiers (IReadOnlyList<string> definedTierLiterals)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);

        if (definedTierLiterals.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "SKILL tier definitions must not be empty.");
        }

        var tiers = new List<SkillTier>(definedTierLiterals.Count);
        var tierSet = new HashSet<SkillTier>();
        foreach (var literal in definedTierLiterals)
        {
            if (!SkillTier.TryCreate(literal, out var tier) || tier is null)
            {
                return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"SKILL tier definition is invalid: {literal}.");
            }

            if (!tierSet.Add(tier))
            {
                return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"SKILL tier definition contains a duplicate literal: {tier.Value}.");
            }

            tiers.Add(tier);
        }

        return SkillOperationResult<IReadOnlyList<SkillTier>>.Success(tiers);
    }
}
