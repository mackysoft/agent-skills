using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Contracts;
using MackySoft.AgentSkills.Installation.Diffing;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.State;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Updates SKILL packages under a host target root. </summary>
public sealed class SkillUpdateService
{
    private readonly SkillInstallTargetResolver targetResolver;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillMaterializedPackageWriter packageWriter;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillUpdateService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageWriter"> The materialized package writer. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillUpdateService (
        SkillInstallTargetResolver targetResolver,
        SkillMaterializationService materializationService,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillMaterializedPackageWriter packageWriter,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageWriter = packageWriter ?? throw new ArgumentNullException(nameof(packageWriter));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Updates SKILL packages. </summary>
    /// <param name="input"> The update input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The update result or failure. </returns>
    public async ValueTask<SkillOperationResult<SkillUpdateResult>> UpdateAsync (
        SkillUpdateInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var targetRequest = input.TargetRequest;
        var targetResult = targetResolver.ResolveTarget(targetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillUpdateResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUpdateActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, skillName.Value);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
            }

            var actionPlanResult = await CreateActionPlanAsync(
                    package,
                    target.Host,
                    skillDirectory,
                    identity,
                    stateResult.Value!,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!actionPlanResult.IsSuccess)
            {
                return SkillOperationResult<SkillUpdateResult>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
            }

            actionPlans.Add(actionPlanResult.Value!);
        }

        if (!input.DryRun)
        {
            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (actionPlan.MaterializedPackage is null)
                {
                    continue;
                }

                var preconditionResult = await ValidateWritePreconditionAsync(
                        actionPlan.Package,
                        target.Host,
                        actionPlan.SkillDirectory,
                        actionPlan.Action.ActionKind,
                        actionPlan.TargetSnapshot,
                        input.Force,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUpdateResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            foreach (var actionPlan in actionPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (actionPlan.MaterializedPackage is null)
                {
                    continue;
                }

                var writeResult = await packageWriter.WriteAsync(
                        targetRoot,
                        actionPlan.SkillDirectory,
                        actionPlan.MaterializedPackage,
                        ResolveWriteMode(actionPlan.Action.ActionKind),
                        (directory, token) => ValidateWritePreconditionAsync(
                            actionPlan.Package,
                            target.Host,
                            directory,
                            actionPlan.Action.ActionKind,
                            actionPlan.TargetSnapshot,
                            input.Force,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!writeResult.IsSuccess)
                {
                    return SkillOperationResult<SkillUpdateResult>.FailureResult(writeResult.Failure!.Code, writeResult.Failure.Message);
                }
            }
        }

        return SkillOperationResult<SkillUpdateResult>.Success(new SkillUpdateResult(
            targetRoot,
            actionPlans.Select(static actionPlan => actionPlan.Action).ToArray(),
            input.DryRun,
            input.Force,
            input.PrintDiff));
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        switch (state.Kind)
        {
            case SkillTargetStateKind.Missing:
                return await CreateWriteActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.Created,
                        state,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillTargetStateKind.Current:
                return SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
                    new SkillUpdateAction(
                        identity,
                        SkillUpdateActionKind.NoOp,
                        SkillActionTargetStateProjection.Create(state),
                        blockedReason: null,
                        diffs: null,
                        fileChanges: null),
                    skillDirectory,
                    package,
                    null,
                    null));
            case SkillTargetStateKind.CleanOutdated:
                return await CreateWriteActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.Updated,
                        state,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillTargetStateKind.VersionAhead:
                return await CreateVersionAheadActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        state,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case var kind when SkillTargetStateClassifier.IsLocalModificationDrift(kind):
                return await CreateLocalModificationActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        state,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillTargetStateKind.Unmanaged:
                if (!input.DryRun)
                {
                    return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
                        SkillFailureCodes.InstallTargetUnmanaged,
                        $"Target skill directory is not managed by Agent Skills: {skillDirectory}");
                }

                return await CreateBlockedActionPlanAsync(
                        package,
                        host,
                        skillDirectory,
                        identity,
                        SkillUpdateActionKind.BlockedUnmanaged,
                        SkillBlockedReason.UnmanagedTarget,
                        false,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);
            case SkillTargetStateKind.NameCollision:
            case SkillTargetStateKind.HostConflict:
                return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
                    ResolveStateFailureCode(state),
                    state.Failure?.Message ?? $"Target skill directory cannot be updated: {skillDirectory}");
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateVersionAheadActionPlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        if (input.Force)
        {
            return await CreateWriteActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.Updated,
                    state,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (input.DryRun)
        {
            return await CreateBlockedActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.BlockedVersionAhead,
                    SkillBlockedReason.InstalledVersionAhead,
                    input.PrintDiff,
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory was generated from a newer SKILL bundle. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateLocalModificationActionPlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        if (input.Force)
        {
            return await CreateWriteActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.Updated,
                    state,
                    input,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (input.DryRun)
        {
            return await CreateBlockedActionPlanAsync(
                    package,
                    host,
                    skillDirectory,
                    identity,
                    SkillUpdateActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    input.PrintDiff,
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory contains local modifications. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateWriteActionPlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackageWritePlanAsync(package, host, skillDirectory, input.PrintDiff, cancellationToken).ConfigureAwait(false);
        if (!packagePlanResult.IsSuccess)
        {
            return SkillOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
        }

        var packagePlan = packagePlanResult.Value!;
        return SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
            new SkillUpdateAction(
                identity,
                actionKind,
                SkillActionTargetStateProjection.Create(state),
                null,
                packagePlan.Diffs,
                packagePlan.FileChanges),
            skillDirectory,
            package,
            packagePlan.MaterializedPackage,
            packagePlan.TargetSnapshot));
    }

    private async ValueTask<SkillOperationResult<SkillUpdateActionPlan>> CreateBlockedActionPlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillBlockedReason blockedReason,
        bool printDiff,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, printDiff, cancellationToken).ConfigureAwait(false);
        return packagePlanResult.IsSuccess
            ? SkillOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
                new SkillUpdateAction(
                    identity,
                    actionKind,
                    SkillActionTargetStateProjection.Create(state),
                    blockedReason,
                    packagePlanResult.Value!.Diffs,
                    fileChanges: null),
                skillDirectory,
                package,
                null,
                null))
            : SkillOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateWritePreconditionAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        SkillUpdateActionKind actionKind,
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
        var isValid = actionKind switch
        {
            SkillUpdateActionKind.Created => SkillForceTargetStatePolicy.CanCreate(state.Kind),
            SkillUpdateActionKind.Updated => SkillForceTargetStatePolicy.CanUpdateReplace(state.Kind, force),
            _ => true,
        };
        if (isValid)
        {
            return targetSnapshot is null
                ? SkillOperationResult<bool>.Success(true)
                : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, state, cancellationToken).ConfigureAwait(false);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to write: {skillDirectory}");
    }

    private static SkillMaterializedPackageWriteMode ResolveWriteMode (SkillUpdateActionKind actionKind)
    {
        return actionKind switch
        {
            SkillUpdateActionKind.Created => SkillMaterializedPackageWriteMode.CreateNew,
            SkillUpdateActionKind.Updated => SkillMaterializedPackageWriteMode.ReplaceExisting,
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), actionKind, "Action does not write a materialized package."),
        };
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
            $"Target skill directory changed after planning; refusing to write: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<SkillMaterializedPackagePlan>> CreateMaterializedPackagePlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackagePlan>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var diffResult = await diffBuilder.BuildOptionalAsync(skillDirectory, materializedResult.Value!, printDiff, cancellationToken).ConfigureAwait(false);
        return diffResult.IsSuccess
            ? SkillOperationResult<SkillMaterializedPackagePlan>.Success(new SkillMaterializedPackagePlan(materializedResult.Value!, diffResult.Value!))
            : SkillOperationResult<SkillMaterializedPackagePlan>.FailureResult(diffResult.Failure!.Code, diffResult.Failure.Message);
    }

    private async ValueTask<SkillOperationResult<SkillMaterializedPackageWritePlan>> CreateMaterializedPackageWritePlanAsync (
        CanonicalSkillPackage package,
        SkillHostKind host,
        string skillDirectory,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackageWritePlan>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var changePlanResult = await diffBuilder.BuildReplacementPlanAsync(skillDirectory, materializedResult.Value!, printDiff, cancellationToken).ConfigureAwait(false);
        if (!changePlanResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackageWritePlan>.FailureResult(changePlanResult.Failure!.Code, changePlanResult.Failure.Message);
        }

        var changePlan = changePlanResult.Value!;
        return SkillOperationResult<SkillMaterializedPackageWritePlan>.Success(new SkillMaterializedPackageWritePlan(
            materializedResult.Value!,
            changePlan.Diffs,
            changePlan.FileChanges.FileChanges,
            changePlan.FileChanges.TargetSnapshot));
    }

    private sealed class SkillMaterializedPackagePlan
    {
        public SkillMaterializedPackagePlan (
            SkillMaterializedPackage materializedPackage,
            IReadOnlyList<SkillActionDiff> diffs)
        {
            MaterializedPackage = materializedPackage ?? throw new ArgumentNullException(nameof(materializedPackage));
            Diffs = SkillActionContractGuard.Snapshot(diffs, nameof(diffs));
        }

        public SkillMaterializedPackage MaterializedPackage { get; }

        public IReadOnlyList<SkillActionDiff> Diffs { get; }
    }

    private sealed class SkillMaterializedPackageWritePlan
    {
        public SkillMaterializedPackageWritePlan (
            SkillMaterializedPackage materializedPackage,
            IReadOnlyList<SkillActionDiff> diffs,
            SkillActionFileChanges fileChanges,
            SkillActionTargetSnapshot targetSnapshot)
        {
            MaterializedPackage = materializedPackage ?? throw new ArgumentNullException(nameof(materializedPackage));
            Diffs = SkillActionContractGuard.Snapshot(diffs, nameof(diffs));
            FileChanges = fileChanges ?? throw new ArgumentNullException(nameof(fileChanges));
            TargetSnapshot = targetSnapshot ?? throw new ArgumentNullException(nameof(targetSnapshot));
        }

        public SkillMaterializedPackage MaterializedPackage { get; }

        public IReadOnlyList<SkillActionDiff> Diffs { get; }

        public SkillActionFileChanges FileChanges { get; }

        public SkillActionTargetSnapshot TargetSnapshot { get; }
    }

    private sealed class SkillUpdateActionPlan
    {
        public SkillUpdateActionPlan (
            SkillUpdateAction action,
            string skillDirectory,
            CanonicalSkillPackage package,
            SkillMaterializedPackage? materializedPackage,
            SkillActionTargetSnapshot? targetSnapshot)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
            if ((materializedPackage is null) != (targetSnapshot is null))
            {
                throw new ArgumentException("A materialized package and its target snapshot must be provided together.", nameof(targetSnapshot));
            }

            SkillDirectory = skillDirectory;
            Package = package ?? throw new ArgumentNullException(nameof(package));
            MaterializedPackage = materializedPackage;
            TargetSnapshot = targetSnapshot;
        }

        public SkillUpdateAction Action { get; }

        public string SkillDirectory { get; }

        public CanonicalSkillPackage Package { get; }

        public SkillMaterializedPackage? MaterializedPackage { get; }

        public SkillActionTargetSnapshot? TargetSnapshot { get; }
    }
}
