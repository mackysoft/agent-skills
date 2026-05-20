using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Serialization.Yaml;

namespace MackySoft.AgentSkills.Hosts.Copilot;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
public sealed class CopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical GitHub Copilot CLI host key. </summary>
    public const string HostKey = "copilot";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        HostKey: HostKey,
        SupportsProjectScope: true,
        SupportsUserScope: true,
        ProjectDefaultTargetPath: ".github/skills",
        UserDefaultTargetPath: "~/.copilot/skills",
        UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".copilot/skills"),
        RequiresMetadataArtifact: false,
        MetadataArtifactPath: null,
        ReloadGuidance: "Run /skills reload in GitHub Copilot CLI to load newly installed or updated skills.");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName)
            .Mapping("description", metadata.Description)
            .Mapping("user-invocable", true)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, null);
    }
}
