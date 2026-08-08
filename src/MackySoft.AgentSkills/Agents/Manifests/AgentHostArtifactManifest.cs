using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Manifests;

/// <summary> Records one generated agent host artifact file. </summary>
public sealed class AgentHostArtifactManifest
{
    /// <summary> Initializes an artifact manifest. </summary>
    internal AgentHostArtifactManifest (AgentHostKind hostId, string path, Sha256Digest digest)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        if (!PackageRelativePath.TryParse(path, out _))
        {
            throw new ArgumentException("Agent host artifact path must be safe.", nameof(path));
        }

        HostId = hostId;
        Path = path;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary> Gets the host identifier. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets the package-relative artifact path. </summary>
    public string Path { get; }

    /// <summary> Gets the artifact content digest. </summary>
    public Sha256Digest Digest { get; }
}
