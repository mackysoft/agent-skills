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
}
