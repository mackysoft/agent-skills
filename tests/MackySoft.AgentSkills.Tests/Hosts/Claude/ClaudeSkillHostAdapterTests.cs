using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;

namespace MackySoft.AgentSkills.Tests.Hosts.Claude;

public sealed class ClaudeSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new ClaudeSkillHostAdapter();
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
            + "disable-model-invocation: false\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
