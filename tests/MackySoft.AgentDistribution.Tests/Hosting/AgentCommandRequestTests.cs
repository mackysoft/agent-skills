using MackySoft.AgentDistribution.Hosting.Commands;

namespace MackySoft.AgentDistribution.Tests.Hosting;

public sealed class AgentCommandRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Requests_CaptureCategoryAndAgentSnapshots ()
    {
        var categories = new List<string> { "planning" };
        var agents = new List<string> { "architect" };
        var request = new AgentInstallCommandRequest(category: categories, agent: agents);

        categories.Clear();
        agents.Clear();

        Assert.Equal(["planning"], request.Category);
        Assert.Equal(["architect"], request.Agent);
    }
}
