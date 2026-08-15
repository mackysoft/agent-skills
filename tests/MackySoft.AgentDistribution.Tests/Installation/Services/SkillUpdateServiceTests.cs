using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Installation.Services;

public sealed class SkillUpdateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_ReconcilesAddedChangedAndRemovedScripts ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-scripts");
        var basePackage = (await SkillTestData.GenerateFixturePackagesAsync())[0];
        var addedPackage = SkillTestData.CreatePackageWithScripts(
            basePackage,
            [new PackageTextFile(PackageRelativePath.Parse("scripts/collect.sh"), "#!/bin/sh\necho first\n")],
            basePackage.Manifest.SkillBundleVersion.Next().Value);
        var changedPackage = SkillTestData.CreatePackageWithScripts(
            basePackage,
            [new PackageTextFile(PackageRelativePath.Parse("scripts/collect.sh"), "#!/bin/sh\necho changed\n")],
            addedPackage.Manifest.SkillBundleVersion.Next().Value);
        var removedPackage = SkillTestData.CreatePackageWithSkillBundleVersion(
            basePackage,
            changedPackage.Manifest.SkillBundleVersion.Next().Value);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);

        var install = await installService.InstallAsync(basePackage.Manifest.CatalogId, [basePackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var added = await updateService.UpdateAsync(new SkillUpdateInput(addedPackage.Manifest.CatalogId, [addedPackage], request), CancellationToken.None);
        Assert.True(added.IsSuccess, added.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, Assert.Single(added.Value!.Actions).ActionKind);
        var scriptPath = Path.Combine(added.Value.TargetRoot.Value, basePackage.Manifest.SkillName.Value, "scripts", "collect.sh");
        Assert.Equal("#!/bin/sh\necho first\n", File.ReadAllText(scriptPath));

        var changed = await updateService.UpdateAsync(new SkillUpdateInput(changedPackage.Manifest.CatalogId, [changedPackage], request), CancellationToken.None);
        Assert.True(changed.IsSuccess, changed.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, Assert.Single(changed.Value!.Actions).ActionKind);
        Assert.Equal("#!/bin/sh\necho changed\n", File.ReadAllText(scriptPath));

        var removed = await updateService.UpdateAsync(new SkillUpdateInput(removedPackage.Manifest.CatalogId, [removedPackage], request), CancellationToken.None);

        Assert.True(removed.IsSuccess, removed.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, Assert.Single(removed.Value!.Actions).ActionKind);
        Assert.False(File.Exists(scriptPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(scriptPath)!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_CreatesThenNoOps_WhenTargetIsCurrent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-create-noop");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);

        var created = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);
        var noOp = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot.Value, package.Manifest.SkillName.Value, "SKILL.md")), package.Manifest.SkillName.Value);
            Assert.True(File.Exists(Path.Combine(created.Value.TargetRoot.Value, package.Manifest.SkillName.Value, "agent-skill.json")), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCompatibleFlatTargetWithoutCreatingPreferredTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-compatible-flat-target");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installedPackage = packages[0];
        var updatedPackage = SkillTestData.CreatePackageWithSkillBundleVersion(
            SkillTestData.CreatePackageWithUpdatedBody(installedPackage),
            installedPackage.Manifest.SkillBundleVersion.Next().Value);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var flatRequest = SkillTestData.CreateInstallRequest(
            HostKind.Codex,
            SkillScopeKind.Project,
            scope.FullPath,
            Path.Combine(".agents", "skills"));
        var flatInstall = await installService.InstallAsync(
            installedPackage.Manifest.CatalogId,
            [installedPackage],
            flatRequest,
            CancellationToken.None);
        Assert.True(flatInstall.IsSuccess, flatInstall.Failure?.Message);

        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var updated = await updateService.UpdateAsync(
            new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request),
            CancellationToken.None);
        var noOp = await updateService.UpdateAsync(
            new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request),
            CancellationToken.None);

        var preferredTargetRoot = Path.Combine(scope.FullPath, ".agents", "skills", updatedPackage.Manifest.CatalogId.Value);
        Assert.True(updated.IsSuccess, updated.Failure?.Message);
        Assert.Equal(flatInstall.Value!.TargetRoot.Value, updated.Value!.TargetRoot.Value);
        var updatedAction = Assert.Single(updated.Value.Actions);
        Assert.Equal(SkillUpdateActionKind.Updated, updatedAction.ActionKind);
        Assert.Equal(SkillTargetStateKind.CleanOutdated, updatedAction.TargetState!.Kind);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.Equal(flatInstall.Value.TargetRoot.Value, noOp.Value!.TargetRoot.Value);
        Assert.Equal(SkillUpdateActionKind.NoOp, Assert.Single(noOp.Value.Actions).ActionKind);
        Assert.False(Directory.Exists(preferredTargetRoot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage_WhenBundleVersionAdvances ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUpdateActionKind.Updated, result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value).ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName.Value != packages[0].Manifest.SkillName.Value), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));

        var manifestPath = PackageRelativePath.Parse("agent-skill.json");
        var expectedManifest = updatedPackages[0].Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json"));
        Assert.Equal(expectedManifest, actualManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage_WhenCanonicalContentChangesAtSameVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-outdated-same-version");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            request,
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var installedPackage = packages[0];
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(
            installedPackage,
            installedPackage.Manifest.SkillBundleVersion.Value);
        var updatedPackages = SkillTestData.ReplacePackage(packages, updatedPackage);

        var result = await updateService.UpdateAsync(
            new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(item =>
            item.Identity.SkillName.Value == installedPackage.Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        var installedBody = File.ReadAllText(Path.Combine(result.Value.TargetRoot.Value, installedPackage.Manifest.SkillName.Value, "SKILL.md"));
        Assert.Contains("Fixture update.", installedBody, StringComparison.Ordinal);
        var manifestPath = PackageRelativePath.Parse("agent-skill.json");
        var expectedManifest = updatedPackage.Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        var installedManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot.Value, installedPackage.Manifest.SkillName.Value, "agent-skill.json"));
        Assert.Equal(expectedManifest, installedManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_UpdatesCleanOutdatedPackage_WhenOnlyOpenAiMetadataChanged ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-openai-metadata-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedOpenAiMetadata(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.All(result.Value.Actions.Where(action => action.Identity.SkillName.Value != packages[0].Manifest.SkillName.Value), static action =>
            Assert.Equal(SkillUpdateActionKind.NoOp, action.ActionKind));
        var metadataPath = PackageRelativePath.Parse("agents/openai.yaml");
        var expectedMetadata = updatedPackages[0].Files.Single(file => file.RelativePath.Equals(metadataPath)).Content;
        var actualMetadata = File.ReadAllText(Path.Combine(result.Value.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"));
        Assert.Equal(expectedMetadata, actualMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
                force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal("# Existing\n", File.ReadAllText(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var targetRoot = scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value));
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
                force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsFrontmatterDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-frontmatter-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.WriteAllText(skillPath, File.ReadAllText(skillPath).Replace("description:", "description: Drifted", StringComparison.Ordinal));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsHostArtifactDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-host-artifact-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"), "\n# Drifted metadata.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestOnlyLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-manifest-only-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestDigestOnlyLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-manifest-digest-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        SkillTestData.TamperManifestDigest(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsManifestOnlyLocalModification_WhenInstalledPackageIsOtherwiseOutdated ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-outdated-manifest-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(
            packages[0].Manifest.DisplayName,
            packages[0].Manifest.DisplayName + " Local",
            StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunCreatesPlanWithoutWritingFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-create");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);

        var result = await service.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Created, action.ActionKind);
        Assert.Equal(SkillTargetStateKind.Missing, action.TargetState!.Kind);
        Assert.NotEmpty(action.Diffs!);
        Assert.NotNull(action.FileChanges);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot.Value, packages[0].Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunUpdatesCleanOutdatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-outdated");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Equal(SkillTargetStateKind.CleanOutdated, action.TargetState!.Kind);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetOutdated, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.NotNull(action.FileChanges);
        Assert.Contains(PackageRelativePath.Parse("SKILL.md"), action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksLocalModificationWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = File.ReadAllText(skillPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.Equal(SkillTargetStateKind.CommonContentDrift, action.TargetState!.Kind);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(modifiedSkill, File.ReadAllText(skillPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksVersionAheadWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion.Next().Value);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(aheadPackage.Manifest.CatalogId, [aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, aheadPackage.Manifest.SkillName.Value, "agent-skill.json");
        var aheadManifest = File.ReadAllText(manifestPath);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.BlockedVersionAhead, action.ActionKind);
        Assert.Equal(SkillBlockedReason.InstalledVersionAhead, action.BlockedReason);
        Assert.Equal(SkillTargetStateKind.VersionAhead, action.TargetState!.Kind);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetVersionAhead, action.TargetState.Code);
        Assert.Equal(aheadPackage.Manifest.SkillBundleVersion.Value, action.TargetState.InstalledSkillBundleVersion);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion.Value, action.TargetState.BundledSkillBundleVersion);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(aheadManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceOverwritesVersionAheadPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-force-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion.Next().Value);
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(aheadPackage.Manifest.CatalogId, [aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        var manifestPath = PackageRelativePath.Parse("agent-skill.json");
        var expectedManifest = packages[0].Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        var actualManifest = File.ReadAllText(Path.Combine(result.Value.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json"));
        Assert.Equal(expectedManifest, actualManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceOverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true, printDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal([PackageRelativePath.Parse("SKILL.md")], action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.DoesNotContain("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunWithForceReportsRemovedFileWithoutWritingOrDiffs ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Empty(action.Diffs!);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal([PackageRelativePath.Parse("local-note.md")], action.FileChanges!.RemovedFiles);
        Assert.True(File.Exists(extraFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunWithForceReportsReplacedFileWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, dryRun: true, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Equal([PackageRelativePath.Parse("SKILL.md")], action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.Contains("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceRemovesExtraFileAndReportsIt ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, [packages[0]], request, force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUpdateActionKind.Updated, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal([PackageRelativePath.Parse("local-note.md")], action.FileChanges!.RemovedFiles);
        Assert.False(File.Exists(extraFile));
        Assert.True(File.Exists(Path.Combine(result.Value!.TargetRoot.Value, packages[1].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DryRunBlocksUnmanagedTargetEvenWithForce ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-dry-run-unmanaged-force");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUpdateService();
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[0].Manifest.CatalogId.Value, packages[0].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");

        var result = await service.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                [packages[0]],
                SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath),
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
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-writer-failure");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService(new FailingPackageWriter());
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenLaterTargetIsUnmanaged_DoesNotUpdateEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-plan-before-write-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalManifest = File.ReadAllText(manifestPath);
        var unmanagedPath = scope.WriteFile(
            Path.Combine(".agents", "skills", packages[1].Manifest.CatalogId.Value, packages[1].Manifest.SkillName.Value, "SKILL.md"),
            "# Existing\n");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage, packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal(originalManifest, File.ReadAllText(manifestPath));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenTargetChangesAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "SKILL.md");
        var updatedPackage = SkillTestData.CreatePackageWithUpdatedBody(packages[0]);
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            skillDirectory => File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected after planning.\n")));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackage.Manifest.CatalogId, [updatedPackage, packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
        var skillText = File.ReadAllText(skillPath);
        Assert.Contains("Injected after planning.", skillText, StringComparison.Ordinal);
        Assert.DoesNotContain("Fixture update.", skillText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceWhenTargetChangesAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-force-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            targetDirectory => File.WriteAllText(Path.Combine(targetDirectory, "late-local-note.md"), "# Late local note\n")));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(lateFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WithForceWhenEmptyDirectoryAppearsAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-force-empty-directory-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, [packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateDirectory = Path.Combine(skillDirectory, "late-local-notes");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var updateService = SkillTestData.CreateUpdateService(new MutatingSkillMaterializedPackageWriter(
            SkillTestData.CreatePackageWriter(),
            targetDirectory => Directory.CreateDirectory(Path.Combine(targetDirectory, "late-local-notes"))));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(packages[0].Manifest.CatalogId, packages, request, force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(lateDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsLocalEmptyDirectoryBeforeReplacingOutdatedPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-local-empty-directory");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "local-notes");
        Directory.CreateDirectory(localDirectory);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-local-directory-symlink");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var request = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value);
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
        Assert.Equal(AgentDistributionFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectoryLink));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var install = await installService.InstallAsync(
            packages[0].Manifest.CatalogId,
            packages,
            SkillTestData.CreateInstallRequest(HostKind.ClaudeCode, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await updateService.UpdateAsync(
            new SkillUpdateInput(
                packages[0].Manifest.CatalogId,
                packages,
                SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
                force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetHostConflict, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "update-other-host");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var updateService = SkillTestData.CreateUpdateService();
        var openAiRequest = SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, scope.FullPath);
        var claudeRequest = SkillTestData.CreateInstallRequest(HostKind.ClaudeCode, SkillScopeKind.Project, scope.FullPath);
        var openAi = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, openAiRequest, CancellationToken.None);
        var claude = await installService.InstallAsync(packages[0].Manifest.CatalogId, packages, claudeRequest, CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await updateService.UpdateAsync(new SkillUpdateInput(updatedPackages[0].Manifest.CatalogId, updatedPackages, openAiRequest), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var manifestPath = PackageRelativePath.Parse("agent-skill.json");
        var updatedManifest = updatedPackages[0].Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        var oldManifest = packages[0].Files.Single(file => file.RelativePath.Equals(manifestPath)).Content;
        Assert.Equal(updatedManifest, File.ReadAllText(Path.Combine(openAi.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
        Assert.Equal(oldManifest, File.ReadAllText(Path.Combine(claude.Value!.TargetRoot.Value, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
    }

    private sealed class FailingPackageWriter : ISkillMaterializedPackageWriter
    {
        public ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
            SkillMaterializedPackageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                "Synthetic write failure."));
        }
    }
}
