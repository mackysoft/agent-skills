using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Serialization.Yaml;

namespace MackySoft.AgentSkills.Hosts.Copilot;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
internal sealed class CopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        SkillHostKind.Copilot,
        ".github/skills",
        "~/.copilot/skills",
        new SkillUserTargetRootPolicy(null, null, ".copilot/skills"),
        SkillBundleTargetRootLayout.CatalogDirectory,
        [SkillBundleTargetRootLayout.Flat],
        null,
        "Run /skills reload in GitHub Copilot CLI to load newly installed or updated skills.");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName.Value)
            .Mapping("description", metadata.Description)
            .Mapping("user-invocable", true)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, null);
    }
}
