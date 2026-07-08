using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandRunnerTests
{
    private static readonly string[] DefinedTiers = ["basic", "advanced", "developer"];

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenSelectorsAreOmitted_ReturnsAllDefinedTiers ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "list-all");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.ListAsync(new AgentSkillsListCommandRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("skills.list", result.Command);
        Assert.Equal(0, result.ExitCode);
        var report = Assert.IsType<SkillListReport>(result.Payload);
        Assert.Equal(DefinedTiers, report.Tiers);
        Assert.Empty(report.SkillNames);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.Equal(
            new[] { ("basic", 4), ("advanced", 0), ("developer", 0) },
            report.AvailableTiers.Select(static tier => (tier.Tier, tier.SkillCount)).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenCommandRootIsConfigured_ReturnsCommandNameWithConfiguredRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "list-custom-root");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath, "tools skills");
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.ListAsync(new AgentSkillsListCommandRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("tools.skills.list", result.Command);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenSelectorIsOmitted_ReturnsInputFailureBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-selector-required");
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                Host: "openai",
                Scope: "project",
                RepositoryRoot: scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("--tier", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("--skill", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_UsesCompleteCurrentCatalogEvenWhenSkillSelectorIsNarrow ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-target");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var installResult = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                Host: "openai",
                Tier: ["basic"],
                Scope: "project",
                RepositoryRoot: targetScope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var selectedSkill = SkillTestData.ExpectedSkillNames[0];
        var pruneResult = await runner.PruneAsync(
            new AgentSkillsPruneCommandRequest(
                Host: "openai",
                Skill: [selectedSkill],
                Scope: "project",
                RepositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess, pruneResult.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(pruneResult.Payload);
        Assert.Equal(["basic", "advanced", "developer"], report.Tiers);
        Assert.Equal([selectedSkill], report.SkillNames);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Actions.Select(static action => action.SkillName).ToArray());
        Assert.All(report.Actions, static action => Assert.Equal("skippedCurrent", action.Action));
        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            Assert.True(
                Directory.Exists(Path.Combine(targetScope.FullPath, ".agents", "skills", skillName)),
                skillName);
        }
    }

    private static ServiceProvider CreateProvider (
        string packageBaseDirectory,
        string commandRoot = "skills")
    {
        var services = new ServiceCollection();
        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.CatalogId = "com.mackysoft.agent-skills";
            options.DefinedTiers = DefinedTiers;
            options.PackageBaseDirectory = packageBaseDirectory;
            options.CommandRoot = commandRoot;
        });

        return services.BuildServiceProvider();
    }

    private static async Task<IReadOnlyList<CanonicalSkillPackage>> WriteFixturePackagesAsync (string packageBaseDirectory)
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var result = await new CanonicalSkillPackageWriter().WriteAllAsync(
            packages,
            Path.Combine(packageBaseDirectory, "skills"),
            cleanOutputRoot: true,
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return packages;
    }
}
