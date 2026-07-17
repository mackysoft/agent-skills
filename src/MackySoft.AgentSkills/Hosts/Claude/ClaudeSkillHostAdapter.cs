using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Serialization.Yaml;

namespace MackySoft.AgentSkills.Hosts.Claude;

/// <summary> Materializes SKILL files for Claude Code. </summary>
internal sealed class ClaudeSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        SkillHostKind.Claude,
        ".claude/skills",
        "~/.claude/skills",
        new SkillUserTargetRootPolicy(null, null, ".claude/skills"),
        null,
        "Claude Code watches existing skill directories. Restart Claude Code if the top-level skills directory was created after the session started.");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName.Value)
            .Mapping("description", metadata.Description)
            .Mapping("disable-model-invocation", false)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, null);
    }
}
