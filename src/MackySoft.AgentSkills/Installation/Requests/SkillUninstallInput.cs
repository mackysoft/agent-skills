using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one immutable SKILL uninstall service input. </summary>
public sealed class SkillUninstallInput
{
    /// <summary> Initializes one SKILL uninstall service input. </summary>
    /// <param name="catalogId"> The product-owned catalog being uninstalled. </param>
    /// <param name="packages"> The canonical packages to remove. </param>
    /// <param name="targetRequest"> The host target request. </param>
    /// <param name="dryRun"> Whether to return a plan without writing to the file system. </param>
    /// <param name="force">
    /// Whether managed locally modified targets can be deleted. Current and clean-outdated targets can be deleted
    /// without force; force does not allow deleting unmanaged targets, name collisions, host conflicts, or path-safety
    /// failures.
    /// </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="catalogId" />, <paramref name="packages" />, or <paramref name="targetRequest" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="packages" /> contains <see langword="null" />, a foreign catalog, or duplicate SKILL names. </exception>
    public SkillUninstallInput (
        SkillCatalogId catalogId,
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillInstallRequest targetRequest,
        bool dryRun = false,
        bool force = false)
    {
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        Packages = SkillPackageInputSnapshot.Create(CatalogId, packages, nameof(packages));
        TargetRequest = targetRequest ?? throw new ArgumentNullException(nameof(targetRequest));
        DryRun = dryRun;
        Force = force;
    }

    /// <summary> Gets the product-owned catalog being uninstalled. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets an immutable snapshot of the canonical packages to remove. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Packages { get; }

    /// <summary> Gets the host target request. </summary>
    public SkillInstallRequest TargetRequest { get; }

    /// <summary> Gets whether to return a plan without writing to the file system. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether uninstall force semantics are enabled. </summary>
    public bool Force { get; }
}
