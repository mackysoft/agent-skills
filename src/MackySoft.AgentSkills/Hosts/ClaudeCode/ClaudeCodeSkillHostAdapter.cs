using MackySoft.AgentSkills.Serialization.Yaml;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosts.ClaudeCode;

/// <summary> Materializes SKILL files for Claude Code. </summary>
internal sealed class ClaudeCodeSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        RootRelativePath.Parse(".claude/skills"),
        new SkillUserTargetRootPolicy(null, null, RootRelativePath.Parse(".claude/skills")),
        SkillBundleTargetRootLayout.Flat,
        [],
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
