using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ComputeDigest_IsRawLowercaseSha256AndIndependentOfPackageAndFileOrder ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var calculator = new SkillBundleDigestCalculator(new SkillManifestJsonSerializer());
        var reorderedPackages = bundle.Packages
            .Reverse()
            .Select(static package => SkillTestData.CreateCanonicalPackage(package.Manifest, package.Files.Reverse().ToArray()))
            .ToArray();

        var actual = calculator.ComputeDigest(reorderedPackages);

        Assert.Equal(bundle.Descriptor.BundleDigest, actual);
        Assert.Equal(64, actual.ToString().Length);
        Assert.Equal(actual.ToString().ToLowerInvariant(), actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ComputeDigest_ExcludesSkillBundleVersionAndDerivedManifestDigest ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var manifestSerializer = new SkillManifestJsonSerializer();
        var calculator = new SkillBundleDigestCalculator(manifestSerializer);
        var versionChangedPackages = bundle.Packages
            .Select(package => ReplaceManifest(
                package,
                SkillTestData.CopyManifest(
                    package.Manifest,
                    skillBundleVersion: package.Manifest.SkillBundleVersion.Next().Value,
                    manifestDigest: Sha256Digest.Parse(new string('f', 64))),
                manifestSerializer))
            .ToArray();

        var actual = calculator.ComputeDigest(versionChangedPackages);

        Assert.Equal(bundle.Descriptor.BundleDigest, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ComputeDigest_IncludesCategoryAndEveryCanonicalPackage ()
    {
        var bundle = await SkillTestData.GenerateFixtureBundleAsync();
        var manifestSerializer = new SkillManifestJsonSerializer();
        var calculator = new SkillBundleDigestCalculator(manifestSerializer);
        var first = bundle.Packages[0];
        var categoryChanged = ReplaceManifest(
            first,
            SkillTestData.CopyManifest(first.Manifest, category: new SkillCategory("extended")),
            manifestSerializer);
        var categoryChangedPackages = new[] { categoryChanged }.Concat(bundle.Packages.Skip(1)).ToArray();

        Assert.NotEqual(bundle.Descriptor.BundleDigest, calculator.ComputeDigest(categoryChangedPackages));
        Assert.NotEqual(bundle.Descriptor.BundleDigest, calculator.ComputeDigest(bundle.Packages.Skip(1)));
    }

    private static CanonicalSkillPackage ReplaceManifest (
        CanonicalSkillPackage package,
        SkillManifestCandidate manifest,
        SkillManifestJsonSerializer serializer)
    {
        var canonicalManifest = SkillTestData.WithComputedManifestDigest(manifest);
        var manifestFile = new SkillPackageFile("agent-skill.json", serializer.Serialize(canonicalManifest));
        return SkillTestData.CreateCanonicalPackage(
            canonicalManifest,
            package.Files
                .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal) ? manifestFile : file)
                .ToArray());
    }
}
