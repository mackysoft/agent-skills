using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Packaging.Canonical;

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
            files.Select(static file => file.RelativePath).Order(StringComparer.Ordinal),
            package.Files.Select(static file => file.RelativePath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Candidate_RejectsNullFile ()
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];

        Assert.Throws<ArgumentException>(() => new CanonicalSkillPackageCandidate(
            generated.Manifest,
            [new SkillPackageFile("SKILL.md", "content\n"), null!]));
    }

    [Theory]
    [InlineData("references/example.md", "references/example.md")]
    [InlineData("references/example.md", "REFERENCES/EXAMPLE.MD")]
    [Trait("Size", "Small")]
    public async Task Factory_RejectsNonPortableDuplicateFilePath (
        string firstPath,
        string secondPath)
    {
        var generated = (await SkillTestData.GenerateFixturePackagesAsync())[0];

        var factory = new CanonicalSkillPackage.Factory(
            SkillTestData.CreateDefaultHostAdapterSet(),
            new SkillDigestCalculator(),
            new SkillManifestJsonSerializer());
        var result = factory.CreateCanonical(new CanonicalSkillPackageCandidate(
            generated.Manifest,
            [
                new SkillPackageFile(firstPath, "first\n"),
                new SkillPackageFile(secondPath, "second\n"),
            ]));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }
}
