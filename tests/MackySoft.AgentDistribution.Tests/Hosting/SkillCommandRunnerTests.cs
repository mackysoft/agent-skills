using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Composition;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.OperationReports.Contracts;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentDistribution.Tests.Hosting;

public sealed class SkillCommandRunnerTests
{
    private const string FixtureCatalogId = "com.mackysoft.agent-distribution";

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenSelectorsAreOmitted_ReturnsAllBundledCategories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "list-all");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.ListAsync(new SkillListCommandRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(0, result.ExitCode);
        var report = Assert.IsType<SkillListReport>(result.Payload);
        Assert.Equal(["core"], report.Categories);
        Assert.Empty(report.SkillNames);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.Equal(
            new[] { ("core", 4) },
            report.AvailableCategories.Select(static category => (category.Category, category.SkillCount)).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenSelectorIsOmitted_ReturnsInputFailureBeforeLoadingPackages ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-selector-required");
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                scope: "project",
                repositoryRoot: scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("--category", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("--skill", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenSelectedCategoryContainsWhitespace_ReturnsInputFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "list-category-whitespace");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.ListAsync(new SkillListCommandRequest(category: ["core "]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenUserTargetIsRelative_ReturnsPathFailureBeforeConstructingRequest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-relative-user-target");
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "user",
                targetDir: "relative-target"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenRepositoryRootIsOmitted_UsesConfiguredRepositoryRootResolver ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-resolved-root");
        var repositoryRootInput = Path.Combine(targetScope.FullPath, "nested", "..");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(
            packageScope.FullPath,
            repositoryRootResolver: _ => AbsolutePath.Parse(Path.GetFullPath(repositoryRootInput)));
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "project",
                dryRun: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(result.Payload);
        Assert.Equal(Path.GetFullPath(repositoryRootInput), report.RepositoryRoot);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(Path.Combine(targetScope.FullPath, ".agents", "skills", FixtureCatalogId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_ReturnsNormalizedTargetContextAndReloadGuidance ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-report-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-report-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.DoctorAsync(
            new SkillDoctorCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillDoctorReport>(result.Payload);
        FileSystemAssert.ForPath(report.RepositoryRoot!).EqualsNormalized(targetScope.FullPath);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(Path.Combine(targetScope.FullPath, ".agents", "skills", FixtureCatalogId));
        var registration = HostRegistration.Get(report.Host);
        Assert.True(registration.IsSuccess, registration.Failure?.Message);
        Assert.Equal(registration.Value!.Skill.ReloadGuidance, report.ReloadGuidance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenLegacyFlatInstallExists_UsesLegacyTargetRoot ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-legacy-flat-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-legacy-flat-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();
        var legacyTargetRoot = Path.Combine(targetScope.FullPath, ".agents", "skills");

        var installResult = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath,
                targetDir: legacyTargetRoot),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var doctorResult = await runner.DoctorAsync(
            new SkillDoctorCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(doctorResult.IsSuccess, doctorResult.Failure?.Message);
        var report = Assert.IsType<SkillDoctorReport>(doctorResult.Payload);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(legacyTargetRoot);
        Assert.True(report.IsHealthy);
        var diagnostic = Assert.Single(report.Diagnostics);
        Assert.Equal("SKILL_DOCTOR_OK", diagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenScopeIsUser_ReturnsNoRepositoryRoot ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-user-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "install-user-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "user",
                targetDir: targetScope.FullPath,
                dryRun: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(result.Payload);
        Assert.Null(report.RepositoryRoot);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(targetScope.FullPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenScopeIsUser_ReturnsNoRepositoryRoot ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-user-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-user-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.DoctorAsync(
            new SkillDoctorCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "user",
                targetDir: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillDoctorReport>(result.Payload);
        Assert.Null(report.RepositoryRoot);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(targetScope.FullPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenSelectedCategoryIsAbsentFromBundle_ReturnsInputFailure ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-empty-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "doctor-empty-target");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var result = await runner.DoctorAsync(
            new SkillDoctorCommandRequest(
                host: "codex",
                category: ["removed"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Unsupported SKILL category: removed", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenRemovedSkillNameIsSelected_PrunesOnlyThatInstalledSkill ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "prune-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "prune-target");
        var packages = await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var installResult = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var selectedOrphan = packages[0].Manifest.SkillName.Value;
        var unselectedOrphan = packages[1].Manifest.SkillName.Value;
        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages.Skip(2).ToArray()));

        var pruneResult = await runner.PruneAsync(
            new SkillPruneCommandRequest(
                host: "codex",
                skill: [selectedOrphan],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess, pruneResult.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(pruneResult.Payload);
        Assert.Equal(["core"], report.Categories);
        Assert.Equal([selectedOrphan], report.SkillNames);
        var action = Assert.Single(report.Actions);
        Assert.Equal(selectedOrphan, action.SkillName);
        Assert.Equal("deleted", action.Action);
        var targetRoot = Path.Combine(targetScope.FullPath, ".agents", "skills", FixtureCatalogId);
        Assert.False(Directory.Exists(Path.Combine(targetRoot, selectedOrphan)));
        Assert.True(Directory.Exists(Path.Combine(targetRoot, unselectedOrphan)));
        foreach (var skillName in packages.Skip(2).Select(static package => package.Manifest.SkillName.Value))
        {
            Assert.True(
                Directory.Exists(Path.Combine(targetRoot, skillName)),
                skillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenCategoryWasRemovedFromBundle_UsesInstalledManifestCategory ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "prune-removed-category-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-distribution-hosting", "prune-removed-category-target");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[0] = WithCategory(packages[0], new SkillCategory("removed"));
        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages));
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<SkillCommandRunner>();

        var installResult = await runner.InstallAsync(
            new SkillInstallCommandRequest(
                host: "codex",
                category: ["removed"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages.Skip(1).ToArray()));
        var pruneResult = await runner.PruneAsync(
            new SkillPruneCommandRequest(
                host: "codex",
                category: ["removed"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess, pruneResult.Failure?.Message);
        var report = Assert.IsType<SkillOperationReport>(pruneResult.Payload);
        Assert.Equal(["removed"], report.Categories);
        var action = Assert.Single(report.Actions);
        Assert.Equal(packages[0].Manifest.SkillName.Value, action.SkillName);
        Assert.Equal("deleted", action.Action);
    }

    private static ServiceProvider CreateProvider (
        string packageBaseDirectory,
        Func<AbsolutePath, AbsolutePath>? repositoryRootResolver = null)
    {
        var services = new ServiceCollection();
        services.AddAgentDistributionCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
            options.PackageBaseDirectory = AbsolutePath.Parse(packageBaseDirectory);
            if (repositoryRootResolver is not null)
            {
                options.RepositoryRootResolver = repositoryRootResolver;
            }
        });

        return services.BuildServiceProvider();
    }

    private static async Task<IReadOnlyList<CanonicalSkillPackage>> WriteFixturePackagesAsync (string packageBaseDirectory)
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        await WriteBundleAsync(packageBaseDirectory, bundle);
        return bundle.Packages;
    }

    private static async Task WriteBundleAsync (
        string packageBaseDirectory,
        CanonicalSkillBundle bundle)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleSerializer = new SkillBundleJsonSerializer();
        var bundleFactory = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(manifestSerializer));
        var writer = new CanonicalSkillBundleWriter(
            SkillTestData.CreateCanonicalPackageWriter(),
            bundleSerializer,
            new CanonicalSkillBundleReader(
                SkillTestData.CreatePackageReader(),
                bundleSerializer,
                bundleFactory));
        var result = await writer.WriteAsync(
            bundle,
            AbsolutePath.Parse(Path.Combine(packageBaseDirectory, "skills")),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    private static CanonicalSkillBundle CreateBundle (IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var identity = Assert.Single(packages.GroupBy(static package => (package.Manifest.CatalogId, package.Manifest.SkillBundleVersion))).Key;
        var descriptor = new SkillBundleDescriptor(
            SkillBundleDefinition.CurrentSchemaVersion,
            identity.CatalogId,
            identity.SkillBundleVersion,
            new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()).ComputeDigest(packages));
        return SkillTestData.CreateCanonicalBundle(descriptor, packages);
    }

    private static CanonicalSkillPackage WithCategory (
        CanonicalSkillPackage package,
        SkillCategory category)
    {
        var manifest = SkillTestData.WithComputedManifestDigest(SkillTestData.CopyManifest(package.Manifest, category: category));
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();
        return SkillTestData.CreateCanonicalPackage(manifest, files);
    }
}
