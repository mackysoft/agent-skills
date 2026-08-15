using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Installation.Diffing;
using MackySoft.AgentDistribution.Installation.Inventory;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.State;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Services;

/// <summary> Updates SKILL packages under a bundle target root. </summary>
public sealed class SkillUpdateService
{
    private readonly SkillCatalogTargetRootSelector targetSelector;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillInstalledTargetStateAnalyzer targetStateAnalyzer;
    private readonly ISkillMaterializedPackageWriter packageWriter;
    private readonly SkillMaterializedPackageDiffBuilder diffBuilder;

    /// <summary> Initializes a new instance of the <see cref="SkillUpdateService" /> class. </summary>
    /// <param name="targetSelector"> The installed-catalog-aware target selector. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageWriter"> The materialized package writer. </param>
    /// <param name="diffBuilder"> The structured diff builder. </param>
    public SkillUpdateService (
        SkillCatalogTargetRootSelector targetSelector,
        SkillMaterializationService materializationService,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillMaterializedPackageWriter packageWriter,
        SkillMaterializedPackageDiffBuilder diffBuilder)
    {
        this.targetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageWriter = packageWriter ?? throw new ArgumentNullException(nameof(packageWriter));
        this.diffBuilder = diffBuilder ?? throw new ArgumentNullException(nameof(diffBuilder));
    }

    /// <summary> Updates SKILL packages. </summary>
    /// <param name="input"> The update input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The update result or failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillUpdateResult>> UpdateAsync (
        SkillUpdateInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var planResult = await PlanAsync(input, cancellationToken).ConfigureAwait(false);
        if (!planResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillUpdateResult>.FailureResult(planResult.Failure!.Code, planResult.Failure.Message);
        }

        return input.DryRun
            ? AgentDistributionOperationResult<SkillUpdateResult>.Success(planResult.Value!.CreateResult(dryRun: true))
            : await ApplyAsync(planResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Creates the complete SKILL update plan without writing package files. </summary>
    internal async ValueTask<AgentDistributionOperationResult<SkillUpdatePlan>> PlanAsync (
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var targetRequest = input.TargetRequest;
        var targetResult = await targetSelector.SelectTargetAsync(
                targetRequest,
                input.CatalogId,
                input.Packages.Select(static package => package.Manifest.SkillName).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillUpdatePlan>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUpdateActionPlan>();
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
                return AgentDistributionOperationResult<SkillUpdatePlan>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            var identity = new SkillInstallIdentity(target.Host, targetRequest.Scope, targetRoot, skillName);
            var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, target.Host, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillUpdatePlan>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
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
                return AgentDistributionOperationResult<SkillUpdatePlan>.FailureResult(actionPlanResult.Failure!.Code, actionPlanResult.Failure.Message);
            }

            actionPlans.Add(actionPlanResult.Value!);
        }

        return AgentDistributionOperationResult<SkillUpdatePlan>.Success(new SkillUpdatePlan(input, target, actionPlans));
    }

    /// <summary> Applies a previously created SKILL update plan without resolving the target or rebuilding actions. </summary>
    internal async ValueTask<AgentDistributionOperationResult<SkillUpdateResult>> ApplyAsync (
        SkillUpdatePlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();

        var input = plan.Input;
        var target = plan.Target;
        var targetRoot = target.TargetRoot;
        foreach (var actionPlan in plan.ActionPlans)
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
                return AgentDistributionOperationResult<SkillUpdateResult>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
            }
        }

        foreach (var actionPlan in plan.ActionPlans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (actionPlan.MaterializedPackage is null)
            {
                continue;
            }

            var writeResult = await packageWriter.WriteAsync(
                    new SkillMaterializedPackageWriteRequest(
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
                            token)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                return AgentDistributionOperationResult<SkillUpdateResult>.FailureResult(writeResult.Failure!.Code, writeResult.Failure.Message);
            }
        }

        return AgentDistributionOperationResult<SkillUpdateResult>.Success(plan.CreateResult(dryRun: false));
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUpdateActionPlan>> CreateActionPlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
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
                return AgentDistributionOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
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
                    return AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(
                        AgentDistributionFailureCodes.InstallTargetUnmanaged,
                        $"Target skill directory is not managed by Agent Distribution: {skillDirectory}");
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
                return AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(
                    ResolveStateFailureCode(state),
                    state.Failure?.Message ?? $"Target skill directory cannot be updated: {skillDirectory}");
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUpdateActionPlan>> CreateVersionAheadActionPlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
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

        return AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory was generated from a newer SKILL bundle. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUpdateActionPlan>> CreateLocalModificationActionPlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
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

        return AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory contains local modifications. Use --force to overwrite: {skillDirectory}");
    }

    private async ValueTask<AgentDistributionOperationResult<SkillUpdateActionPlan>> CreateWriteActionPlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillInstalledTargetState state,
        SkillUpdateInput input,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackageWritePlanAsync(package, host, skillDirectory, input.PrintDiff, cancellationToken).ConfigureAwait(false);
        if (!packagePlanResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
        }

        var packagePlan = packagePlanResult.Value!;
        return AgentDistributionOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
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

    private async ValueTask<AgentDistributionOperationResult<SkillUpdateActionPlan>> CreateBlockedActionPlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        SkillInstallIdentity identity,
        SkillUpdateActionKind actionKind,
        SkillBlockedReason blockedReason,
        bool printDiff,
        SkillInstalledTargetState state,
        CancellationToken cancellationToken)
    {
        var packagePlanResult = await CreateMaterializedPackagePlanAsync(package, host, skillDirectory, printDiff, cancellationToken).ConfigureAwait(false);
        return packagePlanResult.IsSuccess
            ? AgentDistributionOperationResult<SkillUpdateActionPlan>.Success(new SkillUpdateActionPlan(
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
            : AgentDistributionOperationResult<SkillUpdateActionPlan>.FailureResult(packagePlanResult.Failure!.Code, packagePlanResult.Failure.Message);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateWritePreconditionAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        SkillUpdateActionKind actionKind,
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
        var isValid = actionKind switch
        {
            SkillUpdateActionKind.Created => SkillForceTargetStatePolicy.CanCreate(state.Kind),
            SkillUpdateActionKind.Updated => SkillForceTargetStatePolicy.CanUpdateReplace(state.Kind, force),
            _ => true,
        };
        if (isValid)
        {
            return targetSnapshot is null
                ? AgentDistributionOperationResult<bool>.Success(true)
                : await ValidateTargetSnapshotAsync(skillDirectory, targetSnapshot, state, cancellationToken).ConfigureAwait(false);
        }

        return AgentDistributionOperationResult<bool>.FailureResult(
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
            $"Target skill directory changed after planning; refusing to write: {skillDirectory}");
    }

    private async ValueTask<AgentDistributionOperationResult<SkillMaterializedPackagePlan>> CreateMaterializedPackagePlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillMaterializedPackagePlan>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var diffResult = await diffBuilder.BuildOptionalAsync(skillDirectory, materializedResult.Value!, printDiff, cancellationToken).ConfigureAwait(false);
        return diffResult.IsSuccess
            ? AgentDistributionOperationResult<SkillMaterializedPackagePlan>.Success(new SkillMaterializedPackagePlan(materializedResult.Value!, diffResult.Value!))
            : AgentDistributionOperationResult<SkillMaterializedPackagePlan>.FailureResult(diffResult.Failure!.Code, diffResult.Failure.Message);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillMaterializedPackageWritePlan>> CreateMaterializedPackageWritePlanAsync (
        CanonicalSkillPackage package,
        HostKind host,
        AbsolutePath skillDirectory,
        bool printDiff,
        CancellationToken cancellationToken)
    {
        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillMaterializedPackageWritePlan>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var changePlanResult = await diffBuilder.BuildReplacementPlanAsync(skillDirectory, materializedResult.Value!, printDiff, cancellationToken).ConfigureAwait(false);
        if (!changePlanResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillMaterializedPackageWritePlan>.FailureResult(changePlanResult.Failure!.Code, changePlanResult.Failure.Message);
        }

        var changePlan = changePlanResult.Value!;
        return AgentDistributionOperationResult<SkillMaterializedPackageWritePlan>.Success(new SkillMaterializedPackageWritePlan(
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

}
