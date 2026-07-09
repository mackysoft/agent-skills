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
    private static readonly string[] Tiers = ["basic", "advanced", "developer"];

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenSelectorsAreOmitted_ReturnsAllConfiguredTiers ()
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
        Assert.Equal(Tiers, report.Tiers);
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
    public async Task ListAsync_WhenSelectedTierContainsWhitespace_ReturnsInputFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "list-tier-whitespace");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.ListAsync(new AgentSkillsListCommandRequest(Tier: ["advanced "]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenRepositoryRootIsOmitted_UsesConfiguredRepositoryRootResolver ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-resolved-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(
            packageScope.FullPath,
            repositoryRootResolver: _ => targetScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                Host: "openai",
                Tier: ["basic"],
                Scope: "project",
                DryRun: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(result.Payload);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(Path.Combine(targetScope.FullPath, ".agents", "skills"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenSelectedTierContainsNoPackages_ReturnsHealthyEmptyReport ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-empty-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-empty-target");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.DoctorAsync(
            new AgentSkillsDoctorCommandRequest(
                Host: "openai",
                Tier: ["advanced"],
                Scope: "project",
                RepositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(0, result.ExitCode);
        var report = Assert.IsType<SkillDoctorReport>(result.Payload);
        Assert.True(report.IsHealthy);
        Assert.Equal(["advanced"], report.Tiers);
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenRemovedSkillNameIsSelected_PrunesOnlyThatInstalledSkill ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-target");
        var packages = await WriteFixturePackagesAsync(packageScope.FullPath);
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

        var selectedOrphan = packages[0].Manifest.SkillName.Value;
        var unselectedOrphan = packages[1].Manifest.SkillName.Value;
        Directory.Delete(Path.Combine(packageScope.FullPath, "skills", selectedOrphan), recursive: true);
        Directory.Delete(Path.Combine(packageScope.FullPath, "skills", unselectedOrphan), recursive: true);

        var pruneResult = await runner.PruneAsync(
            new AgentSkillsPruneCommandRequest(
                Host: "openai",
                Skill: [selectedOrphan],
                Scope: "project",
                RepositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess, pruneResult.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(pruneResult.Payload);
        Assert.Equal(["basic", "advanced", "developer"], report.Tiers);
        Assert.Equal([selectedOrphan], report.SkillNames);
        var action = Assert.Single(report.Actions);
        Assert.Equal(selectedOrphan, action.SkillName);
        Assert.Equal("deleted", action.Action);
        Assert.False(Directory.Exists(Path.Combine(targetScope.FullPath, ".agents", "skills", selectedOrphan)));
        Assert.True(Directory.Exists(Path.Combine(targetScope.FullPath, ".agents", "skills", unselectedOrphan)));
        foreach (var skillName in packages.Skip(2).Select(static package => package.Manifest.SkillName.Value))
        {
            Assert.True(
                Directory.Exists(Path.Combine(targetScope.FullPath, ".agents", "skills", skillName)),
                skillName);
        }
    }

    private static ServiceProvider CreateProvider (
        string packageBaseDirectory,
        string commandRoot = "skills",
        Func<string, string>? repositoryRootResolver = null)
    {
        var services = new ServiceCollection();
        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.CatalogId = "com.mackysoft.agent-skills";
            options.Tiers = Tiers;
            options.PackageBaseDirectory = packageBaseDirectory;
            options.CommandRoot = commandRoot;
            if (repositoryRootResolver is not null)
            {
                options.RepositoryRootResolver = repositoryRootResolver;
            }
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
