using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one immutable SKILL prune service input. </summary>
public sealed class SkillPruneInput
{
    /// <summary> Initializes one SKILL prune service input. </summary>
    /// <param name="CatalogId"> The product-owned catalog whose removed managed installs may be pruned. </param>
    /// <param name="CurrentCatalogPackages"> The complete current canonical package set for <paramref name="CatalogId" />. </param>
    /// <param name="TargetRequest"> The host target request. </param>
    /// <param name="DryRun"> Whether to return a plan without deleting from the file system. </param>
    /// <param name="Force">
    /// Whether managed orphan targets with local modifications can be deleted. Force does not allow deleting unmanaged
    /// targets, foreign catalogs, invalid manifests, name collisions, host conflicts, or path-safety failures.
    /// </param>
    /// <param name="SelectedCategories"> Optional category filter for installed managed targets. Empty or <see langword="null" /> means every category. </param>
    /// <param name="SelectedSkillNames"> Optional exact skill-name filter for installed target directories. Empty or <see langword="null" /> means every skill name. </param>
    public SkillPruneInput (
        SkillCatalogId CatalogId,
        IReadOnlyList<CanonicalSkillPackage> CurrentCatalogPackages,
        SkillInstallRequest TargetRequest,
        bool DryRun = false,
        bool Force = false,
        IReadOnlyList<SkillCategory>? SelectedCategories = null,
        IReadOnlyList<SkillName>? SelectedSkillNames = null)
    {
        this.CatalogId = CatalogId ?? throw new ArgumentNullException(nameof(CatalogId));
        this.CurrentCatalogPackages = SkillPackageInputSnapshot.Create(
            this.CatalogId,
            CurrentCatalogPackages,
            nameof(CurrentCatalogPackages));
        this.TargetRequest = TargetRequest ?? throw new ArgumentNullException(nameof(TargetRequest));
        this.DryRun = DryRun;
        this.Force = Force;

        if (SelectedCategories is not null)
        {
            var categorySnapshot = SelectedCategories.ToArray();
            if (categorySnapshot.Any(static category => category is null))
            {
                throw new ArgumentException("Selected categories must not contain null items.", nameof(SelectedCategories));
            }

            this.SelectedCategories = Array.AsReadOnly(categorySnapshot);
        }

        if (SelectedSkillNames is not null)
        {
            var skillNameSnapshot = SelectedSkillNames.ToArray();
            if (skillNameSnapshot.Any(static skillName => skillName is null))
            {
                throw new ArgumentException("Selected SKILL names must not contain null items.", nameof(SelectedSkillNames));
            }

            this.SelectedSkillNames = Array.AsReadOnly(skillNameSnapshot);
        }
    }

    /// <summary> Gets the product-owned catalog whose removed managed installs may be pruned. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets an immutable snapshot of the complete current canonical package set. </summary>
    public IReadOnlyList<CanonicalSkillPackage> CurrentCatalogPackages { get; }

    /// <summary> Gets the host target request. </summary>
    public SkillInstallRequest TargetRequest { get; }

    /// <summary> Gets whether to return a plan without deleting from the file system. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether prune force semantics are enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets an immutable category-filter snapshot, or <see langword="null" /> when every category is selected. </summary>
    public IReadOnlyList<SkillCategory>? SelectedCategories { get; }

    /// <summary> Gets an immutable skill-name-filter snapshot, or <see langword="null" /> when every SKILL name is selected. </summary>
    public IReadOnlyList<SkillName>? SelectedSkillNames { get; }
}
