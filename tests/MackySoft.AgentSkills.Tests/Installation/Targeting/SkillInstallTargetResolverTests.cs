using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Targeting;

public sealed class SkillInstallTargetResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_ProjectScope_UsesHostProjectTargetUnderRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-project-default");
        var resolver = CreateResolver(scope.GetPath("home"));

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(scope.FullPath, ".agents", "skills")), result.Value!.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_UsesCodexHomeWhenPresent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-codex-home");
        var codexHome = scope.GetPath("codex-home");
        var resolver = CreateResolver(scope.GetPath("home"), codexHome);

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(codexHome, "skills")), result.Value!.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_FallsBackToCodexDirectoryUnderHome ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-home-fallback");
        var home = scope.GetPath("home");
        var resolver = CreateResolver(home);

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".codex", "skills")), result.Value!.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_UsesClaudeHomeDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-claude-home");
        var home = scope.GetPath("home");
        var resolver = CreateResolver(home);

        var result = resolver.ResolveTarget(new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".claude", "skills")), result.Value!.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_UsesCopilotHomeDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-copilot-home");
        var home = scope.GetPath("home");
        var resolver = CreateResolver(home);

        var result = resolver.ResolveTarget(new SkillInstallRequest(CopilotSkillHostAdapter.HostKey, SkillScopeKind.User, null));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ResolveExpectedPath(Path.Combine(home, ".copilot", "skills")), result.Value!.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_RejectsRelativeCodexHome ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-relative-codex-home");
        var resolver = CreateResolver(scope.GetPath("home"), "relative-codex-home");

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.UserTargetUnavailable, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_RejectsRelativeExplicitTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-relative");
        var resolver = CreateResolver(scope.GetPath("home"));

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User, null, "relative-skills"));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UserScope_RejectsRepositoryRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-user-repo-root");
        var resolver = CreateResolver(scope.GetPath("home"));

        var result = resolver.ResolveTarget(new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.User, scope.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTarget_UnsupportedHost_ReturnsHostUnsupported ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "target-unsupported-host");
        var resolver = CreateResolver(scope.GetPath("home"));

        var result = resolver.ResolveTarget(new SkillInstallRequest("generic", SkillScopeKind.User, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static SkillInstallTargetResolver CreateResolver (
        string homeDirectory,
        string? codexHome = null)
    {
        return new SkillInstallTargetResolver(
            SkillTestData.CreateDefaultHostAdapterSet(),
            new SkillUserTargetRootResolver(
                () => homeDirectory,
                name => string.Equals(name, "CODEX_HOME", StringComparison.Ordinal) ? codexHome : null));
    }

    private static string ResolveExpectedPath (string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return Path.GetFullPath(path);
        }

        var currentPath = root;
        var relativePath = path[root.Length..];
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]);
            if (!Directory.Exists(currentPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(currentPath);
            var resolved = directory.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is not null)
            {
                currentPath = resolved.FullName;
            }
        }

        return Path.GetFullPath(currentPath);
    }
}
