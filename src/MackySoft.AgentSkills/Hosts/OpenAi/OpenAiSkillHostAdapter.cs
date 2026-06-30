using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Serialization.Yaml;

namespace MackySoft.AgentSkills.Hosts.OpenAi;

/// <summary> Materializes SKILL files for OpenAI / Codex. </summary>
public sealed class OpenAiSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical OpenAI / Codex host key. </summary>
    public const string HostKey = "openai";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        HostKey: HostKey,
        SupportsProjectScope: true,
        SupportsUserScope: true,
        ProjectDefaultTargetPath: ".agents/skills",
        UserDefaultTargetPath: "${CODEX_HOME}/skills or ~/.codex/skills",
        UserTargetRootPolicy: new SkillUserTargetRootPolicy("CODEX_HOME", "skills", ".codex/skills"),
        RequiresMetadataArtifact: true,
        MetadataArtifactPath: "agents/openai.yaml",
        ReloadGuidance: "Restart the Codex session or app to reload installed or updated skills.");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName.Value)
            .Mapping("description", metadata.Description)
            .DocumentMarker()
            .Build();

        var openAiYaml = new DeterministicYamlBuilder()
            .Section("interface")
            .Mapping("display_name", metadata.DisplayName, indentationLevel: 1)
            .Mapping("short_description", metadata.Description, indentationLevel: 1)
            .Mapping("default_prompt", $"Use ${metadata.SkillName.Value} to follow the {metadata.DisplayName} workflow.", indentationLevel: 1)
            .BlankLine()
            .Section("policy")
            .Mapping("allow_implicit_invocation", true, indentationLevel: 1)
            .Build();

        return new SkillHostArtifactSet(
            frontmatter,
            openAiYaml);
    }
}
