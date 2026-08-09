using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
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

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, scope.FullPath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot.Value).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot.Value).EqualsNormalized(Path.Combine(scope.FullPath, ".codex", "agent-skills", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserDefault_UsesCodexHomeWhenAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agents", "user-target");
        var codexHome = scope.CreateDirectory("codex-home");
        var resolver = CreateResolver(scope.FullPath, name => name == "CODEX_HOME" ? codexHome : null);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        FileSystemAssert.ForPath(result.Value!.ArtifactRoot.Value).EqualsNormalized(Path.Combine(codexHome, "agents"));
        FileSystemAssert.ForPath(result.Value.StateRoot.Value).EqualsNormalized(Path.Combine(codexHome, "agent-skills", "agents"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserDefaultThroughSymbolicLink_ReturnsPathUnsafeFailure ()
    {
        using var home = TestDirectories.CreateTempScope("agent-skills-agents", "user-link-home");
        using var outside = TestDirectories.CreateTempScope("agent-skills-agents", "user-link-outside");
        if (!TryCreateDirectorySymbolicLink(Path.Combine(home.FullPath, ".claude"), outside.FullPath))
        {
            return;
        }

        var result = CreateResolver(home.FullPath).ResolveTarget(
            SkillTestData.CreateAgentTargetRequest(HostKind.ClaudeCode, AgentInstallScopeKind.User, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ExplicitProjectTargetOutsideRepository_ReturnsPathUnsafeFailure ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-skills-agents", "project-root");
        using var outside = TestDirectories.CreateTempScope("agent-skills-agents", "outside-root");
        var resolver = CreateResolver(repository.FullPath);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, repository.FullPath, outside.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal("SKILL_PATH_UNSAFE", result.Failure!.Code.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ExplicitProjectTargetThroughSymbolicLink_ReturnsPathUnsafeFailure ()
    {
        using var repository = TestDirectories.CreateTempScope("agent-skills-agents", "project-link-root");
        using var outside = TestDirectories.CreateTempScope("agent-skills-agents", "project-link-outside");
        var link = Path.Combine(repository.FullPath, "linked");
        if (!TryCreateDirectorySymbolicLink(link, outside.FullPath))
        {
            return;
        }

        var resolver = CreateResolver(repository.FullPath);

        var result = resolver.ResolveTarget(SkillTestData.CreateAgentTargetRequest(
            HostKind.Codex,
            AgentInstallScopeKind.Project,
            repository.FullPath,
            Path.Combine(link, "agents")));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    private static bool TryCreateDirectorySymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static AgentInstallTargetResolver CreateResolver (string homeDirectory, Func<string, string?>? environment = null)
    {
        return new AgentInstallTargetResolver(
            new AgentUserTargetRootResolver(() => homeDirectory, environment ?? (_ => null)));
    }
}
