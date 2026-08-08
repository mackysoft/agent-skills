using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Records one managed artifact path and its installed digest. </summary>
public sealed class AgentInstalledArtifact
{
    /// <summary> Initializes one managed artifact record. </summary>
    public AgentInstalledArtifact (string path, Sha256Digest digest)
    {
        if (!PackageRelativePath.TryParse(path, out _))
        {
            throw new ArgumentException("Managed agent artifact path must be safe.", nameof(path));
        }

        Path = path;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary> Gets the agent artifact-root-relative path. </summary>
    public string Path { get; }

    /// <summary> Gets the digest observed when the artifact was installed. </summary>
    public Sha256Digest Digest { get; }
}
