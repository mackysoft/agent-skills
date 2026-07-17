using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Packaging.Canonical;

public sealed class CanonicalSkillPackageTextEncodingTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsByteOrderMarkOnNonManifestFile ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "package-reader-skill-bom");
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var skillsRoot = scope.GetPath("skills");
        var writeResult = await SkillTestData.CreateCanonicalPackageWriter().WriteToStagingAsync(
            package,
            skillsRoot,
            CancellationToken.None);
        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);

        var skillPath = Path.Combine(skillsRoot, package.Manifest.SkillName.Value, "SKILL.md");
        var bytes = await File.ReadAllBytesAsync(skillPath);
        await File.WriteAllBytesAsync(skillPath, [0xEF, 0xBB, 0xBF, .. bytes]);

        var result = await SkillTestData.CreatePackageReader().ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("byte order mark", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsNonCanonicalLineEndings ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "package-reader-crlf");
        var package = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var skillsRoot = scope.GetPath("skills");
        var writeResult = await SkillTestData.CreateCanonicalPackageWriter().WriteToStagingAsync(
            package,
            skillsRoot,
            CancellationToken.None);
        Assert.True(writeResult.IsSuccess, writeResult.Failure?.Message);

        var skillPath = Path.Combine(skillsRoot, package.Manifest.SkillName.Value, "SKILL.md");
        var content = await File.ReadAllTextAsync(skillPath);
        await File.WriteAllTextAsync(skillPath, content.Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = await SkillTestData.CreatePackageReader().ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("LF line endings", result.Failure.Message, StringComparison.Ordinal);
    }
}
