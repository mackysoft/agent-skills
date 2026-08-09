using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Records one managed artifact path and its installed digest. </summary>
public sealed class AgentInstalledArtifact
{
    /// <summary> Initializes one managed artifact record. </summary>
    public AgentInstalledArtifact (PackageRelativePath path, Sha256Digest digest)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary> Gets the agent artifact-root-relative path. </summary>
    public PackageRelativePath Path { get; }

    /// <summary> Gets the digest observed when the artifact was installed. </summary>
    public Sha256Digest Digest { get; }
}
