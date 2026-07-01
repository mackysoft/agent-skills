using System.Text;
using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Services;

public sealed class SkillInstallServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_CreatesThenNoOps_WhenTargetMatchesSameHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-noop");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        var noOp = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.True(created.IsSuccess, created.Failure?.Message);
        Assert.True(noOp.IsSuccess, noOp.Failure?.Message);
        Assert.All(created.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.Created, action.ActionKind));
        Assert.All(noOp.Value!.Actions, static action => Assert.Equal(SkillInstallActionKind.NoOp, action.ActionKind));
        foreach (var package in packages)
        {
            var expectedManifest = package.Files.Single(static file => file.RelativePath == "agent-skill.json").Content;
            var actualManifest = File.ReadAllText(Path.Combine(scope.FullPath, ".agents", "skills", package.Manifest.SkillName.Value, "agent-skill.json"));
            Assert.Equal(expectedManifest, actualManifest);
        }

        Assert.True(File.Exists(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName.Value, "agents", "openai.yaml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunCreatesPlanWithoutWritingFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-dry-run-create");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);

        var result = await service.InstallAsync(new SkillInstallInput(packages, request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.DryRun);
        Assert.All(result.Value.Actions, static action => Assert.Equal(SkillInstallActionKind.Created, action.ActionKind));
        Assert.All(result.Value.Actions, static action => Assert.Equal(nameof(SkillInstalledTargetStateKind.Missing), action.TargetState!.Kind));
        Assert.All(result.Value.Actions, static action => Assert.NotEmpty(action.Diffs!));
        Assert.All(result.Value.Actions, static action =>
        {
            Assert.NotNull(action.FileChanges);
            Assert.Empty(action.FileChanges!.ReplacedFiles);
            Assert.Empty(action.FileChanges!.RemovedFiles);
        });
        Assert.False(Directory.Exists(result.Value.TargetRoot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksManagedOverwriteWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-dry-run-managed-overwrite");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        var originalSkill = File.ReadAllText(skillPath);
        var updatedPackages = SkillTestData.ReplacePackage(packages, SkillTestData.CreatePackageWithUpdatedBody(packages[0]));

        var result = await service.InstallAsync(
            new SkillInstallInput(updatedPackages, request, DryRun: true, PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillInstallActionKind.BlockedManagedOverwrite, action.ActionKind);
        Assert.Equal(SkillBlockedReason.ManagedOverwriteRequiresForce, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.CleanOutdated), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetOutdated, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(originalSkill, File.ReadAllText(skillPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksVersionAheadWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-dry-run-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion + 1);
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync([aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, aheadPackage.Manifest.SkillName.Value, "agent-skill.json");
        var aheadManifest = File.ReadAllText(manifestPath);

        var result = await service.InstallAsync(new SkillInstallInput([packages[0]], request, DryRun: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.BlockedManagedOverwrite, action.ActionKind);
        Assert.Equal(SkillBlockedReason.InstalledVersionAhead, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.VersionAhead), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetVersionAhead, action.TargetState.Code);
        Assert.Equal(aheadManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceOverwritesVersionAheadPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-version-ahead");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var aheadPackage = SkillTestData.CreatePackageWithSkillBundleVersion(packages[0], packages[0].Manifest.SkillBundleVersion + 1);
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync([aheadPackage], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var manifestPath = Path.Combine(install.Value!.TargetRoot, aheadPackage.Manifest.SkillName.Value, "agent-skill.json");

        var result = await service.InstallAsync(new SkillInstallInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.Updated, action.ActionKind);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.VersionAhead), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetVersionAhead, action.TargetState.Code);
        Assert.Equal(aheadPackage.Manifest.SkillBundleVersion, action.TargetState.InstalledSkillBundleVersion);
        Assert.Equal(packages[0].Manifest.SkillBundleVersion, action.TargetState.BundledSkillBundleVersion);
        Assert.Equal(packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceOverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-local-modification");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillPath = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await service.InstallAsync(new SkillInstallInput(packages, request, Force: true, PrintDiff: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillInstallActionKind.Updated, action.ActionKind);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(new[] { "SKILL.md" }, action.FileChanges!.ReplacedFiles);
        Assert.Empty(action.FileChanges!.RemovedFiles);
        Assert.DoesNotContain("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceRemovesExtraFileAndReportsIt ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await service.InstallAsync(new SkillInstallInput([packages[0]], request, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.Updated, action.ActionKind);
        Assert.Empty(action.FileChanges!.ReplacedFiles);
        Assert.Equal(new[] { "local-note.md" }, action.FileChanges!.RemovedFiles);
        Assert.False(File.Exists(extraFile));
        Assert.True(File.Exists(Path.Combine(result.Value!.TargetRoot, packages[1].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunWithForceReportsChangesWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-dry-run-force-changes");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var extraFile = Path.Combine(skillDirectory, "local-note.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");
        File.WriteAllText(extraFile, "# Local note\n");

        var result = await service.InstallAsync(new SkillInstallInput([packages[0]], request, DryRun: true, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.Updated, action.ActionKind);
        Assert.Equal(new[] { "SKILL.md" }, action.FileChanges!.ReplacedFiles);
        Assert.Equal(new[] { "local-note.md" }, action.FileChanges!.RemovedFiles);
        Assert.Contains("Injected instruction.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(extraFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceRejectsUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            new SkillInstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal("# Existing\n", File.ReadAllText(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenTargetAppearsAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var firstSkillDirectory = Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName.Value);
        var unmanagedPath = Path.Combine(firstSkillDirectory, "SKILL.md");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
        {
            Directory.CreateDirectory(firstSkillDirectory);
            File.WriteAllText(unmanagedPath, "# Race\n");
        });

        var result = await service.InstallAsync([packages[0], secondPackage], request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.Equal("# Race\n", File.ReadAllText(unmanagedPath));
        Assert.False(File.Exists(Path.Combine(firstSkillDirectory, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceWhenEmptyDirectoryAppearsAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-empty-directory-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateDirectory = Path.Combine(skillDirectory, "late-local-notes");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            Directory.CreateDirectory(lateDirectory));

        var result = await service.InstallAsync(new SkillInstallInput([packages[0], secondPackage], request, Force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(Directory.Exists(lateDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WithForceWhenFileAppearsAfterPlanning_ReturnsFailureWithoutOverwriting ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-force-file-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync([packages[0], packages[1]], request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var lateFile = Path.Combine(skillDirectory, "late-local-note.md");
        File.AppendAllText(skillPath, "\nInjected before planning.\n");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
            File.WriteAllText(lateFile, "# Late local note\n"));

        var result = await service.InstallAsync(new SkillInstallInput([packages[0], secondPackage], request, Force: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
        Assert.Contains("Injected before planning.", File.ReadAllText(skillPath), StringComparison.Ordinal);
        Assert.True(File.Exists(lateFile));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenLaterTargetAppearsAfterPlanning_ReturnsFailureWithoutCreatingEarlierTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-later-target-race");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var firstSkillDirectory = Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName.Value);
        var secondSkillDirectory = Path.Combine(scope.FullPath, ".agents", "skills", packages[1].Manifest.SkillName.Value);
        var secondUnmanagedPath = Path.Combine(secondSkillDirectory, "SKILL.md");
        var secondPackage = SkillTestData.WithFileEnumerationCallback(packages[1], () =>
        {
            Directory.CreateDirectory(secondSkillDirectory);
            File.WriteAllText(secondUnmanagedPath, "# Race\n");
        });

        var result = await service.InstallAsync([packages[0], secondPackage], request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.False(Directory.Exists(firstSkillDirectory));
        Assert.Equal("# Race\n", File.ReadAllText(secondUnmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var targetRoot = "shared-skills";

        var claude = await service.InstallAsync(
            packages,
            new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);
        var openAi = await service.InstallAsync(
            new SkillInstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
                Force: true),
            CancellationToken.None);

        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        Assert.False(openAi.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, openAi.Failure!.Code);
        Assert.True(File.Exists(Path.Combine(claude.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsOpenAiSharedTargetRootFromDifferentHost ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-openai-host-conflict");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var targetRoot = "shared-skills";

        var openAi = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
            CancellationToken.None);
        var claude = await service.InstallAsync(
            new SkillInstallInput(
                packages,
                new SkillInstallRequest(ClaudeSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath, targetRoot),
                Force: true),
            CancellationToken.None);

        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.False(claude.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostConflict, claude.Failure!.Code);
        Assert.True(File.Exists(Path.Combine(openAi.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsNameCollision ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-name-collision");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var targetRoot = scope.CreateDirectory(".agents/skills");
        SkillTestData.WriteNameCollisionManifest(targetRoot, packages[0]);

        var result = await service.InstallAsync(
            new SkillInstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetNameCollision, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsExistingUnmanagedTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksUnmanagedTargetWithoutDiffContent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-dry-run-unmanaged-no-diff-content");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "SKILL.md"), "# Existing\nsecret=local\n");

        var result = await service.InstallAsync(
            new SkillInstallInput(
                [packages[0]],
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
                DryRun: true,
                PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single();
        Assert.Equal(SkillInstallActionKind.BlockedUnmanaged, action.ActionKind);
        Assert.Equal(SkillBlockedReason.UnmanagedTarget, action.BlockedReason);
        Assert.Empty(action.Diffs!);
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenLaterTargetIsUnmanaged_DoesNotCreateEarlierPackage ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-plan-before-write-unmanaged");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var firstSkillDirectory = Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName.Value);
        var unmanagedPath = scope.WriteFile(Path.Combine(".agents", "skills", packages[1].Manifest.SkillName.Value, "SKILL.md"), "# Existing\n");

        var result = await service.InstallAsync(
            [packages[0], packages[1]],
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.False(Directory.Exists(firstSkillDirectory));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsDifferentContentDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-digest-mismatch");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath).Replace(packages[0].Manifest.ContentDigest, new string('0', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsLocallyModifiedInstalledManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var originalDigest = packages[0].Manifest.HostArtifacts
            .Single(static artifact => artifact.Host == OpenAiSkillHostAdapter.HostKey)
            .Digest!;
        var manifestText = File.ReadAllText(manifestPath).Replace(originalDigest, new string('f', 64), StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifestText);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksInstalledManifestDigestOnlyDriftAsLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-digest-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        SkillTestData.TamperManifestDigest(manifestPath);
        var tamperedManifest = File.ReadAllText(manifestPath);

        var result = await service.InstallAsync(
            new SkillInstallInput(packages, request, DryRun: true, PrintDiff: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == packages[0].Manifest.SkillName.Value);
        Assert.Equal(SkillInstallActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(SkillBlockedReason.LocalModificationRequiresForce, action.BlockedReason);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.ManifestDrift), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetManifestDigestMismatch, action.TargetState.Code);
        Assert.NotEmpty(action.Diffs!);
        Assert.Equal(tamperedManifest, File.ReadAllText(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsInstalledManifestUtf8ByteOrderMark ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-bom");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var manifestPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agent-skill.json");
        var manifestText = File.ReadAllText(manifestPath);
        await File.WriteAllBytesAsync(manifestPath, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(manifestText)]);

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("byte order mark", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledSkillBody ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-body-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var skillPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.AppendAllText(skillPath, "\nInjected instruction.\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledFrontmatter ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-frontmatter-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var skillPath = Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md");
        File.WriteAllText(skillPath, File.ReadAllText(skillPath).Replace("description:", "description: Drifted", StringComparison.Ordinal));

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFrontmatterDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedOpenAiMetadataArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-host-artifact-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        File.AppendAllText(Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "agents", "openai.yaml"), "\n# Drifted metadata.\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetHostArtifactDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsMissingInstalledSkillBodyWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-body-missing");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        File.Delete(Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "SKILL.md"));

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_DryRunBlocksFileSetDriftWithTargetStateDetails ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-file-set-target-state");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);
        var package = packages[0];
        var referencePath = package.Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath;
        File.Delete(Path.Combine(created.Value!.TargetRoot, package.Manifest.SkillName.Value, referencePath));

        var result = await service.InstallAsync(new SkillInstallInput(packages, request, DryRun: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var action = result.Value!.Actions.Single(action => action.Identity.SkillName.Value == package.Manifest.SkillName.Value);
        Assert.Equal(SkillInstallActionKind.BlockedLocalModification, action.ActionKind);
        Assert.Equal(nameof(SkillInstalledTargetStateKind.FileSetDrift), action.TargetState!.Kind);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, action.TargetState.Code);
        Assert.Contains(referencePath, action.TargetState.FileSet!.MissingFiles);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsModifiedInstalledReference ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-reference-drift");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);

        var referencePath = Path.Combine(
            created.Value!.TargetRoot,
            packages[0].Manifest.SkillName.Value,
            packages[0].Files.First(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal)).RelativePath);
        File.AppendAllText(referencePath, "\nInjected reference.\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetContentDigestMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsUnexpectedInstalledFile ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-extra-file");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var created = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(created.IsSuccess, created.Failure?.Message);
        File.WriteAllText(Path.Combine(created.Value!.TargetRoot, packages[0].Manifest.SkillName.Value, "references", "extra.md"), "# Extra\n");

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetFileSetMismatch, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsInvalidExistingManifestWithoutThrowing ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-invalid-manifest");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.WriteFile(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value, "agent-skill.json"), "{}");

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsManifestSymlinkThatEscapesTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        scope.CreateDirectory(Path.Combine(".agents", "skills", packages[0].Manifest.SkillName.Value));
        var outsideManifest = outsideScope.WriteFile("agent-skill.json", packages[0].Files.Single(static file => file.RelativePath == "agent-skill.json").Content);
        try
        {
            File.CreateSymbolicLink(Path.Combine(scope.FullPath, ".agents", "skills", packages[0].Manifest.SkillName.Value, "agent-skill.json"), outsideManifest);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsManifestSymlinkWithinTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-manifest-symlink-local");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();
        var request = new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, scope.FullPath);
        var install = await service.InstallAsync(packages, request, CancellationToken.None);
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var skillDirectory = Path.Combine(install.Value!.TargetRoot, packages[0].Manifest.SkillName.Value);
        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var targetPath = Path.Combine(skillDirectory, "agent-skill.actual.json");
        File.Move(manifestPath, targetPath);
        try
        {
            File.CreateSymbolicLink(manifestPath, targetPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var result = await service.InstallAsync(packages, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "install-unsupported-host");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest("generic", SkillScopeKind.Project, scope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsTargetRootOutsideRepository ()
    {
        using var repoScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-path-repo");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-path-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            new SkillInstallInput(
                packages,
                new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath, outsideScope.FullPath),
                Force: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsTargetRootThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repoScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-path-symlink-repo");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-path-symlink-outside");
        var symlinkPath = Path.Combine(repoScope.FullPath, "linked");
        try
        {
            Directory.CreateSymbolicLink(symlinkPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath, "linked/skills"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_RejectsExistingSkillDirectoryThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repoScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-skill-symlink-repo");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "install-skill-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var targetRoot = repoScope.CreateDirectory(".agents/skills");
        var symlinkPath = Path.Combine(targetRoot, packages[0].Manifest.SkillName.Value);
        try
        {
            Directory.CreateSymbolicLink(symlinkPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var service = SkillTestData.CreateInstallService();

        var result = await service.InstallAsync(
            packages,
            new SkillInstallRequest(OpenAiSkillHostAdapter.HostKey, SkillScopeKind.Project, repoScope.FullPath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }
}
