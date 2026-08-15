using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Factory_CopiesFileCollection ()
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var files = generated.Files.ToList();
        var package = SkillTestData.CreateCanonicalPackage(generated.Manifest, files);

        files.Clear();

        Assert.NotEmpty(package.Files);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Factory_SortsFilesByOrdinalPath ()
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var files = generated.Files.Reverse().ToArray();

        var package = SkillTestData.CreateCanonicalPackage(generated.Manifest, files);

        Assert.Equal(
            files.Select(static file => file.RelativePath.Value).Order(StringComparer.Ordinal),
            package.Files.Select(static file => file.RelativePath.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Candidate_RejectsNullFile ()
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];

        Assert.Throws<ArgumentException>(() => new CanonicalSkillPackageCandidate(
            generated.Manifest,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "content\n"), null!]));
    }

    [Theory]
    [InlineData("references/example.md", "references/example.md")]
    [InlineData("references/example.md", "REFERENCES/EXAMPLE.MD")]
    [InlineData("scripts/collect.sh", "scripts/COLLECT.sh")]
    [Trait("Size", "Small")]
    public async Task Factory_RejectsNonPortableDuplicateFilePath (
        string firstPath,
        string secondPath)
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];

        var factory = new CanonicalSkillPackage.Factory(
            new PackageContentDigestCalculator(),
            new SkillManifestJsonSerializer());
        var result = factory.CreateCanonical(new CanonicalSkillPackageCandidate(
            generated.Manifest,
            [
                new PackageTextFile(PackageRelativePath.Parse(firstPath), "first\n"),
                new PackageTextFile(PackageRelativePath.Parse(secondPath), "second\n"),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }
}
