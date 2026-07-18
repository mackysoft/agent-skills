using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var created = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);
        var noOp = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName.Value, "SKILL.md")), package.Manifest.SkillName.Value);
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot, package.Manifest.SkillName.Value, "agent-skill.json")), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCompatibleFlatTargetWithoutCreatingPreferredTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-compatible-flat-target");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installedPackage = packages[0];
        var updatedPackage = SkillTestData.CreatePackageWithSkillBundleVersion(
            SkillTestData.CreatePackageWithUpdatedBody(installedPackage),
            installedPackage.Manifest.SkillBundleVersion + 1);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var flatRequest = new SkillInstallRequest(
            SkillHostKind.OpenAi,
            SkillScopeKind.Project,
            scope.FullPath,
            Path.Combine(".agents", "skills"));
        var flatInstall = await installService.InstallAsync(
            installedPackage.Manifest.CatalogId,
            [installedPackage],
            flatRequest,
            CancellationToken.None);
        Assert.True(flatInstall.IsSuccess, flatInstall.Failure?.Message);

        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var updated = await updateService.UpdateAsync(
            new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request),
            CancellationToken.None);
        var noOp = await updateService.UpdateAsync(
            new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request),
            CancellationToken.None);

        var preferredTargetRoot = Path.Combine(scope.FullPath, ".agents", "skills", updatedPackage.Manifest.CatalogId.Value);
        Assert.True(updated.IsSuccess, updated.Failure?.Message);
        Assert.Equal(flatInstall.Value!.TargetRoot, updated.Value!.TargetRoot);
        var updatedAction = Assert.Single(updated.Value.Actions);
        Assert.Equal(SkillUpdateActionKind.Updated, updatedAction.ActionKind);
        Assert.Equal(SkillTargetStateKind.CleanOutdated, updatedAction.TargetState!.Kind);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.Equal(flatInstall.Value.TargetRoot, noOp.Value!.TargetRoot);
        Assert.Equal(SkillUpdateActionKind.NoOp, Assert.Single(noOp.Value.Actions).ActionKind);
        Assert.False(Directory.Exists(preferredTargetRoot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value).ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName.Value != packages[0].Manifest.SkillName.Value), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));

        var expectedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json"));
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedOpenAiMetadata(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName.Value != packages[0].Manifest.SkillName.Value), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        var expectedMetadata = updatedPackages[0].Files.Single(static file => file.RelativePath == "agents/openai.yaml").Content;
        var actualMetadata = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"));
        Assert.Equal(expectedMetadata, actualMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
                force: true),
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
        var targetRoot = scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value));
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
                force: true),
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.WriteAllText(skillPath, File.ReadAllText(skillPath).Replace("description:", "description: Drifted", StringComparison.Ordinal));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"), "\n# Drifted metadata.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        SkillTestData.TamperManifestDigest(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind);
        Assert.Equal(SkillTargetStateKind.Missing, action.TargetState!.Kind);
        Assert.NotEmpty(action.Diffs!);
        Assert.NotNull(action.FileChanges);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunUpdatesCleanOutdatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Equal(SkillTargetStateKind.CleanOutdated, action.TargetState!.Kind);
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = File.ReadAllText(skillPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.Equal(SkillTargetStateKind.CommonContentDrift, action.TargetState!.Kind);
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(aheadPackage.Manifest.CatalogId, [aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, aheadPackage.Manifest.SkillName.Value, "agent-skill.json");
        var aheadManifest = File.ReadAllText(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.BlockedVersionAhead, action.ActionKind);
        Assert.Equal(SkillBlockedReason.InstalledVersionAhead, action.BlockedReason);
        Assert.Equal(SkillTargetStateKind.VersionAhead, action.TargetState!.Kind);
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(aheadPackage.Manifest.CatalogId, [aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        var expectedManifest = packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json"));
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, force: true), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, force: true), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal(new[] { "local-note.md" }, action.FileChanges!.RemovedFiles);
        Assert.False(File.Exists(extraFile));
        Assert.True(File.Exists(Path.Combine(result.Value!.TargetRoot, packages[1].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksUnmanagedTargetEvenWithForce ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-dry-run-unmanaged-force");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                [packages[0]],
                new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath),
                dryRun: true,
                force: true,
                printDiff: true),
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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[1].Manifest.CatalogId.Value, packages[1].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage, packages[1]], request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            skillDirectory => File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected after planning.\n")));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage, packages[1]], request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            targetDirectory => File.WriteAllText(Path.Combine(targetDirectory, "late-local-note.md"), "# Late local note\n")));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateDirectory = Path.Combine(skillDirectory, "late-local-notes");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            targetDirectory => Directory.CreateDirectory(Path.Combine(targetDirectory, "late-local-notes"))));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(lateDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalEmptyDirectoryBeforeReplacingOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "update-local-empty-directory");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "local-notes");
        Directory.CreateDirectory(localDirectory);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

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
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
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

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

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
            packages[0].Manifest.CatalogId,
            packages,
            new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
                force: true),
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
        var openAiRequest = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var claudeRequest = new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath);
        var openAi = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, openAiRequest, CancellationToken.None);
        var claude = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, claudeRequest, CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, openAiRequest), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var updatedManifest = updatedPackages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        var oldManifest = packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
        Assert.Equal(updatedManifest, File.ReadAllText(Path.Combine(openAi.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
        Assert.Equal(oldManifest, File.ReadAllText(Path.Combine(claude.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
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
