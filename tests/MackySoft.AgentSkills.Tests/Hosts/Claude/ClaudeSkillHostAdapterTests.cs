using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Hosts.Claude;

public sealed class ClaudeSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesCapabilityMetadata ()
    {
        var adapter = new ClaudeSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(ClaudeSkillHostAdapter.HostKey, descriptor.HostKey);
        Assert.True(descriptor.SupportsProjectScope);
        Assert.True(descriptor.SupportsUserScope);
        Assert.Equal(".claude/skills", descriptor.ProjectDefaultTargetPath);
        Assert.Equal("~/.claude/skills", descriptor.UserDefaultTargetPath);
        Assert.NotNull(descriptor.UserTargetRootPolicy);
        Assert.Null(descriptor.UserTargetRootPolicy!.EnvironmentVariableName);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".claude/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory);
        Assert.False(descriptor.RequiresMetadataArtifact);
        Assert.Null(descriptor.MetadataArtifactPath);
        Assert.Null(((ISkillHostAdapter)adapter).MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new ClaudeSkillHostAdapter();
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
