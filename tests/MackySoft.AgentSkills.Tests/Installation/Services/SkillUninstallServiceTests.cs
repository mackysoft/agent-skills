using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Services;

public sealed class SkillUninstallServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DeletesManagedSkillsAndPreservesTargetRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-delete");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", "custom-skill", "SKILL.md"), "# Custom\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind));
        Assert.True(Directory.Exists(result.Value.TargetRoot));
        Assert.True(File.Exists(unmanagedPath));
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.Manifest.SkillName.Value)), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DryRunReturnsDeletedPlanWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-dry-run-delete");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.DryRun);
        Assert.All(result.Value.Actions, static action => Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind));
        Assert.All(result.Value.Actions, static action => Assert.Equal(nameof(SkillInstalledTargetStateKind.Current), action.TargetState!.Kind));
        Assert.All(result.Value.Actions, static action =>
        {
            Assert.NotNull(action.FileChanges);
            Assert.Empty(action.FileChanges!.ReplacedFiles);
            Assert.Contains("SKILL.md", action.FileChanges!.RemovedFiles);
            Assert.Contains("agent-skill.json", action.FileChanges!.RemovedFiles);
        });
        foreach (var package in packages)
        {
            Assert.True(Directory.Exists(Path.Combine(result.Value.TargetRoot, package.Manifest.SkillName.Value)), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DryRunWithForceReportsChangesWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-dry-run-force-changes");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await uninstallService.UninstallAsync(
            new SkillUninstallInput([packages[0]], request, DryRun: true, Force: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Contains("SKILL.md", action.FileChanges!.RemovedFiles);
        Assert.Contains("agent-skill.json", action.FileChanges!.RemovedFiles);
        Assert.Contains("local-note.md", action.FileChanges!.RemovedFiles);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Contains("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(extraFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_NoOps_WhenTargetRootIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-missing-target");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUninstallService();

        var result = await service.UninstallAsync(
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.All(result.Value!.Actions, static action => Assert.Equal(SkillUninstallActionKind.NoOp, action.ActionKind));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_NoOps_WhenSkillDirectoryIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-missing-skill");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        Directory.Delete(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value), recursive: true);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0]], request), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillUninstallActionKind.NoOp, result.Value!.Actions.Single().ActionKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_SkipsUnmanagedSkillDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUninstallService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "SKILL.md"), "# Existing\n");

        var result = await service.UninstallAsync(
            new SkillUninstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUninstallActionKind.SkippedUnmanaged, action.ActionKind);
        Assert.Null(action.FileChanges);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateUninstallService();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);

        var result = await service.UninstallAsync(
            new SkillUninstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        File.AppendAllText(Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WhenLaterTargetHasLocalModification_DoesNotDeleteEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-plan-before-delete-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var firstSkillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var modifiedSkillDirectory = Path.Combine(install.Value.TargetRoot, packages[1].Manifest.SkillName.Value);
        File.AppendAllText(Path.Combine(modifiedSkillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0], packages[1]], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(firstSkillDirectory));
        Assert.True(Directory.Exists(modifiedSkillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WhenTargetChangesAfterPlanning_ReturnsFailureWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.AppendAllText(skillPath, "\nInjected after planning.\n"));

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0], secondPackage], request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Contains("Injected after planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DryRunBlocksLocalModificationWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-dry-run-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillUninstallActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.CommonContentDrift), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, action.TargetState.Code);
        Assert.True(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WithForceDeletesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        File.AppendAllText(Path.Combine(skillDirectory, "SKILL.md"), "\nInjected instruction.\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Contains("SKILL.md", action.FileChanges!.RemovedFiles);
        Assert.Contains("agent-skill.json", action.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(skillDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WithForceRemovesExtraFileAndReportsIt ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillUninstallActionKind.Deleted, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Contains("local-note.md", action.FileChanges!.RemovedFiles);
        Assert.False(Directory.Exists(skillDirectory));
        Assert.True(File.Exists(Path.Combine(result.Value!.TargetRoot, packages[1].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WithForceWhenTargetChangesAfterPlanning_ReturnsFailureWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-force-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.WriteAllText(lateFile, "# Late local note\n"));

        var result = await uninstallService.UninstallAsync(
            new SkillUninstallInput([packages[0], secondPackage], request, Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(lateFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_WithForceWhenEmptyDirectoryAppearsAfterPlanning_ReturnsFailureWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-force-empty-directory-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateDirectory = Path.Combine(skillDirectory, "late-local-notes");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            Directory.CreateDirectory(lateDirectory));

        var result = await uninstallService.UninstallAsync(
            new SkillUninstallInput([packages[0], secondPackage], request, Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(skillDirectory));
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(lateDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalEmptyDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-local-empty-directory");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var localDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "local-notes");
        Directory.CreateDirectory(localDirectory);

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsLocalDirectorySymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-local-directory-symlink");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await installService.InstallAsync(packages, request, CancellationToken.None);
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

        var result = await uninstallService.UninstallAsync(new SkillUninstallInput(packages, request, Force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(localDirectoryLink));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var install = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
            CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await uninstallService.UninstallAsync(
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, "shared-skills"),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-other-host");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var installService = SkillTestData.CreateInstallService();
        var uninstallService = SkillTestData.CreateUninstallService();
        var openAi = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        var claude = await installService.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);
        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);

        var result = await uninstallService.UninstallAsync(
            new SkillUninstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        foreach (var package in packages)
        {
            Assert.False(Directory.Exists(Path.Combine(openAi.Value!.TargetRoot, package.Manifest.SkillName.Value)), package.Manifest.SkillName.Value);
            Assert.True(Directory.Exists(Path.Combine(claude.Value!.TargetRoot, package.Manifest.SkillName.Value)), package.Manifest.SkillName.Value);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RejectsManifestSymlinkThatEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-manifest-symlink-outside");
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

        var service = SkillTestData.CreateUninstallService();

        var result = await service.UninstallAsync(
            new SkillUninstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PackageRemoverDeleteAsync_RejectsTargetRootDeletion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "uninstall-remover-target-root");
        var remover = SkillTestData.CreatePackageRemover();

        var result = await remover.DeleteAsync(scope.FullPath, scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.True(Directory.Exists(scope.FullPath));
    }
}
