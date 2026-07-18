using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleContractTests
{
    private static readonly SkillCatalogId CatalogId = new("com.mackysoft.agent-skills");
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
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }
}
