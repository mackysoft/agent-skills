using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Validation;

public sealed class SkillInstalledPackageIntegrityVerifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsUnsupportedSchemaVersionManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "integrity-schema-version-one");
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var installService = SkillTestData.CreateInstallService();
        var install = await installService.InstallAsync(
            package.Manifest.CatalogId,
            [package],
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, package.Manifest.SkillName.Value);
        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var unsupportedSchemaVersionText = File.ReadAllText(manifestPath)
            .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0", StringComparison.Ordinal);
        File.WriteAllText(manifestPath, unsupportedSchemaVersionText);
        var verifier = SkillTestData.CreateInstalledPackageIntegrityVerifier(SkillTestData.CreateDefaultHostAdapterSet());

        var result = await verifier.VerifyAsync(skillDirectory, SkillHostKind.OpenAi, CancellationToken.None);

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

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "integrity-reference-directory-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "integrity-reference-directory-symlink-outside");
        var hostAdapters = SkillTestData.CreateDefaultHostAdapterSet();
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var materializedResult = SkillTestData.CreateMaterializationService().Materialize(package, SkillHostKind.OpenAi);
        Assert.True(materializedResult.IsSuccess, materializedResult.Failure?.Message);
        var skillDirectory = scope.CreateDirectory(package.Manifest.SkillName.Value);
        foreach (var file in materializedResult.Value!.Files)
        {
            scope.WriteFile(Path.Combine(package.Manifest.SkillName.Value, file.RelativePath), file.Content);
        }

        const string outsideFileName = "outside-secret.md";
        outsideScope.WriteFile(outsideFileName, "# Outside\n");
        if (!TestSymbolicLinks.TryCreateDirectory(Path.Combine(skillDirectory, "references", "outside"), outsideScope.FullPath))
        {
            return;
        }

        var verifier = SkillTestData.CreateInstalledPackageIntegrityVerifier(hostAdapters);

        var result = await verifier.VerifyAsync(skillDirectory, SkillHostKind.OpenAi, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.Contains("references/outside", result.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(outsideFileName, result.Failure.Message, StringComparison.Ordinal);
    }
}
