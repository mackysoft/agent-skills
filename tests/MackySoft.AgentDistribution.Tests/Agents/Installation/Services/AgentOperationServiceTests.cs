using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.Services;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Agents.Installation.Services;

public sealed class AgentOperationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_InstallsSelectedAgentAndResolvedSkillToSeparateTargets ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "install");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(
            skills,
            "architect",
            "architect.toml",
            "name = \"architect\"\n",
            [skills[0].Manifest.SkillName]);
        var catalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]]);
        var targets = CreateTargets(scope.FullPath);

        var result = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(catalog, targets.Agent, targets.Skill, printDiff: true));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentReconcileActionKind.Created, Assert.Single(result.Value!.Actions).ActionKind);
        var diff = Assert.Single(result.Value.Actions[0].Diffs!);
        Assert.Null(diff.BeforeContent);
        Assert.Equal("name = \"architect\"\n", diff.AfterContent);
        Assert.Equal("name = \"architect\"\n", await File.ReadAllTextAsync(Path.Combine(result.Value.ArtifactRoot.Value, "architect.toml")));
        Assert.True(File.Exists(Path.Combine(result.Value.SkillResult.TargetRoot.Value, skills[0].Manifest.SkillName.Value, "SKILL.md")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenAgentTargetIsUnmanaged_DoesNotStartSkillWrites ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "install-blocked");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "managed\n", [skills[0].Manifest.SkillName]);
        var catalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]]);
        var targets = CreateTargets(scope.FullPath);
        var artifactRoot = Path.Combine(scope.FullPath, "agent-target");
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "architect.toml"), "unmanaged\n");

        var result = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(catalog, targets.Agent, targets.Skill));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetUnmanaged, result.Failure!.Code);
        Assert.False(Directory.Exists(Path.Combine(scope.FullPath, "skill-target")));
        Assert.Equal("unmanaged\n", await File.ReadAllTextAsync(Path.Combine(artifactRoot, "architect.toml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task InstallAsync_WhenSelectedAgentsCollide_RejectsBeforeAnyWrite ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "install-collision");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var first = AgentOperationTestData.CreateAgent(skills, "architect", "shared.toml", "first\n");
        var second = AgentOperationTestData.CreateAgent(skills, "reviewer", "shared.toml", "second\n");
        var catalog = AgentOperationTestData.CreateCatalog(skills, [first, second]);
        var targets = CreateTargets(scope.FullPath);

        var result = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(catalog, targets.Agent, targets.Skill));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.False(Directory.Exists(Path.Combine(scope.FullPath, "agent-target")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateAsync_WhenManagedArtifactWasModified_RequiresForceAndThenConverges ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "update-force");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var original = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "original\n");
        var originalCatalog = AgentOperationTestData.CreateCatalog(skills, [original]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(originalCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var artifactPath = Path.Combine(install.Value!.ArtifactRoot.Value, "architect.toml");
        await File.WriteAllTextAsync(artifactPath, "local\n");
        var updated = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "updated\n");
        var updatedCatalog = AgentOperationTestData.CreateCatalog(skills, [updated]);

        var blocked = await AgentOperationTestData.CreateUpdateService(scope.FullPath).UpdateAsync(
            new AgentUpdateInput(updatedCatalog, targets.Agent, targets.Skill));
        var forced = await AgentOperationTestData.CreateUpdateService(scope.FullPath).UpdateAsync(
            new AgentUpdateInput(updatedCatalog, targets.Agent, targets.Skill, force: true));

        Assert.False(blocked.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetLocalModification, blocked.Failure!.Code);
        Assert.True(forced.IsSuccess, forced.Failure?.Message);
        Assert.Equal("updated\n", await File.ReadAllTextAsync(artifactPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UninstallAsync_RemovesOnlySelectedAgentAndLeavesResolvedSkillInstalled ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "uninstall");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "agent\n", [skills[0].Manifest.SkillName]);
        var catalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]], [agent.Manifest.AgentName]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(catalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var result = await AgentOperationTestData.CreateUninstallService(scope.FullPath).UninstallAsync(
            new AgentUninstallInput(catalog, targets.Agent));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentRemovalActionKind.Deleted, Assert.Single(result.Value!.Actions).ActionKind);
        Assert.False(File.Exists(Path.Combine(result.Value.ArtifactRoot.Value, "architect.toml")));
        Assert.True(File.Exists(Path.Combine(install.Value!.SkillResult.TargetRoot.Value, skills[0].Manifest.SkillName.Value, "SKILL.md")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_RemovesCleanSameCatalogOrphanAndLeavesSkillsInstalled ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "agent\n", [skills[0].Manifest.SkillName]);
        var installedCatalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(installedCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var currentCatalog = AgentOperationTestData.CreateCatalog(skills, []);

        var result = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(currentCatalog, targets.Agent));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentRemovalActionKind.Deleted, Assert.Single(result.Value!.Actions).ActionKind);
        Assert.False(File.Exists(Path.Combine(result.Value.ArtifactRoot.Value, "architect.toml")));
        Assert.True(File.Exists(Path.Combine(install.Value!.SkillResult.TargetRoot.Value, skills[0].Manifest.SkillName.Value, "SKILL.md")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WithRemovedAgentNameFilter_LeavesOtherOrphansInstalled ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune-name-filter");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var architect = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "architect\n");
        var reviewer = AgentOperationTestData.CreateAgent(skills, "reviewer", "reviewer.toml", "reviewer\n");
        var installedCatalog = AgentOperationTestData.CreateCatalog(skills, [architect, reviewer]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(installedCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var currentCatalog = AgentOperationTestData.CreateCatalog(skills, []);

        var result = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(
                currentCatalog,
                targets.Agent,
                selectedAgentNames: [architect.Manifest.AgentName]));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(AgentRemovalActionKind.Deleted, Assert.Single(result.Value!.Actions).ActionKind);
        Assert.False(File.Exists(Path.Combine(result.Value.ArtifactRoot.Value, "architect.toml")));
        Assert.True(File.Exists(Path.Combine(result.Value.ArtifactRoot.Value, "reviewer.toml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenCurrentAndOrphanStatesOwnSameArtifact_RejectsBeforeDeletion ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune-collision");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "agent\n");
        var currentCatalog = AgentOperationTestData.CreateCatalog(skills, [agent]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(currentCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);
        var target = AgentOperationTestData.CreateAgentTargetResolver(scope.FullPath).ResolveTarget(targets.Agent).Value!;
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var currentStatePath = statePathResolver.Resolve(target, agent.Manifest.CatalogId, agent.Manifest.AgentName).Value!;
        var currentState = (await stateStore.ReadAsync(currentStatePath)).Value!.State!;
        var orphanState = new AgentInstallationState(
            currentState.SchemaVersion,
            currentState.BundleVersion,
            currentState.CatalogId,
            currentState.HostId,
            new AgentName("orphan"),
            currentState.AgentManifestDigest,
            currentState.ManagedArtifacts);
        var orphanStatePath = statePathResolver.Resolve(target, agent.Manifest.CatalogId, orphanState.AgentName).Value!;
        var stateWrite = await stateStore.WriteAsync(orphanStatePath, orphanState);
        Assert.True(stateWrite.IsSuccess, stateWrite.Failure?.Message);

        var result = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(currentCatalog, targets.Agent));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.True(File.Exists(Path.Combine(install.Value!.ArtifactRoot.Value, "architect.toml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_CurrentCatalogStateAndInvalidStateFileNameKeepTheirDistinctActions ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune-state-action-contract");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var current = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "architect\n");
        var invalid = AgentOperationTestData.CreateAgent(skills, "reviewer", "reviewer.toml", "reviewer\n");
        var installedCatalog = AgentOperationTestData.CreateCatalog(skills, [current, invalid]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(installedCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var target = AgentOperationTestData.CreateAgentTargetResolver(scope.FullPath).ResolveTarget(targets.Agent).Value!;
        var statePathResolver = new AgentInstallationStatePathResolver();
        var reviewerStatePath = statePathResolver.Resolve(target, installedCatalog.CatalogId, invalid.Manifest.AgentName).Value!;
        var invalidStatePath = reviewerStatePath.TryGetParent(out var stateDirectory)
            ? AbsolutePath.Parse(Path.Combine(stateDirectory.Value, "renamed-state.json"))
            : throw new InvalidOperationException("Expected the ownership-state path to have a parent directory.");
        File.Move(reviewerStatePath.Value, invalidStatePath.Value);

        var currentCatalog = AgentOperationTestData.CreateCatalog(skills, [current]);
        var result = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(currentCatalog, targets.Agent, dryRun: true));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(
            [AgentRemovalActionKind.SkippedCurrent, AgentRemovalActionKind.BlockedInvalid],
            result.Value!.Actions.Select(static action => action.ActionKind).ToArray());
        Assert.True(File.Exists(Path.Combine(install.Value!.ArtifactRoot.Value, "architect.toml")));
        Assert.True(File.Exists(Path.Combine(install.Value.ArtifactRoot.Value, "reviewer.toml")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_ReportsForeignCatalogAndLocalModificationActionsAccordingToForce ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune-orphan-action-contract");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var foreign = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "architect\n");
        var modified = AgentOperationTestData.CreateAgent(skills, "reviewer", "reviewer.toml", "reviewer\n");
        var installedCatalog = AgentOperationTestData.CreateCatalog(skills, [foreign, modified]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(installedCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var target = AgentOperationTestData.CreateAgentTargetResolver(scope.FullPath).ResolveTarget(targets.Agent).Value!;
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var foreignStatePath = statePathResolver.Resolve(target, installedCatalog.CatalogId, foreign.Manifest.AgentName).Value!;
        var foreignState = (await stateStore.ReadAsync(foreignStatePath)).Value!.State!;
        var foreignStateWrite = await stateStore.WriteAsync(
            foreignStatePath,
            new AgentInstallationState(
                foreignState.SchemaVersion,
                foreignState.BundleVersion,
                new AgentDistributionCatalogId("foreign-catalog"),
                foreignState.HostId,
                foreignState.AgentName,
                foreignState.AgentManifestDigest,
                foreignState.ManagedArtifacts));
        Assert.True(foreignStateWrite.IsSuccess, foreignStateWrite.Failure?.Message);
        await File.WriteAllTextAsync(Path.Combine(install.Value!.ArtifactRoot.Value, "reviewer.toml"), "local change\n");

        var currentCatalog = AgentOperationTestData.CreateCatalog(skills, []);
        var blocked = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(currentCatalog, targets.Agent, dryRun: true));
        var forced = await AgentOperationTestData.CreatePruneService(scope.FullPath).PruneAsync(
            new AgentPruneInput(currentCatalog, targets.Agent, dryRun: true, force: true));

        Assert.True(blocked.IsSuccess, blocked.Failure?.Message);
        Assert.Equal(
            [
                (AgentInstalledTargetStateKind.OtherCatalog, AgentRemovalActionKind.BlockedForeignCatalog),
                (AgentInstalledTargetStateKind.LocallyModified, AgentRemovalActionKind.BlockedLocalModification),
            ],
            blocked.Value!.Actions.Select(static action => (action.TargetStateKind, action.ActionKind)).ToArray());
        Assert.True(forced.IsSuccess, forced.Failure?.Message);
        Assert.Equal(
            [
                (AgentInstalledTargetStateKind.OtherCatalog, AgentRemovalActionKind.BlockedForeignCatalog),
                (AgentInstalledTargetStateKind.LocallyModified, AgentRemovalActionKind.Deleted),
            ],
            forced.Value!.Actions.Select(static action => (action.TargetStateKind, action.ActionKind)).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenLaterOrphanDriftsAtDeletionBoundary_StopsBeforeDeletingAnyArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "prune-precondition-change");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var architect = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "architect\n");
        var reviewer = AgentOperationTestData.CreateAgent(skills, "reviewer", "reviewer.toml", "reviewer\n");
        var installedCatalog = AgentOperationTestData.CreateCatalog(skills, [architect, reviewer]);
        var targets = CreateTargets(scope.FullPath);
        var install = await AgentOperationTestData.CreateInstallService(scope.FullPath).InstallAsync(
            new AgentInstallInput(installedCatalog, targets.Agent, targets.Skill));
        Assert.True(install.IsSuccess, install.Failure?.Message);

        var target = AgentOperationTestData.CreateAgentTargetResolver(scope.FullPath).ResolveTarget(targets.Agent).Value!;
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var architectStatePath = statePathResolver.Resolve(target, installedCatalog.CatalogId, architect.Manifest.AgentName).Value!;
        var reviewerStatePath = statePathResolver.Resolve(target, installedCatalog.CatalogId, reviewer.Manifest.AgentName).Value!;
        var architectArtifactPath = Path.Combine(install.Value!.ArtifactRoot.Value, "architect.toml");
        var reviewerArtifactPath = Path.Combine(install.Value.ArtifactRoot.Value, "reviewer.toml");
        var mutatingStore = new MutatingAgentManagedArtifactStore(
            new AgentManagedArtifactStore(statePathResolver, stateStore),
            () => File.AppendAllText(reviewerArtifactPath, "local change\n"));

        var result = await AgentOperationTestData.CreatePruneService(scope.FullPath, mutatingStore).PruneAsync(
            new AgentPruneInput(AgentOperationTestData.CreateCatalog(skills, []), targets.Agent));

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.True(File.Exists(architectArtifactPath));
        Assert.True(File.Exists(reviewerArtifactPath));
        Assert.True(File.Exists(architectStatePath.Value));
        Assert.True(File.Exists(reviewerStatePath.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DiagnoseAsync_ReportsAgentTargetAndResolvedSkillResultsSeparately ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-operations", "doctor");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentOperationTestData.CreateAgent(skills, "architect", "architect.toml", "agent\n", [skills[0].Manifest.SkillName]);
        var catalog = AgentOperationTestData.CreateCatalog(skills, [agent], [skills[0]]);
        var targets = CreateTargets(scope.FullPath);

        var result = await AgentOperationTestData.CreateDoctorService(scope.FullPath).DiagnoseAsync(
            new AgentDoctorInput(catalog, targets.Agent, targets.Skill));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var diagnostic = Assert.Single(result.Value!.Diagnostics);
        Assert.Equal(AgentDoctorDiagnosticArea.TargetState, diagnostic.Area);
        Assert.True(diagnostic.IsError);
        Assert.False(result.Value.SkillResult.IsHealthy);
        Assert.False(result.Value.IsHealthy);
    }

    private static OperationTargets CreateTargets (string repositoryRoot)
    {
        return new OperationTargets(
            SkillTestData.CreateAgentTargetRequest(HostKind.Codex, AgentInstallScopeKind.Project, repositoryRoot, "agent-target"),
            SkillTestData.CreateInstallRequest(HostKind.Codex, SkillScopeKind.Project, repositoryRoot, "skill-target"));
    }

    private sealed class MutatingAgentManagedArtifactStore : IAgentManagedArtifactStore
    {
        private readonly IAgentManagedArtifactStore inner;
        private readonly Action mutate;

        private int mutationInvoked;

        public MutatingAgentManagedArtifactStore (IAgentManagedArtifactStore inner, Action mutate)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.mutate = mutate ?? throw new ArgumentNullException(nameof(mutate));
        }

        public ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
            AgentReconciliationPlan plan,
            AgentResolvedTarget target,
            CancellationToken cancellationToken)
        {
            return inner.WriteAsync(plan, target, cancellationToken);
        }

        public ValueTask<AgentDistributionOperationResult<bool>> DeleteAsync (
            AgentInstallationState state,
            AbsolutePath statePath,
            AgentResolvedTarget target,
            Func<CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref mutationInvoked, 1) == 0)
            {
                mutate();
            }

            return inner.DeleteAsync(state, statePath, target, precondition, cancellationToken);
        }
    }

    private sealed record OperationTargets (AgentTargetRequest Agent, SkillInstallRequest Skill);
}
