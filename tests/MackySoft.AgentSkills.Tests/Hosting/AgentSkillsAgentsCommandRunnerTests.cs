using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsAgentsCommandRunnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenSelectorIsOmitted_ReturnsAgentsInputFailureBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "agents-install-selector-required");
        using var provider = CreateProvider(scope.FullPath, "tools agents");
        var runner = provider.GetRequiredService<AgentSkillsAgentsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsAgentInstallCommandRequest(
                host: "openai",
                scope: "project",
                repositoryRoot: scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("tools.agents.install", result.Command);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("--category", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("--agent", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenUserAgentTargetIsRelative_ReturnsPathFailureBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "agents-doctor-relative-target");
        using var provider = CreateProvider(scope.FullPath, "agents");
        var runner = provider.GetRequiredService<AgentSkillsAgentsCommandRunner>();

        var result = await runner.DoctorAsync(
            new AgentSkillsAgentDoctorCommandRequest(
                host: "openai",
                category: ["planning"],
                scope: "user",
                agentTargetDir: "relative-target"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("agents.doctor", result.Command);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenSelectorIsOmitted_ReturnsAgentsInputFailureBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "agents-prune-selector-required");
        using var provider = CreateProvider(scope.FullPath, "agents");
        var runner = provider.GetRequiredService<AgentSkillsAgentsCommandRunner>();

        var result = await runner.PruneAsync(
            new AgentSkillsAgentPruneCommandRequest(
                host: "openai",
                scope: "project",
                repositoryRoot: scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("agents.prune", result.Command);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("--category", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("--agent", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenSkillHostHasNoAgentHostContract_ReturnsHostUnsupportedBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "agents-install-unsupported-host");
        using var provider = CreateProvider(scope.FullPath, "agents");
        var runner = provider.GetRequiredService<AgentSkillsAgentsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsAgentInstallCommandRequest(
                host: "claude",
                category: ["planning"],
                scope: "project",
                repositoryRoot: scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    private static ServiceProvider CreateProvider (string packageBaseDirectory, string agentsCommandRoot)
    {
        var services = new ServiceCollection();
        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = packageBaseDirectory;
            options.AgentsCommandRoot = agentsCommandRoot;
        });
        return services.BuildServiceProvider();
    }
}
