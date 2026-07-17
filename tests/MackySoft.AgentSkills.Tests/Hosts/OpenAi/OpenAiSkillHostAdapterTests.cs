using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Hosts.OpenAi;

public sealed class OpenAiSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_ExposesHostPolicy ()
    {
        var adapter = new OpenAiSkillHostAdapter();
        var descriptor = adapter.Descriptor;

        Assert.Equal(SkillHostKind.OpenAi, descriptor.Host);
        Assert.Equal(".agents/skills", descriptor.ProjectDefaultTargetPath);
        Assert.Equal("${CODEX_HOME}/skills or ~/.codex/skills", descriptor.UserDefaultTargetPath);
        Assert.Equal("CODEX_HOME", descriptor.UserTargetRootPolicy.EnvironmentVariableName);
        Assert.Equal("skills", descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory);
        Assert.Equal(".codex/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory);
        Assert.Equal("agents/openai.yaml", descriptor.MetadataArtifactPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ReloadGuidance));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new OpenAiSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            new SkillName("agent-skills-sample"),
            "Sample \"Skill\"\rName",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-skills-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Equal(
            "interface:\n"
            + "  display_name: \"Sample \\\"Skill\\\"\\rName\"\n"
            + "  short_description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "  default_prompt: \"Use $agent-skills-sample to follow the Sample \\\"Skill\\\"\\rName workflow.\"\n"
            + "\n"
            + "policy:\n"
            + "  allow_implicit_invocation: true\n",
            artifacts.MetadataContent);
    }
}
