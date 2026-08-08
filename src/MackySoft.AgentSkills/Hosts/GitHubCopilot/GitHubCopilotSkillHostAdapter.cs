using MackySoft.AgentSkills.Serialization.Yaml;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Hosts.GitHubCopilot;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
internal sealed class GitHubCopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        RootRelativePath.Parse(".github/skills"),
        new SkillUserTargetRootPolicy(null, null, RootRelativePath.Parse(".copilot/skills")),
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
