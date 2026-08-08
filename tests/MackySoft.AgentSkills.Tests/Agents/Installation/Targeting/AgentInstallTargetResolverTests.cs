using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Agents.Installation.Targeting;

public sealed class AgentInstallTargetResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ProjectDefault_SeparatesCodexArtifactsFromInstallationState ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "project-target");
        var resolver = CreateResolver(homeDirectory: scope.FullPath);

        var result = resolver.ResolveTarget(new AgentTargetRequest(AgentHostKind.OpenAi, AgentInstallScopeKind.Project, scope.FullPath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agent-skills", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserDefault_UsesCodexHomeWhenAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "user-target");
        var codexHome = scope.CreateDirectory("codex-home");
        var resolver = CreateResolver(scope.FullPath, name => name == "CODEX_HOME" ? codexHome : null);

        var result = resolver.ResolveTarget(new AgentTargetRequest(AgentHostKind.OpenAi, AgentInstallScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot).EqualsNormalized(Path.Combine(codexHome, "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot).EqualsNormalized(Path.Combine(codexHome, "agent-skills", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ExplicitProjectTargetOutsideRepository_ReturnsPathUnsafeFailure ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-skills-agents", "project-root");
        using var outside = TestDirectories.CreateTempScope("agent-skills-agents", "outside-root");
        var resolver = CreateResolver(repository.FullPath);

        var result = resolver.ResolveTarget(new AgentTargetRequest(AgentHostKind.OpenAi, AgentInstallScopeKind.Project, repository.FullPath, outside.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal("SKILL_PATH_UNSAFE", result.Failure!.Code.Value);
    }

    private static AgentInstallTargetResolver CreateResolver (string homeDirectory, Func<string, string?>? environment = null)
    {
        return new AgentInstallTargetResolver(
            new AgentHostAdapterSet(),
            new AgentUserTargetRootResolver(() => homeDirectory, environment ?? (_ => null)));
    }
}
