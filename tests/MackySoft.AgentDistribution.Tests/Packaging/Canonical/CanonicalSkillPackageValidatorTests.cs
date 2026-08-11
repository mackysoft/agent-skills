using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AcceptsGeneratedPackage ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var factory = CreateFactory();

        var result = factory.CreateCanonical(new CanonicalSkillPackageCandidate(package.Manifest, package.Files));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.NotSame(package, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_RejectsManifestThatDoesNotMatchAgentSkillJson ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var serializer = new SkillManifestJsonSerializer();
        var changedManifest = SkillTestData.WithComputedManifestDigest(SkillTestData.CopyManifest(
            package.Manifest,
            displayName: package.Manifest.DisplayName + " changed"));
        var changedPackage = new CanonicalSkillPackageCandidate(changedManifest, package.Files);
        var factory = CreateFactory(serializer);

        var result = factory.CreateCanonical(changedPackage);

        Assert.False(result.IsSuccess);
        Assert.Contains("in-memory manifest", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_RejectsNonCanonicalAgentSkillJson ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var changedPackage = ReplaceFile(
            package,
            "agent-skill.json",
            static file => new PackageTextFile(file.RelativePath, file.Content + "\n"));
        var factory = CreateFactory();

        var result = factory.CreateCanonical(changedPackage);

        Assert.False(result.IsSuccess);
        Assert.Contains("not canonical", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_RejectsUnsupportedFileSet ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var changedPackage = new CanonicalSkillPackageCandidate(
            package.Manifest,
            [.. package.Files, new PackageTextFile(PackageRelativePath.Parse("unsupported.txt"), "unsupported\n")]);
        var factory = CreateFactory();

        var result = factory.CreateCanonical(changedPackage);

        Assert.False(result.IsSuccess);
        Assert.Contains("unsupported file", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_RejectsContentDigestDrift ()
    {
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var changedPackage = ReplaceFile(
            package,
            "SKILL.md",
            static file => new PackageTextFile(file.RelativePath, file.Content + "drift\n"));
        var factory = CreateFactory();

        var result = factory.CreateCanonical(changedPackage);

        Assert.False(result.IsSuccess);
        Assert.Contains("contentDigest", result.Failure!.Message, StringComparison.Ordinal);
    }

    private static CanonicalSkillPackage.Factory CreateFactory (SkillManifestJsonSerializer? manifestSerializer = null)
    {
        manifestSerializer ??= new SkillManifestJsonSerializer();
        return new CanonicalSkillPackage.Factory(
            new PackageContentDigestCalculator(),
            manifestSerializer);
    }

    private static CanonicalSkillPackageCandidate ReplaceFile (
        CanonicalSkillPackage package,
        string relativePath,
        Func<PackageTextFile, PackageTextFile> replace)
    {
        return new CanonicalSkillPackageCandidate(
            package.Manifest,
            package.Files
                .Select(file => string.Equals(file.RelativePath.Value, relativePath, StringComparison.Ordinal) ? replace(file) : file)
                .ToArray());
    }
}
