using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Validation;

public sealed class SkillInstalledPackageIntegrityVerifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsUnsupportedSchemaVersionManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "integrity-schema-version-one");
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var installService = SkillTestData.CreateInstallService();
        var install = await installService.InstallAsync(
            package.Manifest.CatalogId,
            [package],
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, package.Manifest.SkillName.Value);
        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var unsupportedSchemaVersionText = File.ReadAllText(manifestPath)
            .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0", StringComparison.Ordinal);
        File.WriteAllText(manifestPath, unsupportedSchemaVersionText);
        var verifier = SkillTestData.CreateInstalledPackageIntegrityVerifier();

        var result = await verifier.VerifyAsync(AbsolutePath.Parse(skillDirectory), HostKind.Codex, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsReferenceDirectorySymlinkWithoutLeakingTargetFilePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "integrity-reference-directory-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "integrity-reference-directory-symlink-outside");
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var materializedResult = SkillTestData.CreateMaterializationService().Materialize(package, HostKind.Codex);
        Assert.True(materializedResult.IsSuccess, materializedResult.Failure?.Message);
        var skillDirectory = scope.CreateDirectory(package.Manifest.SkillName.Value);
        foreach (var file in materializedResult.Value!.Files)
        {
            scope.WriteFile(Path.Combine(package.Manifest.SkillName.Value, file.RelativePath.Value), file.Content);
        }

        const string outsideFileName = "outside-secret.md";
        outsideScope.WriteFile(outsideFileName, "# Outside\n");
        Directory.CreateSymbolicLink(Path.Combine(skillDirectory, "references", "outside"), outsideScope.FullPath);

        var verifier = SkillTestData.CreateInstalledPackageIntegrityVerifier();

        var result = await verifier.VerifyAsync(AbsolutePath.Parse(skillDirectory), HostKind.Codex, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.Contains("references/outside", result.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(outsideFileName, result.Failure.Message, StringComparison.Ordinal);
    }
}
