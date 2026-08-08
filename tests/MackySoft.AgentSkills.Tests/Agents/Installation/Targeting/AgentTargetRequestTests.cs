using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.FileSystem;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Agents.Installation.Targeting;

public sealed class AgentTargetRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_ProjectRelativeArtifactTarget_PreservesResolvedPathContracts ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-skills-agents", "request-project-root");

        var request = new AgentTargetRequest(
            AgentHostKind.OpenAi,
            AgentInstallScopeKind.Project,
            repository.FullPath,
            "custom/agents");

        Assert.True(request.RepositoryRoot!.IsSameAs(AbsolutePath.Parse(repository.FullPath)));
        Assert.True(request.ArtifactTargetRoot!.IsSameAs(AbsolutePath.Parse(Path.Combine(repository.FullPath, "custom", "agents"))));
    }
}
