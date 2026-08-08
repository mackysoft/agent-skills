using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Hosts;

/// <summary> Represents one host-adapter-relative generated agent artifact. </summary>
internal sealed class AgentHostArtifactFile
{
    /// <summary> Initializes one host artifact file. </summary>
    public AgentHostArtifactFile (string relativePath, string content)
    {
        if (!PackageRelativePath.TryParse(relativePath, out _))
        {
            throw new ArgumentException("Host artifact path must be safe.", nameof(relativePath));
        }

        ArgumentNullException.ThrowIfNull(content);
        RelativePath = relativePath;
        Content = SkillTextNormalizer.NormalizeToLf(content);
    }

    /// <summary> Gets the host-relative path. </summary>
    public string RelativePath { get; }

    /// <summary> Gets normalized text content. </summary>
    public string Content { get; }
}
