using MackySoft.AgentDistribution.Hosting.Commands;

namespace MackySoft.AgentDistribution.Tests.Hosting;

public sealed class AgentCommandRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Requests_CaptureAgentSnapshot ()
    {
        var agents = new List<string> { "architect" };
        var request = new AgentInstallCommandRequest(agent: agents);

        agents.Clear();

        Assert.Equal(["architect"], request.Agent);
    }
}
