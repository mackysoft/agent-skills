using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Validation;

public sealed class SkillInstalledFileSetVerifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReturnsExactMatch_WhenInstalledFilesMatchExpectedFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-exact");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "reference.md"), "# Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "agents", "openai.yaml"), "name: Sample\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                SkillPackageFile.Create("SKILL.md", "# Skill\n"),
                SkillPackageFile.Create("references/reference.md", "# Reference\n"),
                SkillPackageFile.Create("agents/openai.yaml", "name: Sample\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.IsExactMatch);
        Assert.False(result.Value.HasFileSetDrift);
        Assert.False(result.Value.HasContentDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.MismatchedFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsMissingExpectedFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-missing");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                SkillPackageFile.Create("SKILL.md", "# Skill\n"),
                SkillPackageFile.Create("references/reference.md", "# Reference\n"),
                SkillPackageFile.Create("agents/openai.yaml", "name: Sample\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.False(result.Value.HasContentDrift);
        Assert.Equal(["SKILL.md", "agents/openai.yaml", "references/reference.md"], result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.MismatchedFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsExtraInstalledFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-extra-files");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "reference.md"), "# Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "extra.md"), "# Extra Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "local.md"), "# Local\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                SkillPackageFile.Create("SKILL.md", "# Skill\n"),
                SkillPackageFile.Create("references/reference.md", "# Reference\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.False(result.Value.HasContentDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Equal(["local.md", "references/extra.md"], result.Value.ExtraFiles);
        Assert.Empty(result.Value.MismatchedFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsMismatchedExpectedFileContent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-mismatched-content");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Actual\r\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [SkillPackageFile.Create("SKILL.md", "# Expected\n")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.False(result.Value.HasFileSetDrift);
        Assert.True(result.Value.HasContentDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Equal(["SKILL.md"], result.Value.MismatchedFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsExtraEmptyDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-extra-directory");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "empty"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [SkillPackageFile.Create("SKILL.md", "# Skill\n")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.False(result.Value.HasContentDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.MismatchedFiles);
        Assert.Equal(["empty"], result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsUnsafeExpectedPath ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-unsafe-path");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new SkillPackageFile("../escape.md", "# Escape\n")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsExpectedPathThatIsDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-expected-directory");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "SKILL.md"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [SkillPackageFile.Create("SKILL.md", "# Skill\n")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsExpectedFileSymlinkThatEscapesDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "file-set-symlink-outside");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        var targetPath = outsideScope.WriteFile("SKILL.md", "# Outside\n");
        if (!TestSymbolicLinks.TryCreateFile(Path.Combine(skillDirectory, "SKILL.md"), targetPath))
        {
            return;
        }

        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [SkillPackageFile.Create("SKILL.md", "# Skill\n")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
