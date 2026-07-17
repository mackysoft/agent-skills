using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Hosts.Copilot;

public sealed class CopilotSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesHostPolicy ()
    {
        var adapter = new CopilotSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(SkillHostKind.Copilot, descriptor.Host);
        Assert.Equal(".github/skills", descriptor.ProjectDefaultTargetPath);
        Assert.Equal("~/.copilot/skills", descriptor.UserDefaultTargetPath);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableName);
        Assert.Null(descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".copilot/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory);
        Assert.Null(descriptor.MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new CopilotSkillHostAdapter();
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
            + "user-invocable: true\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
