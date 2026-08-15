using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

/// <summary> Prunes clean managed agents removed from the current catalog without removing SKILL packages. </summary>
public sealed class AgentPruneService
{
    private readonly AgentInstallTargetResolver targetResolver;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly AgentInstallationStateStore stateStore;
    private readonly IAgentManagedArtifactStore artifactStore;

    /// <summary> Initializes a custom-agent prune service. </summary>
    public AgentPruneService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore)
        : this(
            targetResolver,
            targetInspector,
            stateStore,
            new AgentManagedArtifactStore(
                statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver)),
                stateStore ?? throw new ArgumentNullException(nameof(stateStore))))
    {
    }

    internal AgentPruneService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        AgentInstallationStateStore stateStore,
        IAgentManagedArtifactStore artifactStore)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
    }

    /// <summary> Deletes only same-catalog orphan agents whose managed artifacts are clean, unless force allows local drift. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentPruneResult>> PruneAsync (
        AgentPruneInput input,
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
        var catalogDirectoryResult = AgentPathGuard.Validate(ContainedPath.Create(
            target.StateRoot,
            RootRelativePath.Parse(input.CurrentCatalog.CatalogId.Value)));
        if (!catalogDirectoryResult.IsSuccess)
        {
            return Failure(catalogDirectoryResult.Failure!);
        }

        if (!Directory.Exists(catalogDirectoryResult.Value!.Value))
        {
            return AgentDistributionOperationResult<AgentPruneResult>.Success(new AgentPruneResult(
                target.ArtifactRoot,
                target.StateRoot,
                [],
                input.DryRun,
                input.Force));
        }

        var plansResult = await CreatePlansAsync(input, target, catalogDirectoryResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!plansResult.IsSuccess)
        {
            return Failure(plansResult.Failure!);
        }

        var plans = plansResult.Value!;
        if (!input.DryRun)
        {
            var preconditionResult = await ValidateDeletionPreconditionsAsync(plans, target, cancellationToken).ConfigureAwait(false);
            if (!preconditionResult.IsSuccess)
            {
                return Failure(preconditionResult.Failure!);
            }

            var deleteResult = await DeletePlannedArtifactsAsync(plans, target, cancellationToken).ConfigureAwait(false);
            if (!deleteResult.IsSuccess)
            {
                return Failure(deleteResult.Failure!);
            }
        }

        return AgentDistributionOperationResult<AgentPruneResult>.Success(new AgentPruneResult(
            target.ArtifactRoot,
            target.StateRoot,
            plans.Select(static plan => plan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>> CreatePlansAsync (
        AgentPruneInput input,
        AgentResolvedTarget target,
        AbsolutePath catalogDirectory,
        CancellationToken cancellationToken)
    {
        var currentNames = input.CurrentCatalog.SelectedAgents.Select(static agent => agent.Manifest.AgentName).ToHashSet();
        var planningContext = new AgentPrunePlanningContext(
            input.CurrentCatalog.CatalogId,
            currentNames,
            target,
            input.Force);
        var plans = new List<AgentRemovalPlan>();
        var managedPaths = new Dictionary<PackageRelativePath, AgentName>(PackageRelativePath.PortableFileSystemComparer);
        IEnumerable<AbsolutePath> statePaths;
        try
        {
            statePaths = Directory.EnumerateFiles(catalogDirectory.Value, "*.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(AbsolutePath.Parse)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetReadFailed,
                $"Could not enumerate installed agent state: {exception.Message}");
        }

        foreach (var statePath in statePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readResult = await stateStore.ReadAsync(statePath, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(readResult.Failure!.Code, readResult.Failure.Message);
            }

            var state = readResult.Value!.State;
            if (state is null)
            {
                continue;
            }

            foreach (var artifact in state.ManagedArtifacts)
            {
                if (managedPaths.TryGetValue(artifact.Path, out var owner))
                {
                    return AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(
                        AgentDistributionFailureCodes.ManifestInvalid,
                        $"Managed agent states for '{owner.Value}' and '{state.AgentName.Value}' own the same artifact: {artifact.Path}.");
                }

                managedPaths.Add(artifact.Path, state.AgentName);
            }

            if (!MatchesSelection(state, input))
            {
                continue;
            }

            var planResult = await CreatePlanAsync(
                    state,
                    statePath,
                    planningContext,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!planResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(planResult.Failure!.Code, planResult.Failure.Message);
            }

            plans.Add(planResult.Value!);
        }

        return AgentDistributionOperationResult<IReadOnlyList<AgentRemovalPlan>>.Success(Array.AsReadOnly(plans.ToArray()));
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> DeletePlannedArtifactsAsync (
        IReadOnlyList<AgentRemovalPlan> plans,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        var deletionPlans = plans.Where(static plan => plan.Action.ActionKind == AgentRemovalActionKind.Deleted).ToArray();
        for (var index = 0; index < deletionPlans.Length; index++)
        {
            var plan = deletionPlans[index];
            var remainingPlans = deletionPlans[index..];
            var deleteResult = await artifactStore.DeleteAsync(
                    plan.State!,
                    plan.StatePath!,
                    target,
                    preconditionCancellationToken => ValidateDeletionPreconditionsAsync(remainingPlans, target, preconditionCancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deleteResult.IsSuccess)
            {
                return deleteResult;
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateDeletionPreconditionsAsync (
        IReadOnlyList<AgentRemovalPlan> plans,
        AgentResolvedTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(target);
        foreach (var plan in plans.Where(static plan => plan.Action.ActionKind == AgentRemovalActionKind.Deleted))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preconditionResult = await ValidateDeletionPreconditionAsync(plan, target, cancellationToken).ConfigureAwait(false);
            if (!preconditionResult.IsSuccess)
            {
                return preconditionResult;
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateDeletionPreconditionAsync (
        AgentRemovalPlan plan,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        var inspectedResult = await targetInspector.InspectOwnedStateAsync(plan.State!, target, cancellationToken).ConfigureAwait(false);
        if (!inspectedResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(inspectedResult.Failure!.Code, inspectedResult.Failure.Message);
        }

        return inspectedResult.Value!.Kind == plan.TargetState.Kind && string.Equals(inspectedResult.Value.Detail, plan.TargetState.Detail, StringComparison.Ordinal)
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Custom-agent target changed after prune planning: {plan.Action.AgentName.Value}.");
    }

    private async ValueTask<AgentDistributionOperationResult<AgentRemovalPlan>> CreatePlanAsync (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentPrunePlanningContext planningContext,
        CancellationToken cancellationToken)
    {
        var invalidStatePathPlan = TryCreateInvalidStatePathPlan(state, statePath);
        if (invalidStatePathPlan is not null)
        {
            return AgentDistributionOperationResult<AgentRemovalPlan>.Success(invalidStatePathPlan);
        }

        var currentCatalogPlan = TryCreateCurrentCatalogPlan(state, statePath, planningContext);
        if (currentCatalogPlan is not null)
        {
            return AgentDistributionOperationResult<AgentRemovalPlan>.Success(currentCatalogPlan);
        }

        return await CreateOrphanPlanAsync(state, statePath, planningContext, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<AgentRemovalPlan>> CreateOrphanPlanAsync (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentPrunePlanningContext planningContext,
        CancellationToken cancellationToken)
    {
        var targetStateResult = await ClassifyTargetStateAsync(
                state,
                planningContext,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetStateResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentRemovalPlan>.FailureResult(
                targetStateResult.Failure!.Code,
                targetStateResult.Failure.Message);
        }

        var targetState = targetStateResult.Value!;
        return AgentDistributionOperationResult<AgentRemovalPlan>.Success(CreatePlan(
            state,
            statePath,
            targetState,
            ResolveActionKind(targetState.Kind, planningContext.Force)));
    }

    private static AgentRemovalPlan? TryCreateInvalidStatePathPlan (AgentInstallationState state, AbsolutePath statePath)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(statePath.Value), state.AgentName.Value, StringComparison.Ordinal)
            ? null
            : CreatePlan(
                state,
                statePath,
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Ownership-state file name does not match its agent name."),
                AgentRemovalActionKind.BlockedInvalid);
    }

    private static AgentRemovalPlan? TryCreateCurrentCatalogPlan (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentPrunePlanningContext planningContext)
    {
        return state.CatalogId == planningContext.CurrentCatalogId && planningContext.CurrentAgentNames.Contains(state.AgentName)
            ? CreatePlan(
                state,
                statePath,
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current),
                AgentRemovalActionKind.SkippedCurrent)
            : null;
    }

    private async ValueTask<AgentDistributionOperationResult<AgentInstalledTargetState>> ClassifyTargetStateAsync (
        AgentInstallationState state,
        AgentPrunePlanningContext planningContext,
        CancellationToken cancellationToken)
    {
        if (state.CatalogId != planningContext.CurrentCatalogId)
        {
            return AgentDistributionOperationResult<AgentInstalledTargetState>.Success(
                new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog));
        }

        return await targetInspector.InspectOwnedStateAsync(state, planningContext.Target, cancellationToken).ConfigureAwait(false);
    }

    private static AgentRemovalPlan CreatePlan (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentInstalledTargetState targetState,
        AgentRemovalActionKind actionKind)
    {
        return new AgentRemovalPlan(
            state,
            statePath,
            targetState,
            new AgentRemovalAction(state.AgentName, actionKind, targetState.Kind, targetState.Detail));
    }

    private static AgentRemovalActionKind ResolveActionKind (AgentInstalledTargetStateKind targetStateKind, bool force)
    {
        return targetStateKind switch
        {
            AgentInstalledTargetStateKind.Current => AgentRemovalActionKind.Deleted,
            AgentInstalledTargetStateKind.LocallyModified when force => AgentRemovalActionKind.Deleted,
            AgentInstalledTargetStateKind.LocallyModified => AgentRemovalActionKind.BlockedLocalModification,
            AgentInstalledTargetStateKind.OtherCatalog => AgentRemovalActionKind.BlockedForeignCatalog,
            AgentInstalledTargetStateKind.Unmanaged => AgentRemovalActionKind.BlockedUnmanaged,
            _ => AgentRemovalActionKind.BlockedInvalid,
        };
    }

    private static bool MatchesSelection (AgentInstallationState state, AgentPruneInput input)
    {
        return input.SelectedAgentNames.Count == 0 || input.SelectedAgentNames.Contains(state.AgentName);
    }

    private static AgentDistributionOperationResult<AgentPruneResult> Failure (AgentDistributionFailure failure)
    {
        return AgentDistributionOperationResult<AgentPruneResult>.FailureResult(failure.Code, failure.Message);
    }

    private sealed class AgentPrunePlanningContext
    {
        public AgentPrunePlanningContext (
            AgentDistributionCatalogId currentCatalogId,
            IReadOnlySet<AgentName> currentAgentNames,
            AgentResolvedTarget target,
            bool force)
        {
            ArgumentNullException.ThrowIfNull(currentCatalogId);
            ArgumentNullException.ThrowIfNull(currentAgentNames);
            CurrentCatalogId = currentCatalogId;
            CurrentAgentNames = currentAgentNames;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Force = force;
        }

        public AgentDistributionCatalogId CurrentCatalogId { get; }

        public IReadOnlySet<AgentName> CurrentAgentNames { get; }

        public AgentResolvedTarget Target { get; }

        public bool Force { get; }
    }
}
