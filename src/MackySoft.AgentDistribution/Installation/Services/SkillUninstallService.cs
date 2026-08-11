using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.State;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Services;

/// <summary> Uninstalls SKILL packages from a bundle target root. </summary>
public sealed class SkillUninstallService
{
    private readonly SkillCatalogTargetRootSelector targetSelector;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillInstalledPackageRemover packageRemover;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillUninstallService" /> class. </summary>
    /// <param name="targetSelector"> The installed-catalog-aware target selector. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillUninstallService (
        SkillCatalogTargetRootSelector targetSelector,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillInstalledPackageRemover packageRemover,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageRemover = packageRemover ?? throw new ArgumentNullException(nameof(packageRemover));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Uninstalls SKILL packages. </summary>
    /// <param name="input"> The uninstall input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The uninstall result or failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillUninstallResult>> UninstallAsync (
        SkillUninstallInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var targetRequest = input.TargetRequest;
        var targetResult = await targetSelector.SelectTargetAsync(
                targetRequest,
                input.CatalogId,
                input.Packages.Select(static package => package.Manifest.SkillName).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUninstallActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryPath = ContainedPath.Create(
                targetRoot,
                RootRelativePath.Parse(skillName.Value)).Target;
            var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(targetRoot, skillDirectoryPath);
            if (!skillDirectoryResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
            }

            var actionPlanResult = await CreateActionPlanAsync(package, skillDirectory, identity, stateResult.Value!, input, cancellationToken).ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
            }

            actionPlans.Add(actionPlanResult.Value!);
        }

        if (!input.DryRun)
        {
            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionPlan.ShouldDelete)
                {
                    continue;
                }

                var preconditionResult = await ValidateDeletePreconditionAsync(
                        actionPlan.Package,
                        target.Host,
                        actionPlan.SkillDirectory,
                        actionPlan.TargetSnapshot,
                        input.Force,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!actionPlan.ShouldDelete)
                {
                    continue;
                }

                var deleteResult = await packageRemover.DeleteAsync(
                        targetRoot,
                        actionPlan.SkillDirectory,
                        (directory, token) => ValidateDeletePreconditionAsync(
                            actionPlan.Package,
                            target.Host,
                            directory,
                            actionPlan.TargetSnapshot,
                            input.Force,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<SkillUninstallResult>.FailureResult(deleteResult.Failure!.Code, deleteResult.Failure.Message);
                }
            }
        }

        return AgentDistributionOperationResult<SkillUninstallResult>.Success(new SkillUninstallResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUninstallActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        AbsolutePath skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input,
        CancellationToken cancellationToken)
    {
        switch (state.Kind)
        {
            case SkillTargetStateKind.Missing:
                return AgentDistributionOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(
                        identity,
                        SkillUninstallActionKind.NoOp,
                        SkillActionTargetStateProjection.Create(state),
                        blockedReason: null,
                        fileChanges: null),
                    skillDirectory,
                    package,
                    shouldDelete: false,
                    targetSnapshot: null));
            case SkillTargetStateKind.Current:
            case SkillTargetStateKind.CleanOutdated:
            case SkillTargetStateKind.VersionAhead:
                return await CreateDeleteActionPlanAsync(package, skillDirectory, identity, state, cancellationToken).ConfigureAwait(false);
            case SkillTargetStateKind.Unmanaged:
                return AgentDistributionOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(
                        identity,
                        SkillUninstallActionKind.SkippedUnmanaged,
                        SkillActionTargetStateProjection.Create(state),
                        blockedReason: null,
                        fileChanges: null),
                    skillDirectory,
                    package,
                    shouldDelete: false,
                    targetSnapshot: null));
            case var kind when SkillTargetStateClassifier.IsLocalModificationDrift(kind):
                return await CreateLocalModificationActionPlanAsync(package, skillDirectory, identity, state, input, cancellationToken).ConfigureAwait(false);
            case SkillTargetStateKind.NameCollision:
            case SkillTargetStateKind.HostConflict:
                return AgentDistributionOperationResult<SkillUninstallActionPlan>.FailureResult(
                    ResolveStateFailureCode(state),
                    state.Failure?.Message ?? $"Target skill directory cannot be deleted: {skillDirectory}");
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUninstallActionPlan>> CreateLocalModificationActionPlanAsync (
        CanonicalSkillPackage package,
        AbsolutePath skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input,
        CancellationToken cancellationToken)
    {
        if (input.Force)
        {
            return await CreateDeleteActionPlanAsync(package, skillDirectory, identity, state, cancellationToken).ConfigureAwait(false);
        }

        if (input.DryRun)
        {
            return AgentDistributionOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                new SkillUninstallAction(
                    identity,
                    SkillUninstallActionKind.BlockedLocalModification,
                    SkillActionTargetStateProjection.Create(state),
                    SkillBlockedReason.LocalModificationRequiresForce,
                    fileChanges: null),
                skillDirectory,
                package,
                shouldDelete: false,
                targetSnapshot: null));
        }

        return AgentDistributionOperationResult<SkillUninstallActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory contains local modifications. Use --force to delete: {skillDirectory}");
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUninstallActionPlan>> CreateDeleteActionPlanAsync (
        CanonicalSkillPackage package,
        AbsolutePath skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var fileChangesResult = await diffBuilder.BuildDeletionFileChangesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!fileChangesResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillUninstallActionPlan>.FailureResult(
                fileChangesResult.Failure!.Code,
                fileChangesResult.Failure.Message);
        }

        return AgentDistributionOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
            new SkillUninstallAction(
                identity,
                SkillUninstallActionKind.Deleted,
                SkillActionTargetStateProjection.Create(state),
                blockedReason: null,
                fileChangesResult.Value!.FileChanges),
            skillDirectory,
            package,
            shouldDelete: true,
            targetSnapshot: fileChangesResult.Value.TargetSnapshot));
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateDeletePreconditionAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        SkillActionTargetSnapshot? targetSnapshot,
        bool force,
        CancellationToken cancellationToken)
    {
        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
        }

        var state = stateResult.Value!;
        var isValid = SkillForceTargetStatePolicy.CanDelete(state.Kind, force);
        if (isValid)
        {
            return targetSnapshot is null
                ? AgentDistributionOperationResult<bool>.Success(true)
                : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, state, cancellationToken).ConfigureAwait(false);
        }

        return AgentDistributionOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private static AgentDistributionFailureCode ResolveChangedTargetFailureCode (SkillInstalledTargetState state)
    {
        return state.Kind == SkillTargetStateKind.Unmanaged
            ? AgentDistributionFailureCodes.InstallTargetUnmanaged
            : ResolveStateFailureCode(state);
    }

    private static AgentDistributionFailureCode ResolveStateFailureCode (SkillInstalledTargetState state)
    {
        return state.Failure?.Code ?? AgentDistributionFailureCodes.InstallTargetDigestMismatch;
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateTargetSnapshotAsync (
        AbsolutePath skillDirectory,
        SkillActionTargetSnapshot expectedSnapshot,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await diffBuilder.BuildTargetSnapshotAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(snapshotResult.Failure!.Code, snapshotResult.Failure.Message);
        }

        if (snapshotResult.Value == expectedSnapshot)
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        return AgentDistributionOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private sealed class SkillUninstallActionPlan
    {
        public SkillUninstallActionPlan (
            SkillUninstallAction action,
            AbsolutePath skillDirectory,
            CanonicalSkillPackage package,
            bool shouldDelete,
            SkillActionTargetSnapshot? targetSnapshot)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ArgumentNullException.ThrowIfNull(skillDirectory);
            if (shouldDelete != (targetSnapshot is not null))
            {
                throw new ArgumentException("A delete action plan must have a target snapshot, and a non-delete plan must not have one.", nameof(targetSnapshot));
            }

            SkillDirectory = skillDirectory;
            Package = package ?? throw new ArgumentNullException(nameof(package));
            ShouldDelete = shouldDelete;
            TargetSnapshot = targetSnapshot;
        }

        public SkillUninstallAction Action { get; }

        public AbsolutePath SkillDirectory { get; }

        public CanonicalSkillPackage Package { get; }

        public bool ShouldDelete { get; }

        public SkillActionTargetSnapshot? TargetSnapshot { get; }
    }
}
