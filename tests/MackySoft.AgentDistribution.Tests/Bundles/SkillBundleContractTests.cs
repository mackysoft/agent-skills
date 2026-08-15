using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class SkillBundleContractTests
{
    private static readonly AgentDistributionCatalogId CatalogId = new("com.mackysoft.agent-distribution");
    private static readonly Sha256Digest Digest = Sha256Digest.Parse(new string('a', 64));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [Trait("Size", "Small")]
    public void Definition_RejectsUnsupportedSchema (int schemaVersion)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillBundleDefinition(
            schemaVersion,
            CatalogId,
            new SkillBundleVersion(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [Trait("Size", "Small")]
    public void Descriptor_RejectsUnsupportedSchema (int schemaVersion)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillBundleDescriptor(
            schemaVersion,
            CatalogId,
            new SkillBundleVersion(1),
            Digest));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Descriptor_RejectsNullDigest ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillBundleDescriptor(
            SkillBundleDefinition.CurrentSchemaVersion,
            CatalogId,
            new SkillBundleVersion(1),
            null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CanonicalBundle_CopiesAndSortsPackages ()
    {
        var generated = await SkillTestData.GenerateFixtureBundleAsync();
        var packages = generated.Packages.Reverse().ToList();
        var result = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()))
            .CreateCanonical(new CanonicalSkillBundleCandidate(generated.Descriptor, packages));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        var bundle = result.Value!;

        packages.Clear();

        Assert.Equal(
            generated.Packages.Select(static package => package.Manifest.SkillName),
            bundle.Packages.Select(static package => package.Manifest.SkillName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Factory_RejectsDuplicateSkillName ()
    {
        var generated = await SkillTestData.GenerateFixtureBundleAsync();
        var package = generated.Packages[0];

        var result = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()))
            .CreateCanonical(new CanonicalSkillBundleCandidate(
                generated.Descriptor,
                [package, package]));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Factory_RejectsPackageDependencyThatIsNotInBundle ()
    {
        var generated = await SkillTestData.GenerateFixtureBundleAsync();
        var package = generated.Packages[0];
        var replacement = CreatePackageWithDependencies(package, [new SkillName("missing-skill")]);

        var result = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()))
            .CreateCanonical(new CanonicalSkillBundleCandidate(
                generated.Descriptor,
                SkillTestData.ReplacePackage(generated.Packages, replacement)));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Equal(
            $"Generated SKILL bundle dependency was not found: {package.Manifest.SkillName.Value} -> missing-skill.",
            result.Failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Factory_RejectsPackageDependencyCycleWithCanonicalPath ()
    {
        var generated = await SkillTestData.GenerateFixtureBundleAsync();
        var first = generated.Packages[0];
        var second = generated.Packages[1];
        var firstReplacement = CreatePackageWithDependencies(first, [second.Manifest.SkillName]);
        var secondReplacement = CreatePackageWithDependencies(second, [first.Manifest.SkillName]);
        var packages = SkillTestData.ReplacePackage(
            SkillTestData.ReplacePackage(generated.Packages, firstReplacement),
            secondReplacement);

        var result = new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(new SkillManifestJsonSerializer()))
            .CreateCanonical(new CanonicalSkillBundleCandidate(generated.Descriptor, packages));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Equal(
            $"Generated SKILL bundle dependency cycle was found: {first.Manifest.SkillName.Value} -> {second.Manifest.SkillName.Value} -> {first.Manifest.SkillName.Value}.",
            result.Failure.Message);
    }

    private static CanonicalSkillPackage CreatePackageWithDependencies (
        CanonicalSkillPackage package,
        IReadOnlyList<SkillName> dependencies)
    {
        var manifest = SkillTestData.WithComputedManifestDigest(
            SkillTestData.CopyManifest(package.Manifest, dependencies: dependencies));
        var manifestFile = new PackageTextFile(
            PackageRelativePath.Parse("agent-skill.json"),
            new SkillManifestJsonSerializer().Serialize(manifest));
        return SkillTestData.CreateCanonicalPackage(
            manifest,
            package.Files
                .Select(file => file.RelativePath.Value == "agent-skill.json" ? manifestFile : file)
                .ToArray());
    }
}
