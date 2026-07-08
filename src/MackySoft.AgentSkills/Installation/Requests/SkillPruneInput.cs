using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one SKILL prune service input. </summary>
/// <param name="CatalogId"> The product-owned catalog whose removed managed installs may be pruned. </param>
/// <param name="CurrentCatalogPackages"> The complete current canonical package set for <paramref name="CatalogId" />. </param>
/// <param name="TargetRequest"> The host target request. </param>
/// <param name="DryRun"> Whether to return a plan without deleting from the file system. </param>
/// <param name="Force">
/// Whether managed orphan targets with local modifications can be deleted. Force does not allow deleting unmanaged
/// targets, foreign catalogs, invalid manifests, name collisions, host conflicts, or path-safety failures.
/// </param>
/// <param name="SelectedTiers"> Optional tier filter for installed managed targets. Empty or <see langword="null" /> means every tier. </param>
/// <param name="SelectedSkillNames"> Optional exact skill-name filter for installed target directories. Empty or <see langword="null" /> means every skill name. </param>
public sealed record SkillPruneInput (
    SkillCatalogId CatalogId,
    IReadOnlyList<CanonicalSkillPackage> CurrentCatalogPackages,
    SkillInstallRequest TargetRequest,
    bool DryRun = false,
    bool Force = false,
    IReadOnlyList<SkillTier>? SelectedTiers = null,
    IReadOnlyList<SkillName>? SelectedSkillNames = null);
