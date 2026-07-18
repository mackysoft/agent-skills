using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandRunnerTests
{
    private const string FixtureCatalogId = "com.mackysoft.agent-skills";

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenSelectorsAreOmitted_ReturnsAllBundledCategories ()
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
        Assert.Equal(["core"], report.Categories);
        Assert.Empty(report.SkillNames);
        Assert.Equal(SkillTestData.ExpectedSkillNames, report.Skills.Select(static skill => skill.SkillName).ToArray());
        Assert.Equal(
            new[] { ("core", 4) },
            report.AvailableCategories.Select(static category => (category.Category, category.SkillCount)).ToArray());
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
                host: "openai",
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
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "list-category-whitespace");
        await WriteFixturePackagesAsync(scope.FullPath);
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.ListAsync(new AgentSkillsListCommandRequest(category: ["core "]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenUserTargetIsRelative_ReturnsPathFailureBeforeConstructingRequest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-relative-user-target");
        using var provider = CreateProvider(scope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-resolved-root");
        var repositoryRootInput = Path.Combine(targetScope.FullPath, "nested", "..");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(
            packageScope.FullPath,
            repositoryRootResolver: _ => repositoryRootInput);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-report-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-report-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.DoctorAsync(
            new AgentSkillsDoctorCommandRequest(
                host: "openai",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var report = Assert.IsType<SkillDoctorReport>(result.Payload);
        FileSystemAssert.ForPath(report.RepositoryRoot!).EqualsNormalized(targetScope.FullPath);
        FileSystemAssert.ForPath(report.TargetRoot).EqualsNormalized(Path.Combine(targetScope.FullPath, ".agents", "skills", FixtureCatalogId));
        var adapter = SkillTestData.CreateDefaultHostAdapterSet().GetAdapter(report.Host);
        Assert.True(adapter.IsSuccess, adapter.Failure?.Message);
        Assert.Equal(adapter.Value!.Descriptor.ReloadGuidance, report.ReloadGuidance);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DoctorAsync_WhenLegacyFlatInstallExists_UsesLegacyTargetRoot ()
    {
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-legacy-flat-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-legacy-flat-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();
        var legacyTargetRoot = Path.Combine(targetScope.FullPath, ".agents", "skills");

        var installResult = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath,
                targetDir: legacyTargetRoot),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var doctorResult = await runner.DoctorAsync(
            new AgentSkillsDoctorCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-user-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "install-user-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-user-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-user-target-root");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.DoctorAsync(
            new AgentSkillsDoctorCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-empty-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "doctor-empty-target");
        await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var result = await runner.DoctorAsync(
            new AgentSkillsDoctorCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-target");
        var packages = await WriteFixturePackagesAsync(packageScope.FullPath);
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var installResult = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
                category: ["core"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var selectedOrphan = packages[0].Manifest.SkillName.Value;
        var unselectedOrphan = packages[1].Manifest.SkillName.Value;
        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages.Skip(2).ToArray()));

        var pruneResult = await runner.PruneAsync(
            new AgentSkillsPruneCommandRequest(
                host: "openai",
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
        using var packageScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-removed-category-package-root");
        using var targetScope = TestDirectories.CreateTempScope("agent-skills-hosting", "prune-removed-category-target");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[0] = WithCategory(packages[0], new SkillCategory("removed"));
        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages));
        using var provider = CreateProvider(packageScope.FullPath);
        var runner = provider.GetRequiredService<AgentSkillsCommandRunner>();

        var installResult = await runner.InstallAsync(
            new AgentSkillsInstallCommandRequest(
                host: "openai",
                category: ["removed"],
                scope: "project",
                repositoryRoot: targetScope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        await WriteBundleAsync(packageScope.FullPath, CreateBundle(packages.Skip(1).ToArray()));
        var pruneResult = await runner.PruneAsync(
            new AgentSkillsPruneCommandRequest(
                host: "openai",
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
        string commandRoot = "skills",
        Func<string, string>? repositoryRootResolver = null)
    {
        var services = new ServiceCollection();
        services.AddAgentSkillsCommandRuntime(options =>
        {
            options.ProductName = "Example CLI";
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
            Path.Combine(packageBaseDirectory, "skills"),
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
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile("agent-skill.json", manifestText)
                : file)
            .ToArray();
        return SkillTestData.CreateCanonicalPackage(manifest, files);
    }
}
