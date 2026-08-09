using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillPackageDependencyResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_IncludesTransitiveDependenciesOnceAndOrdersPackagesOrdinally ()
    {
        var packages = (await SkillTestData.GenerateFixturePackagesAsync()).ToArray();
        packages[0] = WithDependencies(packages[0], [packages[1].Manifest.SkillName.Value, packages[2].Manifest.SkillName.Value]);
        packages[1] = WithDependencies(packages[1], [packages[2].Manifest.SkillName.Value]);

        var result = SkillPackageDependencyResolver.Resolve(
            packages.Reverse().ToArray(),
            [packages[1].Manifest.SkillName, packages[0].Manifest.SkillName, packages[0].Manifest.SkillName]);

        var expectedSkillNames = new[]
        {
            packages[0].Manifest.SkillName.Value,
            packages[1].Manifest.SkillName.Value,
            packages[2].Manifest.SkillName.Value,
        }.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedSkillNames, result.Select(static package => package.Manifest.SkillName.Value).ToArray());
    }

    private static CanonicalSkillPackage WithDependencies (
        CanonicalSkillPackage package,
        IReadOnlyList<string> dependencies)
    {
        var manifestCandidate = SkillTestData.CopyManifest(
            package.Manifest,
            dependencies: dependencies
                .Order(StringComparer.Ordinal)
                .Select(static dependency => new SkillName(dependency))
                .ToArray());
        var manifest = SkillTestData.WithComputedManifestDigest(manifestCandidate);
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath.Value, "agent-skill.json", StringComparison.Ordinal)
                ? new PackageTextFile(PackageRelativePath.Parse("agent-skill.json"), manifestText)
                : file)
            .ToArray();

        return SkillTestData.CreateCanonicalPackage(manifest, files);
    }
}
