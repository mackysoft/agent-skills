using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Represents bundled SKILL package inventory validated against product-owned tier literals. </summary>
/// <param name="SelectedTiers"> The caller's product-owned root tier selection. </param>
/// <param name="SelectedSkillNames"> The caller's exact root SKILL name selection. Empty means no name filter. </param>
/// <param name="AvailableTiers"> The complete product-owned tier list with package counts in definition order. </param>
/// <param name="Packages"> The selected root packages and their transitive dependencies, sorted by skill name using ordinal comparison. </param>
public sealed record SkillPackageCatalog (
    IReadOnlyList<SkillTier> SelectedTiers,
    IReadOnlyList<SkillName> SelectedSkillNames,
    IReadOnlyList<SkillTierPackageCount> AvailableTiers,
    IReadOnlyList<CanonicalSkillPackage> Packages);
