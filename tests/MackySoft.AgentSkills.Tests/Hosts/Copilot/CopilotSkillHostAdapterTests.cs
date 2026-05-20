using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;

namespace MackySoft.AgentSkills.Tests.Hosts.Copilot;

public sealed class CopilotSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesCapabilityMetadata ()
    {
        var adapter = new CopilotSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(CopilotSkillHostAdapter.HostKey, descriptor.HostKey);
        Assert.True(descriptor.SupportsProjectScope);
        Assert.True(descriptor.SupportsUserScope);
        Assert.Equal(".github/skills", descriptor.ProjectDefaultTargetPath);
        Assert.Equal("~/.copilot/skills", descriptor.UserDefaultTargetPath);
        Assert.NotNull(descriptor.UserTargetRootPolicy);
        Assert.Null(descriptor.UserTargetRootPolicy!.EnvironmentVariableName);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".copilot/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory);
        Assert.False(descriptor.RequiresMetadataArtifact);
        Assert.Null(descriptor.MetadataArtifactPath);
        Assert.Null(((ISkillHostAdapter)adapter).MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new CopilotSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            "agent-skills-sample",
            "Sample Skill",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-skills-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "user-invocable: true\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
