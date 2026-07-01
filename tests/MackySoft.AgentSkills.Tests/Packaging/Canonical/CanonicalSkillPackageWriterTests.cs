using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllAsync_AcceptsSkillsOutputRootWithTrailingSeparator ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "writer-trailing-separator");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var outputRoot = scope.GetPath("skills") + Path.DirectorySeparatorChar;
        var writer = new CanonicalSkillPackageWriter();

        var result = await writer.WriteAllAsync(packages, outputRoot, cleanOutputRoot: true, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(Directory.Exists(Path.Combine(result.Value!, packages[0].Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllAsync_RejectsEmptyPackageSet ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "writer-empty");
        var outputRoot = scope.CreateDirectory("skills");
        var existingFile = scope.WriteFile("skills/existing.txt", "existing");
        var writer = new CanonicalSkillPackageWriter();

        var result = await writer.WriteAllAsync([], outputRoot, cleanOutputRoot: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.True(File.Exists(existingFile));
    }
}
