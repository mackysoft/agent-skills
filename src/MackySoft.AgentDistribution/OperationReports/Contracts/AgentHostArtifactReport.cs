using MackySoft.AgentDistribution.Digests;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents one host-specific artifact in a canonical custom-agent package. </summary>
public sealed class AgentHostArtifactReport
{
    internal AgentHostArtifactReport (
        HostKind hostId,
        string path,
        Sha256Digest digest)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        OperationReportContractGuard.ValidateSafeRelativePath(path, nameof(path));
        HostId = hostId;
        Path = path;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    /// <summary> Gets the host identifier that owns the artifact. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets the canonical package-relative artifact path. </summary>
    public string Path { get; }

    /// <summary> Gets the artifact content digest. </summary>
    public Sha256Digest Digest { get; }
}
