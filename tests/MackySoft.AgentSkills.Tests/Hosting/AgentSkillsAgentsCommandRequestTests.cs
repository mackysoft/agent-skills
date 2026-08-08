using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsAgentsCommandRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Requests_CaptureCategoryAndAgentSnapshots ()
    {
        var categories = new List<string> { "planning" };
        var agents = new List<string> { "architect" };
        var request = new AgentSkillsAgentInstallCommandRequest(category: categories, agent: agents);

        categories.Clear();
        agents.Clear();

        Assert.Equal(["planning"], request.Category);
        Assert.Equal(["architect"], request.Agent);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Requests_ExposeOnlyGetOnlyProperties ()
    {
        Type[] types =
        [
            typeof(AgentSkillsAgentListCommandRequest),
            typeof(AgentSkillsAgentExportCommandRequest),
            typeof(AgentSkillsAgentInstallCommandRequest),
            typeof(AgentSkillsAgentUpdateCommandRequest),
            typeof(AgentSkillsAgentUninstallCommandRequest),
            typeof(AgentSkillsAgentPruneCommandRequest),
            typeof(AgentSkillsAgentDoctorCommandRequest),
        ];

        Assert.All(types, static type =>
            Assert.All(type.GetProperties(), static property => Assert.Null(property.SetMethod)));
    }
}
