using MackySoft.AgentDistribution.Installation.Validation;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Installation.Validation;

public sealed class SkillInstalledFileSetVerifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReturnsExactMatch_WhenInstalledFilesMatchExpectedFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-exact");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "reference.md"), "# Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "agents", "openai.yaml"), "name: Sample\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n"),
                new PackageTextFile(PackageRelativePath.Parse("references/reference.md"), "# Reference\n"),
                new PackageTextFile(PackageRelativePath.Parse("agents/openai.yaml"), "name: Sample\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.IsExactMatch);
        Assert.False(result.Value.HasFileSetDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsMissingExpectedFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-missing");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n"),
                new PackageTextFile(PackageRelativePath.Parse("references/reference.md"), "# Reference\n"),
                new PackageTextFile(PackageRelativePath.Parse("agents/openai.yaml"), "name: Sample\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.Equal(
            [
                PackageRelativePath.Parse("SKILL.md"),
                PackageRelativePath.Parse("agents/openai.yaml"),
                PackageRelativePath.Parse("references/reference.md"),
            ],
            result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsExtraInstalledFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-extra-files");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "reference.md"), "# Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "references", "extra.md"), "# Extra Reference\n");
        scope.WriteFile(Path.Combine("sample-skill", "local.md"), "# Local\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n"),
                new PackageTextFile(PackageRelativePath.Parse("references/reference.md"), "# Reference\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Equal(
            [PackageRelativePath.Parse("local.md"), PackageRelativePath.Parse("references/extra.md")],
            result.Value.ExtraFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_IgnoresContentDifferences_WhenFileSetMatches ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-content-drift");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Actual\r\n");
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Expected\n")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.IsExactMatch);
        Assert.False(result.Value.HasFileSetDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Empty(result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsAllFileSetDriftKindsTogether ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-combined-drift");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Drifted\n");
        scope.WriteFile(Path.Combine("sample-skill", "local.md"), "# Local\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory.Value, "empty"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [
                new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Expected\n"),
                new PackageTextFile(PackageRelativePath.Parse("references/missing.md"), "# Missing\n"),
            ],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.Equal([PackageRelativePath.Parse("references/missing.md")], result.Value.MissingFiles);
        Assert.Equal([PackageRelativePath.Parse("local.md")], result.Value.ExtraFiles);
        Assert.Equal([PackageRelativePath.Parse("empty")], result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_ReportsExtraEmptyDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-extra-directory");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory.Value, "empty"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n")],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.False(result.Value!.IsExactMatch);
        Assert.True(result.Value.HasFileSetDrift);
        Assert.Empty(result.Value.MissingFiles);
        Assert.Empty(result.Value.ExtraFiles);
        Assert.Equal([PackageRelativePath.Parse("empty")], result.Value.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsExpectedPathThatIsDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-expected-directory");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        Directory.CreateDirectory(Path.Combine(skillDirectory.Value, "SKILL.md"));
        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n")],
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-symlink-outside");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        var targetPath = outsideScope.WriteFile("SKILL.md", "# Outside\n");
        if (!TestSymbolicLinks.TryCreateFile(Path.Combine(skillDirectory.Value, "SKILL.md"), targetPath))
        {
            return;
        }

        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsDirectorySymlinkBeforeReadingLinkedFiles ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-directory-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "file-set-directory-symlink-outside");
        var skillDirectory = AbsolutePath.Parse(scope.CreateDirectory("sample-skill"));
        scope.WriteFile(Path.Combine("sample-skill", "SKILL.md"), "# Skill\n");
        outsideScope.WriteFile("secret.md", "# Outside\n");
        if (!TestSymbolicLinks.TryCreateDirectory(Path.Combine(skillDirectory.Value, "outside"), outsideScope.FullPath))
        {
            return;
        }

        var verifier = new SkillInstalledFileSetVerifier();

        var result = await verifier.VerifyAsync(
            skillDirectory,
            [new PackageTextFile(PackageRelativePath.Parse("SKILL.md"), "# Skill\n")],
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
