using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsAgentsCommandMetadataTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateReportCommandNames_UsesIndependentAgentsRoot ()
    {
        Assert.Equal(
            [
                "agents.list",
                "agents.export",
                "agents.install",
                "agents.update",
                "agents.uninstall",
                "agents.prune",
                "agents.doctor",
            ],
            AgentSkillsAgentsCommandMetadata.CreateReportCommandNames());
        Assert.Equal(
            [
                "agent-skills.agents.list",
                "agent-skills.agents.export",
                "agent-skills.agents.install",
                "agent-skills.agents.update",
                "agent-skills.agents.uninstall",
                "agent-skills.agents.prune",
                "agent-skills.agents.doctor",
            ],
            AgentSkillsAgentsCommandMetadata.CreateReportCommandNames("agent-skills agents"));
    }

    [Theory]
    [InlineData("Agent Skills")]
    [InlineData("agents-")]
    [InlineData("agents  nested")]
    [Trait("Size", "Small")]
    public void CreateReportCommandNames_WhenAgentsRootIsInvalid_ThrowsArgumentException (string commandRoot)
    {
        Assert.Throws<ArgumentException>(() => AgentSkillsAgentsCommandMetadata.CreateReportCommandNames(commandRoot));
    }
}
