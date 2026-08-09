using MackySoft.AgentDistribution.Serialization.Yaml;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Hosts.Codex;

/// <summary>Materializes Skill files for Codex.</summary>
internal sealed class CodexSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        RootRelativePath.Parse(".agents/skills"),
        new SkillUserTargetRootPolicy(
            "CODEX_HOME",
            RootRelativePath.Parse("skills"),
            RootRelativePath.Parse(".codex/skills")),
        SkillBundleTargetRootLayout.CatalogDirectory,
        [SkillBundleTargetRootLayout.Flat],
        PackageRelativePath.Parse("agents/openai.yaml"),
        "Restart the Codex session or app to reload installed or updated skills.");

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

        var codexYaml = new DeterministicYamlBuilder()
            .Section("interface")
            .Mapping("display_name", metadata.DisplayName, indentationLevel: 1)
            .Mapping("short_description", metadata.Description, indentationLevel: 1)
            .Mapping("default_prompt", $"Use ${metadata.SkillName.Value} to follow the {metadata.DisplayName} workflow.", indentationLevel: 1)
            .BlankLine()
            .Section("policy")
            .Mapping("allow_implicit_invocation", true, indentationLevel: 1)
            .Build();

        return new SkillHostArtifactSet(frontmatter, codexYaml);
    }
}
