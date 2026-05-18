using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.OpenAi;

namespace MackySoft.AgentSkills.Tests.Hosts.OpenAi;

public sealed class OpenAiSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new OpenAiSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            "agent-skills-sample",
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
