using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Services;

public sealed class SkillPruneServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_DeletesCleanManagedSkillOutsideCurrentCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-delete-clean-orphan");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var currentCatalog = packages.Skip(1).ToArray();
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, currentCatalog, request),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var deleted = result.Value!.Actions.Single(action => action.Identity.SkillName == orphan.Manifest.SkillName);
        Assert.Equal(SkillPruneActionKind.Deleted, deleted.ActionKind);
        Assert.Equal(SkillTargetStateKind.RemovedFromCatalog, deleted.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetRemovedFromCatalog, deleted.TargetState.Code);
        Assert.NotNull(deleted.FileChanges);
        Assert.Contains("SKILL.md", deleted.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, orphan.Manifest.SkillName.Value)));
        foreach (var currentPackage in currentCatalog)
        {
            Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, currentPackage.Manifest.SkillName.Value)), currentPackage.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_DryRunReportsDeletionWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-dry-run-clean-orphan");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, DryRun: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.DryRun);
        var deleted = result.Value.Actions.Single(action => action.Identity.SkillName == orphan.Manifest.SkillName);
        Assert.Equal(SkillPruneActionKind.Deleted, deleted.ActionKind);
        Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, orphan.Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenSkillNamesAreSelected_PrunesOnlyMatchingTargets ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-selected-skill");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var selectedOrphan = packages[0];
        var unselectedOrphan = packages[1];
        var currentCatalog = packages.Skip(2).ToArray();
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(
                selectedOrphan.Manifest.CatalogId,
                currentCatalog,
                request,
                SelectedSkillNames: [selectedOrphan.Manifest.SkillName]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = Assert.Single(result.Value!.Actions);
        Assert.Equal(selectedOrphan.Manifest.SkillName, action.Identity.SkillName);
        Assert.Equal(SkillPruneActionKind.Deleted, action.ActionKind);
        Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, selectedOrphan.Manifest.SkillName.Value)));
        Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, unselectedOrphan.Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenCategoryIsSelected_SkipsUnmanagedTargetsWithoutKnownCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-category-skips-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/custom-skill/SKILL.md", "# Custom\n");
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(
                packages[0].Manifest.CatalogId,
                packages,
                request,
                SelectedCategories: [new SkillCategory("basic")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Empty(result.Value!.Actions);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "custom-skill")));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_SkipsCurrentCatalogSkills (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-skip-current");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(new SkillPruneInput(packages[0].Manifest.CatalogId, packages, request, Force: force), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillPruneActionKind.SkippedCurrent, action.ActionKind));
        foreach (var package in packages)
        {
            Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.Manifest.SkillName.Value)), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_BlocksLocalModificationWithoutForce ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-block-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, orphan.Manifest.SkillName.Value);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var blocked = result.Value!.Actions.Single(action => action.Identity.SkillName == orphan.Manifest.SkillName);
        Assert.Equal(SkillPruneActionKind.BlockedLocalModification, blocked.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, blocked.BlockedReason);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, blocked.TargetState!.Code);
        Assert.True(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WithForceDeletesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, orphan.Manifest.SkillName.Value);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var deleted = result.Value!.Actions.Single(action => action.Identity.SkillName == orphan.Manifest.SkillName);
        Assert.Equal(SkillPruneActionKind.Deleted, deleted.ActionKind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, deleted.TargetState!.Code);
        Assert.False(Directory.Exists(skillDirectory));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_SkipsForeignCatalog (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-skip-foreign-catalog");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var foreignPackage = CreatePackageWithCatalogId(packages[0], new SkillCatalogId("com.example.foreign-skills"));
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([foreignPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(new SkillPruneInput(packages[0].Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: force), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.SkippedForeignCatalog, action.ActionKind);
        Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, foreignPackage.Manifest.SkillName.Value)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_SkipsUnmanagedSkillDirectory (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-skip-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/custom-skill/SKILL.md", "# Custom\n");
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(new SkillPruneInput(packages[0].Manifest.CatalogId, packages, request, Force: force), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.SkippedUnmanaged, action.ActionKind);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, action.TargetState!.Code);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "custom-skill")));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_BlocksInvalidManifest (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-block-invalid-manifest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/invalid-skill/agent-skill.json", "{}");
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(new SkillPruneInput(packages[0].Manifest.CatalogId, packages, request, Force: force), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.BlockedManifestInvalid, action.ActionKind);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, action.TargetState!.Code);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "invalid-skill")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WithForceBlocksNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-force-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(packages[0].Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.BlockedNameCollision, action.ActionKind);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, action.TargetState!.Code);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, packages[0].Manifest.SkillName.Value)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WithForceBlocksHostConflict ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-force-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var claudeRequest = new SkillInstallRequest(SkillHostKind.Claude, SkillScopeKind.Project, scope.FullPath, "shared-skills");
        var openAiRequest = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath, "shared-skills");
        var install = await installService.InstallAsync([orphan], claudeRequest, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), openAiRequest, Force: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.BlockedHostConflict, action.ActionKind);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, action.TargetState!.Code);
        Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, orphan.Manifest.SkillName.Value)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_BlocksManifestDigestMismatch (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-force-manifest-digest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([orphan], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, orphan.Manifest.SkillName.Value);
        SkillTestData.TamperManifestDigest(Path.Combine(skillDirectory, "agent-skill.json"));

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: force),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillPruneActionKind.BlockedManifestInvalid, action.ActionKind);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, action.TargetState!.Code);
        Assert.True(Directory.Exists(skillDirectory));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_RejectsUnsafeSkillDirectoryNameWithManifest (bool force)
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-unsafe-directory-name");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        scope.WriteFile(".agents/skills/Invalid Skill/agent-skill.json", "{}");
        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(new SkillPruneInput(packages[0].Manifest.CatalogId, packages, request, Force: force), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "Invalid Skill")));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_RejectsTopLevelSkillDirectorySymlink (bool force)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-skill-directory-symlink");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var linkTarget = scope.CreateDirectory(".agents/skills/nested-target");
        var linkPath = Path.Combine(targetRoot, orphan.Manifest.SkillName.Value);
        try
        {
            Directory.CreateSymbolicLink(linkPath, linkTarget);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: force),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(linkPath));
        Assert.True(Directory.Exists(linkTarget));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PruneAsync_RejectsManifestSymlinkWithoutReportingBlockedManifest (bool force)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-manifest-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var targetRoot = scope.CreateDirectory(".agents/skills");
        var skillDirectory = scope.CreateDirectory(Path.Combine(".agents", "skills", orphan.Manifest.SkillName.Value));
        outsideScope.WriteFile("agent-skill.json", orphan.Files.Single(static file => file.RelativePath == "agent-skill.json").Content);
        var manifestLink = Path.Combine(skillDirectory, "agent-skill.json");
        if (!TestSymbolicLinks.TryCreateFile(manifestLink, Path.Combine(outsideScope.FullPath, "agent-skill.json")))
        {
            return;
        }

        var pruneService = SkillTestData.CreatePruneService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: force),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(Path.Combine(targetRoot, orphan.Manifest.SkillName.Value)));
        Assert.True(File.Exists(manifestLink));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WithForceWhenTargetChangesAfterPlanning_ReturnsFailureWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "prune-force-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var orphan = packages[0];
        var installService = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(SkillHostKind.OpenAi, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([orphan], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, orphan.Manifest.SkillName.Value);
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        var pruneService = SkillTestData.CreatePruneService(new MutatingSkillInstalledPackageRemover(
            SkillTestData.CreatePackageRemover(),
            directory => File.WriteAllText(Path.Combine(directory, "late-local-note.md"), "# Late local note\n")));

        var result = await pruneService.PruneAsync(
            new SkillPruneInput(orphan.Manifest.CatalogId, packages.Skip(1).ToArray(), request, Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.True(File.Exists(lateFile));
    }

    private static CanonicalSkillPackage CreatePackageWithCatalogId (
        CanonicalSkillPackage package,
        SkillCatalogId catalogId)
    {
        var manifest = SkillTestData.WithComputedManifestDigest(SkillTestData.CopyManifest(
            package.Manifest,
            catalogId: catalogId));
        var manifestText = new SkillManifestJsonSerializer().Serialize(manifest);
        var files = package.Files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile("agent-skill.json", manifestText)
                : file)
            .ToArray();

        return SkillTestData.CreateCanonicalPackage(manifest, files);
    }

}
