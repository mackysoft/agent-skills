using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Agents.Installation.Targeting;

public sealed class AgentInstallTargetResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ProjectDefault_SeparatesCodexArtifactsFromInstallationState ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "project-target");
        var resolver = CreateResolver(homeDirectory: scope.FullPath);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, scope.FullPath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot.Value).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot.Value).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agent-distribution", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserDefault_UsesCodexHomeWhenAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agents", "user-target");
        var codexHome = scope.CreateDirectory("codex-home");
        var resolver = CreateResolver(scope.FullPath, name => name == "CODEX_HOME" ? codexHome : null);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot.Value).EqualsNormalized(Path.Combine(codexHome, "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot.Value).EqualsNormalized(Path.Combine(codexHome, "agent-distribution", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserDefaultThroughSymbolicLink_ReturnsPathUnsafeFailure ()
    {
        using var home = TestDirectories.CreateTempScope("agent-distribution-agents", "user-link-home");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-agents", "user-link-outside");
        Directory.CreateSymbolicLink(Path.Combine(home.FullPath, ".claude"), outside.FullPath);

        var result = CreateResolver(home.FullPath).ResolveTarget(
            SkillTestData.CreateAgentTargetRequest(HostKind.ClaudeCode, AgentInstallScopeKind.User, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ExplicitProjectTargetOutsideRepository_ReturnsPathUnsafeFailure ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-distribution-agents", "project-root");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-agents", "outside-root");
        var resolver = CreateResolver(repository.FullPath);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, repository.FullPath, outside.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENT_DISTRIBUTION_PATH_UNSAFE", result.Failure!.Code.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ExplicitProjectTargetThroughSymbolicLink_ReturnsPathUnsafeFailure ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-distribution-agents", "project-link-root");
        using var outside = TestDirectories.CreateTempScope("agent-distribution-agents", "project-link-outside");
        var link = Path.Combine(repository.FullPath, "linked");
        Directory.CreateSymbolicLink(link, outside.FullPath);

        var resolver = CreateResolver(repository.FullPath);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(
            HostKind.Codex,
            AgentInstallScopeKind.Project,
            repository.FullPath,
            Path.Combine(link, "agents")));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    private static AgentInstallTargetResolver CreateResolver (string homeDirectory, Func<string, string?>? environment = null)
    {
        return new AgentInstallTargetResolver(
            new AgentUserTargetRootResolver(() => homeDirectory, environment ?? (_ => null)));
    }
}
