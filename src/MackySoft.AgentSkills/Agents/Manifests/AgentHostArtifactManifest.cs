using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Manifests;

/// <summary> Records one generated agent host artifact file. </summary>
public sealed class AgentHostArtifactManifest
{
    /// <summary> Initializes an artifact manifest. </summary>
    internal AgentHostArtifactManifest (HostKind hostId, PackageRelativePath path, Sha256Digest digest)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        HostId = hostId;
        Path = path ?? throw new ArgumentNullException(nameof(path));
        HostTargetRelativePath = AgentHostArtifactPackagePath.GetHostRelativePath(hostId, Path);
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary> Gets the host identifier. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets the package-relative artifact path. </summary>
    public PackageRelativePath Path { get; }

    /// <summary> Gets the verified artifact path relative to the selected host target root. </summary>
    internal PackageRelativePath HostTargetRelativePath { get; }

    /// <summary> Gets the artifact content digest. </summary>
    public Sha256Digest Digest { get; }
}
