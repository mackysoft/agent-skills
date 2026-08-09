using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal sealed class AgentPlannedArtifact
{
    public AgentPlannedArtifact (PackageRelativePath relativePath, string content, Sha256Digest digest)
    {
        RelativePath = relativePath;
        Content = content;
        Digest = digest;
    }

    public PackageRelativePath RelativePath { get; }

    public string Content { get; }

    public Sha256Digest Digest { get; }
}
