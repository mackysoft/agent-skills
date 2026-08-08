using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

internal sealed class AgentPlannedArtifact
{
    public AgentPlannedArtifact (string relativePath, string content, Sha256Digest digest)
    {
        RelativePath = relativePath;
        Content = content;
        Digest = digest;
    }

    public string RelativePath { get; }

    public string Content { get; }

    public Sha256Digest Digest { get; }
}
