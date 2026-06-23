using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Represents the number of bundled SKILL packages assigned to one product-owned tier. </summary>
/// <param name="Tier"> The product-owned SKILL tier. </param>
/// <param name="PackageCount"> The number of bundled packages assigned to <paramref name="Tier" />. </param>
public sealed record SkillTierPackageCount (
    SkillTier Tier,
    int PackageCount);
