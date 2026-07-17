using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillPackageProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_DerivesAvailableCategoriesAndDescriptorFromBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "catalog");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        packages[2] = WithCategory(packages[2], new SkillCategory("developer"));
        var bundle = CreateBundle(packages.Reverse().ToArray());
        await WriteBundleAsync(scope.FullPath, bundle);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(bundle.Descriptor.SchemaVersion, result.Value!.BundleDescriptor.SchemaVersion);
        Assert.Equal(bundle.Descriptor.CatalogId, result.Value.BundleDescriptor.CatalogId);
        Assert.Equal(bundle.Descriptor.SkillBundleVersion, result.Value.BundleDescriptor.SkillBundleVersion);
        Assert.Equal(bundle.Descriptor.BundleDigest, result.Value.BundleDescriptor.BundleDigest);
        Assert.Equal(["advanced", "core", "developer"], result.Value.SelectedCategories.Select(static item => item.Value).ToArray());
        Assert.Empty(result.Value.SelectedSkillNames);
        Assert.Equal(
            new[] { ("advanced", 1), ("core", 2), ("developer", 1) },
            result.Value.AvailableCategories.Select(static item => (item.Category.Value, item.PackageCount)).ToArray());
        Assert.Equal(SkillTestData.ExpectedSkillNames, result.Value.Packages.Select(static package => package.Manifest.SkillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_FiltersByExactSelectedCategories ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "category-filter");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(["advanced"], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal([packages[1].Manifest.SkillName.Value], result.Value!.Select(static package => package.Manifest.SkillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_DeduplicatesSelectionsInCallerOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "deduplicate-selection");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(
            ["core", "advanced", "core"],
            [packages[0].Manifest.SkillName.Value, packages[0].Manifest.SkillName.Value],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["core", "advanced"], result.Value!.SelectedCategories.Select(static item => item.Value).ToArray());
        Assert.Equal([packages[0].Manifest.SkillName.Value], result.Value.SelectedSkillNames.Select(static item => item.Value).ToArray());
        Assert.Equal([packages[0].Manifest.SkillName.Value], result.Value.Packages.Select(static package => package.Manifest.SkillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesBySkillNamesAsync_IncludesTransitiveDependencies ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "dependencies");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[0] = WithDependencies(packages[0], [packages[1].Manifest.SkillName.Value]);
        packages[1] = WithDependencies(packages[1], [packages[2].Manifest.SkillName.Value]);
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesBySkillNamesAsync([packages[0].Manifest.SkillName.Value], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            new[] { packages[0].Manifest.SkillName.Value, packages[1].Manifest.SkillName.Value, packages[2].Manifest.SkillName.Value }.Order(StringComparer.Ordinal),
            result.Value!.Select(static package => package.Manifest.SkillName.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_IncludesDependencyOutsideSelectedCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "cross-category-dependency");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        packages[0] = WithDependencies(packages[0], [packages[1].Manifest.SkillName.Value]);
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(
            ["core"],
            [packages[0].Manifest.SkillName.Value],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            new[] { packages[0].Manifest.SkillName.Value, packages[1].Manifest.SkillName.Value }.Order(StringComparer.Ordinal),
            result.Value!.Select(static package => package.Manifest.SkillName.Value));
        Assert.Equal("advanced", result.Value!.Single(package => package.Manifest.SkillName == packages[1].Manifest.SkillName).Manifest.Category.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsUnknownCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "unknown-category");
        await WriteBundleAsync(scope.FullPath, await SkillTestData.GenerateFixtureBundleAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(["internal"], [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Unsupported SKILL category: internal", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("core", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsSkillOutsideSelectedCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "category-name-mismatch");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(
            ["advanced"],
            [packages[0].Manifest.SkillName.Value],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("does not match selected categories", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogBySkillNamesAsync_RejectsMissingSkillName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "missing-name");
        await WriteBundleAsync(scope.FullPath, await SkillTestData.GenerateFixtureBundleAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogBySkillNamesAsync(["missing-skill"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Selected SKILL name was not found: missing-skill", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_WhenBundleRootIsMissing_ReturnsSourceFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "missing-root");
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("package root", result.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_WhenBundleDescriptorIsMissing_ReturnsManifestFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "missing-descriptor");
        Directory.CreateDirectory(Path.Combine(scope.FullPath, "skills"));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("bundle.json", result.Failure.Message, StringComparison.Ordinal);
    }

    private static SkillPackageProvider CreateProvider (string baseDirectory)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleSerializer = new SkillBundleJsonSerializer();
        var bundleFactory = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(manifestSerializer));
        return new SkillPackageProvider(
            new BundledSkillPackageRootResolver(baseDirectory),
            new CanonicalSkillBundleReader(
                SkillTestData.CreatePackageReader(),
                bundleSerializer,
                bundleFactory));
    }

    private static async Task WriteBundleAsync (
        string baseDirectory,
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
        var result = await writer.WriteAsync(bundle, Path.Combine(baseDirectory, "skills"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    private static CanonicalSkillBundle CreateBundle (IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var package = Assert.Single(packages.GroupBy(static item => (item.Manifest.CatalogId, item.Manifest.SkillBundleVersion)));
        var descriptor = new SkillBundleDescriptor(
            SkillBundleDefinition.CurrentSchemaVersion,
            package.Key.CatalogId,
            package.Key.SkillBundleVersion,
            new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()).ComputeDigest(packages));
        return SkillTestData.CreateCanonicalBundle(descriptor, packages);
    }

    private static CanonicalSkillPackage WithCategory (
        CanonicalSkillPackage package,
        SkillCategory category)
    {
        return WithManifest(package, SkillTestData.CopyManifest(package.Manifest, category: category));
    }

    private static CanonicalSkillPackage WithDependencies (
        CanonicalSkillPackage package,
        IReadOnlyList<string> dependencies)
    {
        return WithManifest(package, SkillTestData.CopyManifest(
            package.Manifest,
            dependencies: dependencies
                .Order(StringComparer.Ordinal)
                .Select(static dependency => new SkillName(dependency))
                .ToArray()));
    }

    private static CanonicalSkillPackage WithManifest (
        CanonicalSkillPackage package,
        SkillManifestCandidate manifest)
    {
        var serializer = new SkillManifestJsonSerializer();
        var normalizedManifest = SkillTestData.WithComputedManifestDigest(manifest);
        var manifestText = serializer.Serialize(normalizedManifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return SkillTestData.CreateCanonicalPackage(normalizedManifest, files);
    }
}
