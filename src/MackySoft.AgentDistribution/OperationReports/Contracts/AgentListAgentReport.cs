using MackySoft.AgentDistribution.Digests;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one canonical custom-agent package. </summary>
public sealed class AgentListAgentReport
{
    internal AgentListAgentReport (
        int schemaVersion,
        int bundleVersion,
        string agentName,
        string displayName,
        string description,
        string catalogId,
        IReadOnlyList<string> skillDependencies,
        Sha256Digest contentDigest,
        Sha256Digest manifestDigest,
        IReadOnlyList<AgentHostArtifactReport> hostArtifacts)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Agent manifest schema version must be positive.");
        }

        if (bundleVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bundleVersion), bundleVersion, "Agent bundle version must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);

        SchemaVersion = schemaVersion;
        BundleVersion = bundleVersion;
        AgentName = agentName;
        DisplayName = displayName;
        Description = description;
        CatalogId = catalogId;
        SkillDependencies = OperationReportContractGuard.SnapshotRequiredStrings(skillDependencies, nameof(skillDependencies));
        ContentDigest = contentDigest ?? throw new ArgumentNullException(nameof(contentDigest));
        ManifestDigest = manifestDigest ?? throw new ArgumentNullException(nameof(manifestDigest));
        HostArtifacts = OperationReportContractGuard.SnapshotRequiredItems(hostArtifacts, nameof(hostArtifacts));
    }

    /// <summary> Gets the agent manifest schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the mixed bundle version. </summary>
    public int BundleVersion { get; }

    /// <summary> Gets the canonical agent name. </summary>
    public string AgentName { get; }

    /// <summary> Gets the display name. </summary>
    public string DisplayName { get; }

    /// <summary> Gets the host-independent description. </summary>
    public string Description { get; }

    /// <summary> Gets the catalog identifier. </summary>
    public string CatalogId { get; }

    /// <summary> Gets direct SKILL dependency names in ordinal order. </summary>
    public IReadOnlyList<string> SkillDependencies { get; }

    /// <summary> Gets the host-independent instruction digest. </summary>
    public Sha256Digest ContentDigest { get; }

    /// <summary> Gets the canonical manifest digest. </summary>
    public Sha256Digest ManifestDigest { get; }

    /// <summary> Gets generated host artifacts in canonical path order. </summary>
    public IReadOnlyList<AgentHostArtifactReport> HostArtifacts { get; }
}
