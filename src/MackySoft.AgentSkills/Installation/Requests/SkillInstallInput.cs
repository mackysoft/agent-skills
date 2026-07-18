using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Installation.Requests;

/// <summary> Represents one immutable SKILL install service input. </summary>
public sealed class SkillInstallInput
{
    /// <summary> Initializes one SKILL install service input. </summary>
    /// <param name="catalogId"> The product-owned catalog being installed. </param>
    /// <param name="packages"> The canonical packages to install. </param>
    /// <param name="targetRequest"> The host target request. </param>
    /// <param name="dryRun"> Whether to return a plan without writing to the file system. </param>
    /// <param name="force">
    /// Whether managed clean-outdated or locally modified targets can be replaced. Force does not allow replacing
    /// unmanaged targets, name collisions, host conflicts, or path-safety failures.
    /// </param>
    /// <param name="printDiff"> Whether per-file diff payloads should be included. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="catalogId" />, <paramref name="packages" />, or <paramref name="targetRequest" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="packages" /> contains <see langword="null" />, a foreign catalog, or duplicate SKILL names. </exception>
    public SkillInstallInput (
        SkillCatalogId catalogId,
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillInstallRequest targetRequest,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
    {
        CatalogId = catalogId ?? throw new ArgumentNullException(nameof(catalogId));
        Packages = SkillPackageInputSnapshot.Create(CatalogId, packages, nameof(packages));
        TargetRequest = targetRequest ?? throw new ArgumentNullException(nameof(targetRequest));
        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets the product-owned catalog being installed. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets an immutable snapshot of the canonical packages to install. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Packages { get; }

    /// <summary> Gets the host target request. </summary>
    public SkillInstallRequest TargetRequest { get; }

    /// <summary> Gets whether to return a plan without writing to the file system. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether install force semantics are enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets whether per-file diff payloads should be included. </summary>
    public bool PrintDiff { get; }
}
