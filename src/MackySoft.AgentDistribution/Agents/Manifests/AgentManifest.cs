using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Manifests;

/// <summary> Represents one canonical generated custom-agent manifest. </summary>
public sealed class AgentManifest
{
    /// <summary> Gets the generated manifest schema version. </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary> Initializes a canonical manifest. </summary>
    internal AgentManifest (int schemaVersion, AgentDistributionBundleVersion bundleVersion, AgentDistributionCatalogId catalogId, AgentName agentName, string displayName, string description, IReadOnlyList<SkillName> skillDependencies, Sha256Digest contentDigest, Sha256Digest manifestDigest, IReadOnlyList<AgentHostArtifactManifest> hostArtifacts)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Agent manifest schema version must be {CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(bundleVersion);
        ArgumentNullException.ThrowIfNull(catalogId);
        ArgumentNullException.ThrowIfNull(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(skillDependencies);
        ArgumentNullException.ThrowIfNull(hostArtifacts);
        var dependencies = skillDependencies.ToArray();
        var artifacts = hostArtifacts.ToArray();
        if (dependencies.Any(static dependency => dependency is null)
            || dependencies.Distinct().Count() != dependencies.Length
            || artifacts.Length == 0
            || artifacts.Any(static artifact => artifact is null)
            || artifacts.GroupBy(static artifact => artifact.Path, PackageRelativePath.PortableFileSystemComparer).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Agent manifest dependencies and artifacts must be unique and complete.");
        }

        SchemaVersion = schemaVersion;
        BundleVersion = bundleVersion;
        CatalogId = catalogId;
        AgentName = agentName;
        DisplayName = displayName;
        Description = description;
        SkillDependencies = Array.AsReadOnly(dependencies.OrderBy(static item => item.Value, StringComparer.Ordinal).ToArray());
        ContentDigest = contentDigest ?? throw new ArgumentNullException(nameof(contentDigest));
        ManifestDigest = manifestDigest ?? throw new ArgumentNullException(nameof(manifestDigest));
        HostArtifacts = Array.AsReadOnly(artifacts.OrderBy(static item => item.Path.Value, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets schema version. </summary>
    public int SchemaVersion { get; }
    /// <summary> Gets bundle version. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }
    /// <summary> Gets catalog identity. </summary>
    public AgentDistributionCatalogId CatalogId { get; }
    /// <summary> Gets agent identity. </summary>
    public AgentName AgentName { get; }
    /// <summary> Gets display name. </summary>
    public string DisplayName { get; }
    /// <summary> Gets description. </summary>
    public string Description { get; }
    /// <summary> Gets direct skill dependencies. </summary>
    public IReadOnlyList<SkillName> SkillDependencies { get; }
    /// <summary> Gets instructions content digest. </summary>
    public Sha256Digest ContentDigest { get; }
    /// <summary> Gets manifest digest. </summary>
    public Sha256Digest ManifestDigest { get; }
    /// <summary> Gets generated host artifact records. </summary>
    public IReadOnlyList<AgentHostArtifactManifest> HostArtifacts { get; }
}
