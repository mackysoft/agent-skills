using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Distribution;

public sealed class SkillPackageProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_ReadsSkillNamespaceFromV2Bundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "v3-skill-view");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        await WriteV2BundleAsync(scope.FullPath, packages);
        var provider = CreateV2Provider(scope.FullPath);

        var result = await provider.GetPackageCatalogBySkillNamesAsync(
            [packages[0].Manifest.SkillName.Value],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(packages[0].Manifest.CatalogId, result.Value!.BundleDescriptor.CatalogId);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion, result.Value.BundleDescriptor.SkillBundleVersion);
        Assert.Equal(
            [packages[0].Manifest.SkillName.Value],
            result.Value.Packages.Select(static package => package.Manifest.SkillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_DerivesAvailableCategoriesAndDescriptorFromBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "catalog");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "category-filter");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "deduplicate-selection");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "dependencies");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "cross-category-dependency");
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "unknown-category");
        await WriteBundleAsync(scope.FullPath, await SkillTestData.GenerateFixtureBundleAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(["internal"], [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Unsupported SKILL category: internal", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("core", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsSkillOutsideSelectedCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "category-name-mismatch");
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[1] = WithCategory(packages[1], new SkillCategory("advanced"));
        await WriteBundleAsync(scope.FullPath, CreateBundle(packages));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(
            ["advanced"],
            [packages[0].Manifest.SkillName.Value],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("does not match selected categories", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogBySkillNamesAsync_RejectsMissingSkillName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "missing-name");
        await WriteBundleAsync(scope.FullPath, await SkillTestData.GenerateFixtureBundleAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogBySkillNamesAsync(["missing-skill"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Selected SKILL name was not found: missing-skill", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_WhenBundleRootIsMissing_ReturnsSourceFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "missing-root");
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("package root", result.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_WhenBundleDescriptorIsMissing_ReturnsManifestFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-package-provider", "missing-descriptor");
        Directory.CreateDirectory(Path.Combine(scope.FullPath, "agent-distribution"));
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("bundle.json", result.Failure.Message, StringComparison.Ordinal);
    }

    private static SkillPackageProvider CreateProvider (string baseDirectory)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleSerializer = new SkillBundleJsonSerializer();
        var bundleFactory = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(manifestSerializer));
        return new SkillPackageProvider(
            new BundledAgentDistributionPackageRootResolver(AbsolutePath.Parse(baseDirectory)),
            new CanonicalSkillBundleReader(
                SkillTestData.CreatePackageReader(),
                bundleSerializer,
                bundleFactory));
    }

    private static SkillPackageProvider CreateV2Provider (string baseDirectory)
    {
        var manifestSerializer = new SkillManifestJsonSerializer();
        var bundleSerializer = new SkillBundleJsonSerializer();
        var bundleDigestCalculator = new SkillBundleDigestCalculator(manifestSerializer);
        return new SkillPackageProvider(
            new BundledAgentDistributionPackageRootResolver(AbsolutePath.Parse(baseDirectory)),
            new CanonicalSkillBundleReader(
                SkillTestData.CreatePackageReader(),
                bundleSerializer,
                new CanonicalSkillBundle.Factory(bundleDigestCalculator)),
            CanonicalAgentDistributionBundleReader.CreateDefault(),
            bundleDigestCalculator);
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
        var result = await writer.WriteAsync(bundle, AbsolutePath.Parse(Path.Combine(baseDirectory, "agent-distribution")), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    private static async Task WriteV2BundleAsync (
        string baseDirectory,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new PackageContentDigestCalculator();
        var descriptor = new AgentDistributionBundleDescriptor(
            AgentDistributionBundleDefinition.CurrentSchemaVersion,
            packages[0].Manifest.CatalogId,
            new AgentDistributionBundleVersion(packages[0].Manifest.SkillBundleVersion.Value),
            new AgentDistributionBundleDigestCalculator(
                skillManifestSerializer,
                agentManifestSerializer,
                digestCalculator).ComputeDigest(packages, []));
        var bundle = new CanonicalAgentDistributionBundle(descriptor, packages, []);
        var bundleReader = CanonicalAgentDistributionBundleReader.CreateDefault();
        var writer = new CanonicalAgentDistributionBundleWriter(
            new CanonicalSkillPackageWriter(),
            new CanonicalAgentPackageWriter(),
            new AgentDistributionBundleJsonSerializer(),
            bundleReader);

        var result = await writer.WriteAsync(
            bundle,
            AbsolutePath.Parse(Path.Combine(baseDirectory, "agent-distribution")),
            CancellationToken.None);
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
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();

        return SkillTestData.CreateCanonicalPackage(normalizedManifest, files);
    }
}
