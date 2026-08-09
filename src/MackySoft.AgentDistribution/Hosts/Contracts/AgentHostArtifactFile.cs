using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosts.Contracts;

/// <summary> Represents one host-adapter-relative generated agent artifact. </summary>
internal sealed class AgentHostArtifactFile
{
    /// <summary> Initializes one host artifact file. </summary>
    public AgentHostArtifactFile (PackageRelativePath relativePath, string content)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        RelativePath = relativePath;
        Content = SkillTextNormalizer.NormalizeToLf(content);
    }

    /// <summary> Gets the host-relative path. </summary>
    public PackageRelativePath RelativePath { get; }

    /// <summary> Gets normalized text content. </summary>
    public string Content { get; }
}
