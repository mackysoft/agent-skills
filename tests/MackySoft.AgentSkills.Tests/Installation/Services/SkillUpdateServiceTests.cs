using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Services;

public sealed class SkillUpdateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_CreatesThenNoOps_WhenTargetIsCurrent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-create-noop");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var created = await service.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);
        var noOp = await service.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName, "SKILL.md")), package.Manifest.SkillName);
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName, "agent-skill.json")), package.Manifest.SkillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName).ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName != packages[0].Manifest.SkillName), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));

        var expectedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json"));
        Assert.Equal(expectedManifest, actualManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage_WhenOnlyOpenAiMetadataChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-openai-metadata-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedOpenAiMetadata(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName != packages[0].Manifest.SkillName), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        var expectedMetadata = updatedPackages[0].Files.Single(static file => file.RelativePath == "agents/openai.yaml").Content;
        var actualMetadata = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName, "agents", "openai.yaml"));
        Assert.Equal(expectedMetadata, actualMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal("# Existing\n", File.ReadAllText(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md"), "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsFrontmatterDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-frontmatter-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.WriteAllText(skillPath, File.ReadAllText(skillPath).Replace("description:", "description: Drifted", StringComparison.Ordinal));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsHostArtifactDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-host-artifact-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agents", "openai.yaml"), "\n# Drifted metadata.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestOnlyLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-manifest-only-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestDigestOnlyLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-manifest-digest-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json");
        SkillTestData.TamperManifestDigest(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestOnlyLocalModification_WhenInstalledPackageIsOtherwiseOutdated ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-outdated-manifest-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunCreatesPlanWithoutWritingFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-create");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var result = await service.UpdateAsync(new SkillUpdateInput([packages[0]], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.Missing), action.TargetState!.Kind);
        Assert.NotEmpty(action.Diffs!);
        Assert.NotNull(action.FileChanges);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunUpdatesCleanOutdatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.CleanOutdated), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetOutdated, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.NotNull(action.FileChanges);
        Assert.Contains("SKILL.md", action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksLocalModificationWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = File.ReadAllText(skillPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUpdateActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.CommonContentDrift), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(modifiedSkill, File.ReadAllText(skillPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksVersionAheadWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion + 1);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, aheadPackage.Manifest.SkillName, "agent-skill.json");
        var aheadManifest = File.ReadAllText(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0]], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.BlockedVersionAhead, action.ActionKind);
        Assert.Equal(SkillBlockedReason.InstalledVersionAhead, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.VersionAhead), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetVersionAhead, action.TargetState.Code);
        Assert.Equal(aheadPackage.Manifest.SkillBundleVersion, action.TargetState.InstalledSkillBundleVersion);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion, action.TargetState.BundledSkillBundleVersion);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(aheadManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceOverwritesVersionAheadPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-force-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion + 1);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        var expectedManifest = packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json"));
        Assert.Equal(expectedManifest, actualManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceOverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages, request, Force: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName == packages[0].Manifest.SkillName);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(new[] { "SKILL.md" }, action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.DoesNotContain("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunWithForceReportsRemovedFileWithoutWritingOrDiffs ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0]], request, DryRun: true, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Empty(action.Diffs!);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal(new[] { "local-note.md" }, action.FileChanges!.RemovedFiles);
        Assert.True(File.Exists(extraFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunWithForceReportsReplacedFileWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0]], request, DryRun: true, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Equal(new[] { "SKILL.md" }, action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.Contains("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceRemovesExtraFileAndReportsIt ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal(new[] { "local-note.md" }, action.FileChanges!.RemovedFiles);
        Assert.False(File.Exists(extraFile));
        Assert.True(File.Exists(Path.Combine(result.Value!.TargetRoot, packages[1].Manifest.SkillName, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksUnmanagedTargetEvenWithForce ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-unmanaged-force");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName, "SKILL.md"), "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                DryRun: true,
                Force: true,
                PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.BlockedUnmanaged, action.ActionKind);
        Assert.Equal(SkillBlockedReason.UnmanagedTarget, action.BlockedReason);
        Assert.Empty(action.Diffs!);
        Assert.Null(action.FileChanges);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenWriterFails_ReturnsWriteFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-writer-failure");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService(new FailingPackageWriter());
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenLaterTargetIsUnmanaged_DoesNotUpdateEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-plan-before-write-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[1].Manifest.SkillName, "SKILL.md"), "# Existing\n");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage, packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenTargetChangesAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.AppendAllText(skillPath, "\nInjected after planning.\n"));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([updatedPackage, secondPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
        var skillText = File.ReadAllText(skillPath);
        Assert.Contains("Injected after planning.", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("Fixture update.", skillText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceWhenTargetChangesAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-force-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.WriteAllText(lateFile, "# Late local note\n"));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0], secondPackage], request, Force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(lateFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceWhenEmptyDirectoryAppearsAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-force-empty-directory-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateDirectory = Path.Combine(skillDirectory, "late-local-notes");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            Directory.CreateDirectory(lateDirectory));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([packages[0], secondPackage], request, Force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(lateDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenLaterTargetChangesAfterPlanning_ReturnsFailureWithoutUpdatingEarlierTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-later-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var firstSkillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "SKILL.md");
        var secondSkillPath = Path.Combine(install.Value.TargetRoot, packages[1].Manifest.SkillName, "SKILL.md");
        var firstUpdatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var secondUpdatedPackage = SkillTestData.WithFileEnumerationCallback(
            SkillTestData.CreatePackageWithUpdatedBody(packages[1]),
            () => File.AppendAllText(secondSkillPath, "\nInjected after planning.\n"));

        var result = await updateService.UpdateAsync(new SkillUpdateInput([firstUpdatedPackage, secondUpdatedPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
        Assert.DoesNotContain("Fixture update.", File.ReadAllText(firstSkillPath), StringComparison.Ordinal);
        Assert.Contains("Injected after planning.", File.ReadAllText(secondSkillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalEmptyDirectoryBeforeReplacingOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-local-empty-directory");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName, "local-notes");
        Directory.CreateDirectory(localDirectory);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalDirectorySymlinkBeforeReplacingOutdatedPackage ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-local-directory-symlink");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName);
        var allowedDirectory = Path.Combine(skillDirectory, "agents");
        Assert.True(Directory.Exists(allowedDirectory));
        var localDirectoryLink = Path.Combine(skillDirectory, "local-agents");
        try
        {
            Directory.CreateSymbolicLink(localDirectoryLink, allowedDirectory);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectoryLink));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var install = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(
            new SkillUpdateInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-other-host");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var openAiRequest = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var claudeRequest = new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var openAi = await installService.InstallAsync(packages, openAiRequest, CancellationToken.None);
        var claude = await installService.InstallAsync(packages, claudeRequest, CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages, openAiRequest), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var updatedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var oldManifest = packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        Assert.Equal(updatedManifest, File.ReadAllText(Path.Combine(openAi.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json")));
        Assert.Equal(oldManifest, File.ReadAllText(Path.Combine(claude.Value!.TargetRoot, packages[0].Manifest.SkillName, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateUpdateService();

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                [package],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    private sealed class FailingPackageWriter : ISkillMaterializedPackageWriter
    {
        public ValueTask<SkillOperationResult<bool>> WriteAsync (
            string targetRoot,
            string skillDirectory,
            SkillMaterializedPackage materializedPackage,
            SkillMaterializedPackageWriteMode writeMode,
            Func<string, CancellationToken, ValueTask<SkillOperationResult<bool>>>? precondition,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                "Synthetic write failure."));
        }
    }
}
