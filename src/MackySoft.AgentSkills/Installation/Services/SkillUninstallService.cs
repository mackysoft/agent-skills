using MackySoft.AgentSkills.Installation.Contracts;
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

    /// <summary> Initializes a new instance of the <see cref="SkillUninstallService" /> class. </summary>
    /// <param name="targetResolver"> The target resolver. </param>
    /// <param name="targetStateAnalyzer"> The installed target state analyzer. </param>
    /// <param name="packageRemover"> The installed package remover. </param>
    public SkillUninstallService (
        SkillInstallTargetResolver targetResolver,
        SkillInstalledTargetStateAnalyzer targetStateAnalyzer,
        ISkillInstalledPackageRemover packageRemover)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetStateAnalyzer = targetStateAnalyzer ?? throw new ArgumentNullException(nameof(targetStateAnalyzer));
        this.packageRemover = packageRemover ?? throw new ArgumentNullException(nameof(packageRemover));
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
        ArgumentNullException.ThrowIfNull(input.Packages);
        ArgumentNullException.ThrowIfNull(input.TargetRequest);

        var targetRequest = input.TargetRequest;
        var targetResult = targetResolver.ResolveTarget(targetRequest);
        if (!targetResult.IsSuccess)
        {
            return SkillOperationResult<SkillUninstallResult>.FailureResult(targetResult.Failure!.Code, targetResult.Failure.Message);
        }

        var target = targetResult.Value!;
        var targetRoot = target.TargetRoot;
        var actionPlans = new List<SkillUninstallActionPlan>();
        foreach (var package in input.Packages.OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillName = package.Manifest.SkillName;
            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(targetRoot, skillName);
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

            var actionPlanResult = CreateActionPlan(package, skillDirectory, identity, stateResult.Value!, input);
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

    private static SkillOperationResult<SkillUninstallActionPlan> CreateActionPlan (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input)
    {
        switch (state.Kind)
        {
            case SkillInstalledTargetStateKind.Missing:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.NoOp, TargetState: SkillActionTargetStateProjection.Create(state)),
                    skillDirectory,
                    package,
                    ShouldDelete: false));
            case SkillInstalledTargetStateKind.Current:
            case SkillInstalledTargetStateKind.CleanOutdated:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.Deleted, TargetState: SkillActionTargetStateProjection.Create(state)),
                    skillDirectory,
                    package,
                    ShouldDelete: true));
            case SkillInstalledTargetStateKind.Unmanaged:
                return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                    new SkillUninstallAction(identity, SkillUninstallActionKind.SkippedUnmanaged, TargetState: SkillActionTargetStateProjection.Create(state)),
                    skillDirectory,
                    package,
                    ShouldDelete: false));
            case var kind when kind.IsLocalModificationDrift():
                return CreateLocalModificationActionPlan(package, skillDirectory, identity, state, input);
            case SkillInstalledTargetStateKind.NameCollision:
            case SkillInstalledTargetStateKind.HostConflict:
                return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
                    ResolveStateFailureCode(state),
                    state.Failure?.Message ?? $"Target skill directory cannot be deleted: {skillDirectory}");
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "Unsupported target state.");
        }
    }

    private static SkillOperationResult<SkillUninstallActionPlan> CreateLocalModificationActionPlan (
        CanonicalSkillPackage package,
        string skillDirectory,
        SkillInstallIdentity identity,
        SkillInstalledTargetState state,
        SkillUninstallInput input)
    {
        if (input.Force)
        {
            return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                new SkillUninstallAction(identity, SkillUninstallActionKind.Deleted, TargetState: SkillActionTargetStateProjection.Create(state)),
                skillDirectory,
                package,
                ShouldDelete: true));
        }

        if (input.DryRun)
        {
            return SkillOperationResult<SkillUninstallActionPlan>.Success(new SkillUninstallActionPlan(
                new SkillUninstallAction(
                    identity,
                    SkillUninstallActionKind.BlockedLocalModification,
                    SkillBlockedReason.LocalModificationRequiresForce,
                    SkillActionTargetStateProjection.Create(state)),
                skillDirectory,
                package,
                ShouldDelete: false));
        }

        return SkillOperationResult<SkillUninstallActionPlan>.FailureResult(
            ResolveStateFailureCode(state),
            $"Target skill directory contains local modifications. Use --force to delete: {skillDirectory}");
    }

    private async ValueTask<SkillOperationResult<bool>> ValidateDeletePreconditionAsync (
        CanonicalSkillPackage package,
        string host,
        string skillDirectory,
        bool force,
        CancellationToken cancellationToken)
    {
        var stateResult = await targetStateAnalyzer.AnalyzeAsync(package, skillDirectory, host, cancellationToken).ConfigureAwait(false);
        if (!stateResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(stateResult.Failure!.Code, stateResult.Failure.Message);
        }

        var state = stateResult.Value!;
        var isValid = force
            ? state.Kind is SkillInstalledTargetStateKind.Current or SkillInstalledTargetStateKind.CleanOutdated || state.Kind.IsLocalModificationDrift()
            : state.Kind is SkillInstalledTargetStateKind.Current or SkillInstalledTargetStateKind.CleanOutdated;
        if (isValid)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        return SkillOperationResult<bool>.FailureResult(
            ResolveChangedTargetFailureCode(state),
            $"Target skill directory changed after planning; refusing to delete: {skillDirectory}");
    }

    private static SkillFailureCode ResolveChangedTargetFailureCode (SkillInstalledTargetState state)
    {
        return state.Kind == SkillInstalledTargetStateKind.Unmanaged
            ? SkillFailureCodes.InstallTargetUnmanaged
            : ResolveStateFailureCode(state);
    }

    private static SkillFailureCode ResolveStateFailureCode (SkillInstalledTargetState state)
    {
        return state.Failure?.Code ?? SkillFailureCodes.InstallTargetDigestMismatch;
    }

    private sealed record SkillUninstallActionPlan (
        SkillUninstallAction Action,
        string SkillDirectory,
        CanonicalSkillPackage Package,
        bool ShouldDelete);
}
