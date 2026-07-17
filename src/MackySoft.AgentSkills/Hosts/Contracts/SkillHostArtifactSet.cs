namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Represents deterministic host-specific artifacts for one skill. </summary>
internal sealed class SkillHostArtifactSet
{
    /// <summary> Initializes one immutable host artifact set. </summary>
    public SkillHostArtifactSet (
        string frontmatter,
        string? metadataContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontmatter);

        Frontmatter = frontmatter;
        MetadataContent = metadataContent;
    }

    /// <summary> Gets the materialized SKILL.md frontmatter including delimiters and trailing LF. </summary>
    public string Frontmatter { get; }

    /// <summary> Gets the optional host metadata content. The path is declared by the host adapter. </summary>
    public string? MetadataContent { get; }
}
