using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Doctor;

public sealed class SkillDoctorServiceTests
{
    public enum SharedDriftCase
    {
        Manifest,
        CommonContent,
        FileSet,
        Frontmatter,
        HostArtifact,
        HostConflict,
        NameCollision,
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReturnsHealthy_WhenTargetMatchesHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-healthy");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.True(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "SKILL_DOCTOR_OK");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsMissingSkillDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-missing");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var doctor = SkillTestData.CreateDoctorService();
        var targetRoot = scope.CreateDirectory(".agents/skills");

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetUnmanaged);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsHostConflict_WhenTargetWasMaterializedForDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetHostConflict);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(SharedDriftCase.Manifest)]
    [InlineData(SharedDriftCase.CommonContent)]
    [InlineData(SharedDriftCase.FileSet)]
    [InlineData(SharedDriftCase.Frontmatter)]
    [InlineData(SharedDriftCase.HostArtifact)]
    [InlineData(SharedDriftCase.HostConflict)]
    [InlineData(SharedDriftCase.NameCollision)]
    public async Task DiagnoseAsync_UsesSameDriftCodeAsTargetStateAnalyzer (SharedDriftCase driftCase)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", $"doctor-shared-{driftCase}");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var package = packages[0];
        var host = SkillHostKind.OpenAi;
        var (targetRoot, skillDirectory) = await PrepareSharedDriftCaseAsync(scope, packages, driftCase);

        var stateResult = await SkillTestData.CreateTargetStateAnalyzer().AnalyzeAsync(package, skillDirectory, host, CancellationToken.None);
        Assert.True(stateResult.IsSuccess, stateResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, host, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.SkillName == package.Manifest.SkillName);
        Assert.Equal(stateResult.Value!.Failure!.Code, diagnostic.Code);
        Assert.Equal(GetExpectedSharedDriftCode(driftCase), diagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsCommonContentDrift_WhenInstalledSkillBodyChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-body-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetContentDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsFileSetMismatch_WhenInstalledSkillBodyIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-body-missing");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.Delete(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"));
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetFileSetMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsManifestDrift_WhenInstalledManifestArtifactDigestChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-manifest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var manifestPath = Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalDigest = packages[0].Manifest.HostArtifacts
            .Single(static artifact => artifact.Host == SkillHostKind.OpenAi)
            .Digest!;
        var manifestText = File.ReadAllText(manifestPath).Replace(originalDigest.ToString(), new string('f', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetManifestDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsManifestDrift_WhenInstalledManifestDigestChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-manifest-digest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var manifestPath = Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        SkillTestData.TamperManifestDigest(manifestPath);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetManifestDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsFileSetMismatch_WhenInstalledReferenceIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-reference-missing");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var referencePath = Path.Combine(
            installResult.Value!.TargetRoot,
            packages[0].Manifest.SkillName.Value,
            packages[0].Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath);
        File.Delete(referencePath);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetFileSetMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsFileSetMismatch_WhenInstalledEmptyDirectoryIsUnexpected ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-extra-empty-directory");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        Directory.CreateDirectory(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "empty"));
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetFileSetMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsHostArtifactMismatch_WhenOpenAiMetadataChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-openai-metadata-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.AppendAllText(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"), "\n# Drifted metadata.\n");
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsHostArtifactMismatch_WhenOpenAiMetadataIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-openai-metadata-missing");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        File.Delete(Path.Combine(installResult.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"));
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetHostArtifactDigestMismatch);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsOutdated_WhenInstalledPackageIsCleanOlderVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-outdated");
        var installedPackages = await SkillTestData.GenerateFixturePackagesAsync();
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(installedPackages[0]);
        var currentPackages = SkillTestData.ReplacePackage(installedPackages, updatedPackage);
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            installedPackages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var stateResult = await SkillTestData.CreateTargetStateAnalyzer().AnalyzeAsync(
            updatedPackage,
            Path.Combine(installResult.Value!.TargetRoot, updatedPackage.Manifest.SkillName.Value),
            SkillHostKind.OpenAi,
            CancellationToken.None);
        Assert.True(stateResult.IsSuccess, stateResult.Failure?.Message);

        var result = await doctor.DiagnoseAsync(currentPackages, SkillHostKind.OpenAi, installResult.Value.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.SkillName == updatedPackage.Manifest.SkillName);
        Assert.Equal(stateResult.Value!.Failure!.Code, diagnostic.Code);
        Assert.Equal(stateResult.Value.Failure.Message, diagnostic.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsOutdated_WhenOnlyOpenAiMetadataChangedInCanonicalPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-openai-metadata-outdated");
        var installedPackages = await SkillTestData.GenerateFixturePackagesAsync();
        var currentPackages = SkillTestData.ReplacePackage(
            installedPackages,
            SkillTestData.CreatePackageWithUpdatedOpenAiMetadata(installedPackages[0]));
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            installedPackages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(currentPackages, SkillHostKind.OpenAi, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetOutdated);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsOutdated_WhenOnlyOpenAiMetadataChangedForClaudePackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-claude-openai-metadata-outdated");
        var installedPackages = await SkillTestData.GenerateFixturePackagesAsync();
        var currentPackages = SkillTestData.ReplacePackage(
            installedPackages,
            SkillTestData.CreatePackageWithUpdatedOpenAiMetadata(installedPackages[0]));
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            installedPackages,
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(currentPackages, SkillHostKind.Claude, installResult.Value!.TargetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.InstallTargetOutdated);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsInvalidManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-invalid-manifest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "agent-skill.json"), "{}");
        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.ManifestInvalid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsPathUnsafe_WhenManifestSymlinkEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-manifest-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value));
        var outsideManifest = outsideScope.WriteFile("agent-skill.json", packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(targetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json"), outsideManifest);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var doctor = SkillTestData.CreateDoctorService();

        var result = await doctor.DiagnoseAsync(packages, SkillHostKind.OpenAi, targetRoot, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == SkillFailureCodes.PathUnsafe);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_RejectsUndefinedHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "doctor-unsupported-host");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var doctor = SkillTestData.CreateDoctorService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await doctor.DiagnoseAsync(packages, (SkillHostKind)42, scope.FullPath, CancellationToken.None));
    }

    private static async Task<(string TargetRoot, string SkillDirectory)> PrepareSharedDriftCaseAsync (
        TestDirectoryScope scope,
        IReadOnlyList<MackySoft.AgentSkills.Packaging.Canonical.CanonicalSkillPackage> packages,
        SharedDriftCase driftCase)
    {
        var package = packages[0];
        if (driftCase == SharedDriftCase.HostConflict)
        {
            var claudeInstall = await SkillTestData.CreateInstallService().InstallAsync(
                packages,
                new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
                CancellationToken.None);
            Assert.True(claudeInstall.IsSuccess, claudeInstall.Failure?.Message);
            return (claudeInstall.Value!.TargetRoot, Path.Combine(claudeInstall.Value.TargetRoot, package.Manifest.SkillName.Value));
        }

        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = Path.Combine(targetRoot, package.Manifest.SkillName.Value);
        if (driftCase == SharedDriftCase.NameCollision)
        {
            Directory.CreateDirectory(skillDirectory);
            var manifest = SkillTestData.WithComputedManifestDigest(SkillTestData.CopyManifest(
                package.Manifest,
                skillName: new SkillName("different-skill")));
            File.WriteAllText(Path.Combine(skillDirectory, "agent-skill.json"), new SkillManifestJsonSerializer().Serialize(manifest));
            return (targetRoot, skillDirectory);
        }

        var installResult = await SkillTestData.CreateInstallService().InstallAsync(
            packages,
            new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        targetRoot = installResult.Value!.TargetRoot;
        skillDirectory = Path.Combine(targetRoot, package.Manifest.SkillName.Value);

        switch (driftCase)
        {
            case SharedDriftCase.Manifest:
                SkillTestData.TamperManifestDigest(Path.Combine(skillDirectory, "agent-skill.json"));
                break;
            case SharedDriftCase.CommonContent:
                File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");
                break;
            case SharedDriftCase.FileSet:
                File.Delete(Path.Combine(
                    skillDirectory,
                    package.Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath));
                break;
            case SharedDriftCase.Frontmatter:
                var skillPath = Path.Combine(skillDirectory, "SKILL.md");
                File.WriteAllText(skillPath, File.ReadAllText(skillPath).Replace("description:", "description: Drifted", StringComparison.Ordinal));
                break;
            case SharedDriftCase.HostArtifact:
                File.AppendAllText(Path.Combine(skillDirectory, "agents", "openai.yaml"), "\n# Drifted metadata.\n");
                break;
            case SharedDriftCase.HostConflict:
            case SharedDriftCase.NameCollision:
                throw new ArgumentOutOfRangeException(nameof(driftCase), driftCase, "Case was handled before installing OpenAI target.");
            default:
                throw new ArgumentOutOfRangeException(nameof(driftCase), driftCase, "Unsupported shared drift case.");
        }

        return (targetRoot, skillDirectory);
    }

    private static SkillFailureCode GetExpectedSharedDriftCode (SharedDriftCase driftCase)
    {
        return driftCase switch
        {
            SharedDriftCase.Manifest => SkillFailureCodes.InstallTargetManifestDigestMismatch,
            SharedDriftCase.CommonContent => SkillFailureCodes.InstallTargetContentDigestMismatch,
            SharedDriftCase.FileSet => SkillFailureCodes.InstallTargetFileSetMismatch,
            SharedDriftCase.Frontmatter => SkillFailureCodes.InstallTargetFrontmatterDigestMismatch,
            SharedDriftCase.HostArtifact => SkillFailureCodes.InstallTargetHostArtifactDigestMismatch,
            SharedDriftCase.HostConflict => SkillFailureCodes.InstallTargetHostConflict,
            SharedDriftCase.NameCollision => SkillFailureCodes.InstallTargetNameCollision,
            _ => throw new ArgumentOutOfRangeException(nameof(driftCase), driftCase, "Unsupported shared drift case."),
        };
    }
}
