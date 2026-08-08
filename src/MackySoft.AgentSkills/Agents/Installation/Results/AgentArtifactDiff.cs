namespace MackySoft.AgentSkills.Agents.Installation.Results;

using MackySoft.AgentSkills.Shared;

/// <summary> Represents one requested custom-agent artifact content diff. </summary>
public sealed class AgentArtifactDiff
{
    /// <summary> Initializes one immutable artifact diff. </summary>
    internal AgentArtifactDiff (PackageRelativePath relativePath, string? beforeContent, string afterContent)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(afterContent);
        RelativePath = relativePath;
        BeforeContent = beforeContent;
        AfterContent = afterContent;
    }

    /// <summary> Gets the artifact-root-relative path. </summary>
    public PackageRelativePath RelativePath { get; }

    /// <summary> Gets the existing content, or <see langword="null" /> when the artifact is created. </summary>
    public string? BeforeContent { get; }

    /// <summary> Gets the desired generated content. </summary>
    public string AfterContent { get; }
}
