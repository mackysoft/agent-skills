using MackySoft.AgentSkills.Hosts.ClaudeCode;

namespace MackySoft.AgentSkills.Tests.Hosts.ClaudeCode;

public sealed class ClaudeCodeSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesHostPolicy ()
    {
        var adapter = new ClaudeCodeSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(".claude/skills", descriptor.ProjectDefaultTargetPath.Value);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableName);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".claude/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory.Value);
        Assert.Equal(SkillBundleTargetRootLayout.Flat, descriptor.BundleTargetRootLayout);
        Assert.Empty(descriptor.CompatiblePreviousBundleTargetRootLayouts);
        Assert.Null(descriptor.MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new ClaudeCodeSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            new SkillName("agent-skills-sample"),
            "Sample Skill",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-skills-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "disable-model-invocation: false\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
