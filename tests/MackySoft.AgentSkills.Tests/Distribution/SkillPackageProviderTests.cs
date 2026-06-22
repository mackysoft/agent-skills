using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillPackageProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_FiltersByExactSelectedTiersAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "filter");
        await WritePackagesAsync(scope.FullPath, await SkillTestData.GenerateFixturePackagesAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(DefinedTierLiterals, ["basic", "advanced"], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, result.Value!.Select(static package => package.Manifest.SkillName).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_ReturnsEmptyList_WhenSelectedTierHasNoPackagesAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "empty-tier");
        await WritePackagesAsync(scope.FullPath, await SkillTestData.GenerateFixturePackagesAsync());
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(DefinedTierLiterals, ["advanced", "developer"], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_Fails_WhenNormalizedSelectionIsEmptyAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "empty-normalized-selection");
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(DefinedTierLiterals, Array.Empty<SkillTier>(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("At least one SKILL tier", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_Fails_WhenNormalizedSelectionContainsUndefinedTierAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "undefined-normalized-selection");
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(DefinedTierLiterals, [new SkillTier("internal")], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Contains("Unsupported SKILL tier: internal", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackagesAsync_Fails_WhenGeneratedPackageUsesUndefinedTierAsync ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-package-provider", "undefined-tier");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        packages = packages
            .Select((package, index) => index == 0 ? WithTier(package, new SkillTier("internal")) : package)
            .ToArray();
        await WritePackagesAsync(scope.FullPath, packages);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackagesAsync(DefinedTierLiterals, ["basic"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("undefined tier", result.Failure.Message, StringComparison.Ordinal);
    }

    private static SkillPackageProvider CreateProvider (string baseDirectory)
    {
        return new SkillPackageProvider(
            new BundledSkillPackageRootResolver(baseDirectory),
            SkillTestData.CreatePackageReader());
    }

    private static async Task WritePackagesAsync (
        string baseDirectory,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var packageRoot = Path.Combine(baseDirectory, "skills");
        Directory.CreateDirectory(packageRoot);

        foreach (var package in packages)
        {
            var skillDirectory = Path.Combine(packageRoot, package.Manifest.SkillName);
            foreach (var file in package.Files)
            {
                var filePath = Path.Combine(skillDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(filePath);
                Assert.False(string.IsNullOrWhiteSpace(directory));
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(filePath, file.Content);
            }
        }
    }

    private static CanonicalSkillPackage WithTier (
        CanonicalSkillPackage package,
        SkillTier tier)
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = package.Manifest with
        {
            Tier = tier,
        };
        manifest = new SkillManifestDigestCalculator(serializer).WithComputedManifestDigest(manifest);
        var manifestText = serializer.Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? SkillPackageFile.Create("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return package with
        {
            Manifest = manifest,
            Files = files,
        };
    }

    private static readonly string[] DefinedTierLiterals = ["basic", "advanced", "developer"];
}
