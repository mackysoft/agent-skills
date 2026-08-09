using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Represents canonical Agent Distribution ownership state for one installed agent. </summary>
public sealed class AgentInstallationState
{
    /// <summary> Gets the only supported installation-state schema version. </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary> Initializes canonical ownership state. </summary>
    public AgentInstallationState (
        int schemaVersion,
        AgentDistributionBundleVersion bundleVersion,
        SkillCatalogId catalogId,
        HostKind hostId,
        AgentName agentName,
        Sha256Digest agentManifestDigest,
        IReadOnlyList<AgentInstalledArtifact> managedArtifacts)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, $"Agent installation-state schema version must be {CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(bundleVersion);
        ArgumentNullException.ThrowIfNull(catalogId);
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        ArgumentNullException.ThrowIfNull(agentName);
        ArgumentNullException.ThrowIfNull(agentManifestDigest);
        ArgumentNullException.ThrowIfNull(managedArtifacts);
        var artifacts = managedArtifacts.ToArray();
        if (artifacts.Length == 0
            || artifacts.Any(static artifact => artifact is null)
            || artifacts.GroupBy(static artifact => artifact.Path, PackageRelativePath.PortableFileSystemComparer).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Managed agent artifacts must be complete and unique.", nameof(managedArtifacts));
        }

        SchemaVersion = schemaVersion;
        BundleVersion = bundleVersion;
        CatalogId = catalogId;
        HostId = hostId;
        AgentName = agentName;
        AgentManifestDigest = agentManifestDigest;
        ManagedArtifacts = Array.AsReadOnly(artifacts.OrderBy(static artifact => artifact.Path.Value, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets the schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the installed bundle version. </summary>
    public AgentDistributionBundleVersion BundleVersion { get; }

    /// <summary> Gets the owning catalog identity. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the host that owns the managed artifacts. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets the agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the installed agent manifest digest. </summary>
    public Sha256Digest AgentManifestDigest { get; }

    /// <summary> Gets the managed artifact paths and installed digests. </summary>
    public IReadOnlyList<AgentInstalledArtifact> ManagedArtifacts { get; }
}
