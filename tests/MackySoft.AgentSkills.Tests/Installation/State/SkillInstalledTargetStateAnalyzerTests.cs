using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.State;

public sealed class SkillInstalledTargetStateAnalyzerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesManifestDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-manifest-drift");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        SkillTestData.TamperManifestDigest(Path.Combine(skillDirectory, "agent-skill.json"));

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.ManifestDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_PrioritizesManifestDriftOverFileSetDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-manifest-over-file-set");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        SkillTestData.TamperManifestDigest(Path.Combine(skillDirectory, "agent-skill.json"));
        File.WriteAllText(Path.Combine(skillDirectory, "references", "extra.md"), "# Extra\n");

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.ManifestDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, state.Failure!.Code);
        Assert.Null(state.FileSet);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesCommonContentDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-content-drift");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.CommonContentDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesFrontmatterDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-frontmatter-drift");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var skillText = File.ReadAllText(skillPath);
        File.WriteAllText(skillPath, skillText.Replace("description:", "description: Drifted", StringComparison.Ordinal));

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.FrontmatterDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesHostArtifactDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-host-artifact-drift");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        File.AppendAllText(Path.Combine(skillDirectory, "agents", "openai.yaml"), "\n# Drifted metadata.\n");

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.HostArtifactDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesMissingReferenceAsFileSetDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-missing-reference");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var package = packages[0];
        var skillDirectory = GetSkillDirectory(targetRoot, package);
        var referencePath = package.Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath;
        File.Delete(Path.Combine(skillDirectory, referencePath));

        var state = await AnalyzeOpenAiAsync(package, skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.FileSetDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, state.Failure!.Code);
        Assert.Contains(referencePath, state.FileSet!.MissingFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesExtraDirectoryAsFileSetDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-extra-directory");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        Directory.CreateDirectory(Path.Combine(skillDirectory, "local-notes"));

        var state = await AnalyzeOpenAiAsync(packages[0], skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.FileSetDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, state.Failure!.Code);
        Assert.Contains("local-notes", state.FileSet!.ExtraDirectories);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesExtraReferenceAsFileSetDrift_WhenCanonicalPackageIsOutdated ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-extra-reference-outdated");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var skillDirectory = GetSkillDirectory(targetRoot, packages[0]);
        var extraReferencePath = Path.Combine(skillDirectory, "references", "extra.md");
        File.WriteAllText(extraReferencePath, "# Extra\n");

        var state = await AnalyzeOpenAiAsync(updatedPackage, skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.FileSetDrift, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, state.Failure!.Code);
        Assert.Contains("references/extra.md", state.FileSet!.ExtraFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesCleanOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-clean-outdated");
        var (packages, targetRoot) = await InstallOpenAiAsync(scope);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var state = await AnalyzeOpenAiAsync(updatedPackage, GetSkillDirectory(targetRoot, packages[0]));

        Assert.Equal(SkillInstalledTargetStateKind.CleanOutdated, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetOutdated, state.Failure!.Code);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion, state.InstalledSkillBundleVersion);
        Assert.Equal(updatedPackage.Manifest.SkillBundleVersion, state.BundledSkillBundleVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesVersionAheadPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion + 1);
        var installService = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var state = await AnalyzeOpenAiAsync(packages[0], GetSkillDirectory(install.Value!.TargetRoot, packages[0]));

        Assert.Equal(SkillInstalledTargetStateKind.VersionAhead, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetVersionAhead, state.Failure!.Code);
        Assert.Equal(aheadPackage.Manifest.SkillBundleVersion, state.InstalledSkillBundleVersion);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion, state.BundledSkillBundleVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesHostConflict ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);

        var state = await AnalyzeAsync(packages[0], GetSkillDirectory(installResult.Value!.TargetRoot, packages[0]), OpenAiSkillHostAdapter.HostKey);

        Assert.Equal(SkillInstalledTargetStateKind.HostConflict, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var package = packages[0];
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", package.Manifest.SkillName));
        var serializer = new SkillManifestJsonSerializer();
        var manifest = SkillTestData.WithComputedManifestDigest(package.Manifest with
        {
            SkillName = "different-skill",
        });
        File.WriteAllText(Path.Combine(skillDirectory, "agent-skill.json"), serializer.Serialize(manifest));

        var state = await AnalyzeOpenAiAsync(package, Path.Combine(targetRoot, package.Manifest.SkillName));

        Assert.Equal(SkillInstalledTargetStateKind.NameCollision, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, state.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AnalyzeAsync_ClassifiesUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "state-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var package = packages[0];
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", package.Manifest.SkillName));
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), "# Existing\n");

        var state = await AnalyzeOpenAiAsync(package, skillDirectory);

        Assert.Equal(SkillInstalledTargetStateKind.Unmanaged, state.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, state.Failure!.Code);
    }

    private static async Task<(IReadOnlyList<CanonicalSkillPackage> Packages, string TargetRoot)> InstallOpenAiAsync (TestDirectoryScope scope)
    {
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var installResult = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(installResult.IsSuccess, installResult.Failure?.Message);
        return (packages, installResult.Value!.TargetRoot);
    }

    private static async Task<SkillInstalledTargetState> AnalyzeOpenAiAsync (
        CanonicalSkillPackage package,
        string skillDirectory)
    {
        return await AnalyzeAsync(package, skillDirectory, OpenAiSkillHostAdapter.HostKey);
    }

    private static async Task<SkillInstalledTargetState> AnalyzeAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host)
    {
        var analyzer = SkillTestData.CreateTargetStateAnalyzer();
        var result = await analyzer.AnalyzeAsync(package, skillDirectory, host, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static string GetSkillDirectory (
        string targetRoot,
        CanonicalSkillPackage package)
    {
        return Path.Combine(targetRoot, package.Manifest.SkillName);
    }
}
