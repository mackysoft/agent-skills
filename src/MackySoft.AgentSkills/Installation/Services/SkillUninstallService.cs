using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Uninstalls SKILL packages from a host target root. </summary>
public sealed class SkillUninstallService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillInstalledPackageRemover packageRemover;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillUninstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillUninstallService (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillInstalledPackageRemover packageRemover,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageRemover = packageRemover ?? throw new ArgumentNullException(nameof(packageRemover));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Uninstalls SKILL packages. </summary>
    /// <param name="input"> The uninstall input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The uninstall result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillUninstallResult>> UninstallAsync (
        SkillUninstallInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var targetRequest = input.TargetRequest;
        var targetResult = targetResolver.ResolveTarget(targetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillUninstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUninstallActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, skillName.Value);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
            }

            var actionPlanResult = await CreateActionPlanAsync(package, skillDirectory, identity, stateResult.Value!, input, cancellationToken).ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return SkillOperationResult<SkillUninstallResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
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
                    return SkillOperationResult<SkillUninstallResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
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
                    return SkillOperationResult<SkillUninstallResult>.FailureResult(deleteResult.Failure!.Code, deleteResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillUninstallResult>.Success(new SkillUninstallResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<SkillOperationResult<SkillUninstallActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input,
        CancellationToken cancellationToken)
    {
        switch (state.Kind)
        {
            case SkillTargetStateKind.Missing:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
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
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
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
                return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
                    ResolveStateFailureCode(state),
                    state.Failure?.Message ?? $"Target skill directory cannot be deleted: {skillDirectory}");
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<SkillOperationResult<SkillUninstallActionPlan>> CreateLocalModificationActionPlanAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
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
            return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
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

        return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory contains local modifications. Use --force to delete: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillUninstallActionPlan>> CreateDeleteActionPlanAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var fileChangesResult = await diffBuilder.BuildDeletionFileChangesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!fileChangesResult.IsSuccess)
        {
            return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
                fileChangesResult.Failure!.Code,
                fileChangesResult.Failure.Message);
        }

        return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
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

    private async ValueTask<SkillOperationResult<bool>> ValidateDeletePreconditionAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillActionTargetSnapshot? targetSnapshot,
        bool force,
        CancellationToken cancellationToken)
    {
        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
        }

        var state = stateResult.Value!;
        var isValid = SkillForceTargetStatePolicy.CanDelete(state.Kind, force);
        if (isValid)
        {
            return targetSnapshot is null
                ? SkillOperationResult<bool>.Success(true)
                : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, state, cancellationToken).ConfigureAwait(false);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private static SkillFailureCode ResolveChangedTargetFailureCode (SkillInstalledTargetState state)
    {
        return state.Kind == SkillTargetStateKind.Unmanaged
            ? SkillFailureCodes.InstallTargetUnmanaged
            : ResolveStateFailureCode(state);
    }

    private static SkillFailureCode ResolveStateFailureCode (SkillInstalledTargetState state)
    {
        return state.Failure?.Code ?? SkillFailureCodes.InstallTargetDigestMismatch;
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateTargetSnapshotAsync (
        string skillDirectory,
        SkillActionTargetSnapshot expectedSnapshot,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await diffBuilder.BuildTargetSnapshotAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!snapshotResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(snapshotResult.Failure!.Code, snapshotResult.Failure.Message);
        }

        if (snapshotResult.Value == expectedSnapshot)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private sealed class SkillUninstallActionPlan
    {
        public SkillUninstallActionPlan (
            SkillUninstallAction action,
            string skillDirectory,
            CanonicalSkillPackage package,
            bool shouldDelete,
            SkillActionTargetSnapshot? targetSnapshot)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
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

        public string SkillDirectory { get; }

        public CanonicalSkillPackage Package { get; }

        public bool ShouldDelete { get; }

        public SkillActionTargetSnapshot? TargetSnapshot { get; }
    }
}
