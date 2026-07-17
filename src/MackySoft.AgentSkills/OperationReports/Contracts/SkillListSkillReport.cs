using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one canonical skill package. </summary>
public sealed class SkillListSkillReport
{
    internal SkillListSkillReport (
        int schemaVersion,
        int skillBundleVersion,
        string skillName,
        string displayName,
        string description,
        IReadOnlyList<string> dependencies,
        string category,
        string catalogId,
        Sha256Digest contentDigest,
        Sha256Digest manifestDigest,
        IReadOnlyList<SkillHostArtifactReport> hostArtifacts)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Manifest schema version must be positive.");
        }

        if (skillBundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skillBundleVersion), skillBundleVersion, "SKILL bundle version must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);

        SchemaVersion = schemaVersion;
        SkillBundleVersion = skillBundleVersion;
        SkillName = skillName;
        DisplayName = displayName;
        Description = description;
        Dependencies = OperationReportContractGuard.SnapshotRequiredStrings(dependencies, nameof(dependencies));
        Category = category;
        CatalogId = catalogId;
        ContentDigest = contentDigest ?? throw new ArgumentNullException(nameof(contentDigest));
        ManifestDigest = manifestDigest ?? throw new ArgumentNullException(nameof(manifestDigest));
        HostArtifacts = OperationReportContractGuard.SnapshotRequiredItems(hostArtifacts, nameof(hostArtifacts));
    }

    /// <summary> Gets the manifest schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the SKILL bundle version. </summary>
    public int SkillBundleVersion { get; }

    /// <summary> Gets the canonical SKILL name. </summary>
    public string SkillName { get; }

    /// <summary> Gets the display name. </summary>
    public string DisplayName { get; }

    /// <summary> Gets the host-independent description. </summary>
    public string Description { get; }

    /// <summary> Gets dependent SKILL name literals. </summary>
    public IReadOnlyList<string> Dependencies { get; }

    /// <summary> Gets the category literal. </summary>
    public string Category { get; }

    /// <summary> Gets the catalog ID literal. </summary>
    public string CatalogId { get; }

    /// <summary> Gets the content digest. </summary>
    public Sha256Digest ContentDigest { get; }

    /// <summary> Gets the manifest digest. </summary>
    public Sha256Digest ManifestDigest { get; }

    /// <summary> Gets host-specific artifact reports in canonical host order. </summary>
    public IReadOnlyList<SkillHostArtifactReport> HostArtifacts { get; }
}
