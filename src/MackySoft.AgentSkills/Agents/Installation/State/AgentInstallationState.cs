using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Represents canonical Agent Skills ownership state for one installed agent. </summary>
public sealed class AgentInstallationState
{
    /// <summary> Gets the only supported installation-state schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Initializes canonical ownership state. </summary>
    public AgentInstallationState (
        int schemaVersion,
        AgentSkillsBundleVersion bundleVersion,
        SkillCatalogId catalogId,
        AgentHostKind hostId,
        AgentCategory category,
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

        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(agentName);
        ArgumentNullException.ThrowIfNull(agentManifestDigest);
        ArgumentNullException.ThrowIfNull(managedArtifacts);
        var artifacts = managedArtifacts.ToArray();
        if (artifacts.Length == 0
            || artifacts.Any(static artifact => artifact is null)
            || artifacts.GroupBy(static artifact => artifact.Path, StringComparer.Ordinal).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Managed agent artifacts must be complete and unique.", nameof(managedArtifacts));
        }

        SchemaVersion = schemaVersion;
        BundleVersion = bundleVersion;
        CatalogId = catalogId;
        HostId = hostId;
        Category = category;
        AgentName = agentName;
        AgentManifestDigest = agentManifestDigest;
        ManagedArtifacts = Array.AsReadOnly(artifacts.OrderBy(static artifact => artifact.Path, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets the schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the installed bundle version. </summary>
    public AgentSkillsBundleVersion BundleVersion { get; }

    /// <summary> Gets the owning catalog identity. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the host that owns the managed artifacts. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets the agent category. </summary>
    public AgentCategory Category { get; }

    /// <summary> Gets the agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the installed agent manifest digest. </summary>
    public Sha256Digest AgentManifestDigest { get; }

    /// <summary> Gets the managed artifact paths and installed digests. </summary>
    public IReadOnlyList<AgentInstalledArtifact> ManagedArtifacts { get; }
}
