using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Installation.Targeting;

namespace MackySoft.AgentDistribution.Tests.Agents.Installation.Requests;

public sealed class AgentTargetInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Constructors_RejectDifferentAgentAndSkillHosts ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-input", "host-mismatch");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var catalog = AgentOperationTestData.CreateCatalog(skills, []);
        var agentTarget = SkillTestData.CreateAgentTargetRequest(
            HostKind.Codex,
            AgentInstallScopeKind.Project,
            scope.FullPath,
            "agents");
        var skillTarget = SkillTestData.CreateInstallRequest(
            HostKind.ClaudeCode,
            SkillScopeKind.Project,
            scope.FullPath,
            "skills");

        Assert.Throws<ArgumentException>(() => new AgentInstallInput(catalog, agentTarget, skillTarget));
        Assert.Throws<ArgumentException>(() => new AgentUpdateInput(catalog, agentTarget, skillTarget));
        Assert.Throws<ArgumentException>(() => new AgentDoctorInput(catalog, agentTarget, skillTarget));
    }
}
