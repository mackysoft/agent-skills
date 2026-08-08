using MackySoft.AgentSkills.Agents.Installation.Requests;
using MackySoft.AgentSkills.Agents.Installation.Results;
using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

/// <summary> Uninstalls only selected custom agents and never removes their SKILL dependencies. </summary>
public sealed class AgentUninstallService
{
    private readonly AgentInstallTargetResolver targetResolver;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly AgentInstallationStatePathResolver statePathResolver;
    private readonly AgentInstallationStateStore stateStore;
    private readonly AgentManagedArtifactStore artifactStore;

    /// <summary> Initializes a custom-agent uninstall service. </summary>
    public AgentUninstallService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        this.statePathResolver = statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        artifactStore = new AgentManagedArtifactStore(statePathResolver, stateStore);
    }

    /// <summary> Removes the selected managed agents without invoking any SKILL removal service. </summary>
    public async ValueTask<SkillOperationResult<AgentUninstallResult>> UninstallAsync (
        AgentUninstallInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var targetResult = targetResolver.ResolveTarget(input.AgentTargetRequest);
        if (!targetResult.IsSuccess)
        {
            return Failure(targetResult.Failure!);
        }

        var target = targetResult.Value!;
        var plansResult = await CreatePlansAsync(input.Catalog.SelectedAgents, target, input.Force, cancellationToken).ConfigureAwait(false);
        if (!plansResult.IsSuccess)
        {
            return Failure(plansResult.Failure!);
        }

        var plans = plansResult.Value!;
        if (!input.DryRun)
        {
            var blocked = plans.FirstOrDefault(static plan => plan.Action.IsBlocked);
            if (blocked is not null)
            {
                return SkillOperationResult<AgentUninstallResult>.FailureResult(
                    ResolveFailureCode(blocked.TargetState.Kind),
                    $"Custom agent '{blocked.Action.AgentName.Value}' cannot be uninstalled from target state '{blocked.TargetState.Kind}'.");
            }

            var preconditionResult = await ValidatePreconditionsAsync(plans, target, cancellationToken).ConfigureAwait(false);
            if (!preconditionResult.IsSuccess)
            {
                return Failure(preconditionResult.Failure!);
            }

            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (plan.Action.ActionKind != AgentRemovalActionKind.Deleted)
                {
                    continue;
                }

                var deleteResult = await artifactStore.DeleteAsync(plan.State!, plan.StatePath!, target, cancellationToken).ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return Failure(deleteResult.Failure!);
                }
            }
        }

        return SkillOperationResult<AgentUninstallResult>.Success(new AgentUninstallResult(
            target.ArtifactRoot,
            target.StateRoot,
            plans.Select(static plan => plan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>> CreatePlansAsync (
        IReadOnlyList<CanonicalAgentPackage> packages,
        AgentResolvedTarget target,
        bool force,
        CancellationToken cancellationToken)
    {
        var plans = new List<AgentRemovalPlan>(packages.Count);
        var managedPaths = new Dictionary<string, AgentName>(StringComparer.Ordinal);
        foreach (var package in packages.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inspectedResult = await targetInspector.InspectAsync(package.Manifest, target, cancellationToken).ConfigureAwait(false);
            if (!inspectedResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(inspectedResult.Failure!.Code, inspectedResult.Failure.Message);
            }

            var statePathResult = statePathResolver.Resolve(target, package.Manifest.CatalogId, package.Manifest.AgentName);
            if (!statePathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(statePathResult.Failure!.Code, statePathResult.Failure.Message);
            }

            var stateReadResult = await stateStore.ReadAsync(statePathResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!stateReadResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(stateReadResult.Failure!.Code, stateReadResult.Failure.Message);
            }

            var state = stateReadResult.Value!.State;
            if (state is not null)
            {
                foreach (var artifact in state.ManagedArtifacts)
                {
                    if (managedPaths.TryGetValue(artifact.Path, out var owner))
                    {
                        return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(
                            SkillFailureCodes.ManifestInvalid,
                            $"Managed agent states for '{owner.Value}' and '{state.AgentName.Value}' own the same artifact: {artifact.Path}.");
                    }

                    managedPaths.Add(artifact.Path, state.AgentName);
                }
            }

            var targetState = inspectedResult.Value!;
            var actionKind = targetState.Kind switch
            {
                AgentInstalledTargetStateKind.Missing => AgentRemovalActionKind.NoOp,
                AgentInstalledTargetStateKind.Current or AgentInstalledTargetStateKind.CleanOutdated => AgentRemovalActionKind.Deleted,
                AgentInstalledTargetStateKind.LocallyModified when force => AgentRemovalActionKind.Deleted,
                AgentInstalledTargetStateKind.LocallyModified => AgentRemovalActionKind.BlockedLocalModification,
                AgentInstalledTargetStateKind.Unmanaged => AgentRemovalActionKind.BlockedUnmanaged,
                AgentInstalledTargetStateKind.OtherCatalog => AgentRemovalActionKind.BlockedForeignCatalog,
                _ => AgentRemovalActionKind.BlockedInvalid,
            };
            plans.Add(new AgentRemovalPlan(
                state,
                state is null ? null : statePathResult.Value!,
                targetState,
                new AgentRemovalAction(package.Manifest.AgentName, actionKind, targetState.Kind, targetState.Detail)));
        }

        return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.Success(Array.AsReadOnly(plans.ToArray()));
    }

    private async ValueTask<SkillOperationResult<bool>> ValidatePreconditionsAsync (
        IReadOnlyList<AgentRemovalPlan> plans,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans.Where(static plan => plan.State is not null))
        {
            var result = await targetInspector.InspectOwnedStateAsync(plan.State!, target, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(result.Failure!.Code, result.Failure.Message);
            }

            var expectedKind = plan.TargetState.Kind == AgentInstalledTargetStateKind.CleanOutdated
                ? AgentInstalledTargetStateKind.Current
                : plan.TargetState.Kind;
            if (result.Value!.Kind != expectedKind || !string.Equals(result.Value.Detail, plan.TargetState.Detail, StringComparison.Ordinal))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetWriteFailed,
                    $"Custom-agent target changed after planning: {plan.Action.AgentName.Value}.");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillFailureCode ResolveFailureCode (AgentInstalledTargetStateKind kind)
    {
        return kind switch
        {
            AgentInstalledTargetStateKind.LocallyModified => SkillFailureCodes.InstallTargetLocalModification,
            AgentInstalledTargetStateKind.Unmanaged or AgentInstalledTargetStateKind.OtherCatalog => SkillFailureCodes.InstallTargetUnmanaged,
            _ => SkillFailureCodes.ManifestInvalid,
        };
    }

    private static SkillOperationResult<AgentUninstallResult> Failure (SkillFailure failure)
    {
        return SkillOperationResult<AgentUninstallResult>.FailureResult(failure.Code, failure.Message);
    }
}
