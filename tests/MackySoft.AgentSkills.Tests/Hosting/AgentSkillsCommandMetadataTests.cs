using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandMetadataTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateSubcommands_ReturnsStableSkillsCommandLeaves ()
    {
        Assert.Equal(
            [
                "list",
                "export",
                "install",
                "update",
                "uninstall",
                "prune",
                "doctor",
            ],
            AgentSkillsCommandMetadata.CreateSubcommands());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateReportCommandNames_ReturnsStableReportCommandNames ()
    {
        Assert.Equal(
            [
                "skills.list",
                "skills.export",
                "skills.install",
                "skills.update",
                "skills.uninstall",
                "skills.prune",
                "skills.doctor",
            ],
            AgentSkillsCommandMetadata.CreateReportCommandNames());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateReportCommandNames_WhenRootIsSpecified_ReturnsRootedReportCommandNames ()
    {
        Assert.Equal(
            [
                "agent-skills.list",
                "agent-skills.export",
                "agent-skills.install",
                "agent-skills.update",
                "agent-skills.uninstall",
                "agent-skills.prune",
                "agent-skills.doctor",
            ],
            AgentSkillsCommandMetadata.CreateReportCommandNames("agent-skills"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateReportCommandNames_WhenRootContainsNestedTokens_ReturnsDottedReportCommandNames ()
    {
        Assert.Equal(
            [
                "tools.skills.list",
                "tools.skills.export",
                "tools.skills.install",
                "tools.skills.update",
                "tools.skills.uninstall",
                "tools.skills.prune",
                "tools.skills.doctor",
            ],
            AgentSkillsCommandMetadata.CreateReportCommandNames("tools skills"));
    }
}
