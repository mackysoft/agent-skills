using MackySoft.AgentDistribution.Hosts.GitHubCopilot;

namespace MackySoft.AgentDistribution.Tests.Hosts.GitHubCopilot;

public sealed class GitHubCopilotSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesHostPolicy ()
    {
        var adapter = new GitHubCopilotSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(".github/skills", descriptor.ProjectDefaultTargetPath.Value);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableName);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".copilot/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory.Value);
        Assert.Equal(SkillBundleTargetRootLayout.CatalogDirectory, descriptor.BundleTargetRootLayout);
        Assert.Equal([SkillBundleTargetRootLayout.Flat], descriptor.CompatiblePreviousBundleTargetRootLayouts);
        Assert.Null(descriptor.MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new GitHubCopilotSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            new SkillName("agent-distribution-sample"),
            "Sample Skill",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-distribution-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "user-invocable: true\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
