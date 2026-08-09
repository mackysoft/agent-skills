using MackySoft.AgentSkills.Agents.Installation.Requests;
using MackySoft.AgentSkills.Agents.Installation.Results;
using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

/// <summary> Prunes clean managed agents removed from the current catalog without removing SKILL packages. </summary>
public sealed class AgentPruneService
{
    private readonly AgentInstallTargetResolver targetResolver;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly AgentInstallationStateStore stateStore;
    private readonly AgentManagedArtifactStore artifactStore;

    /// <summary> Initializes a custom-agent prune service. </summary>
    public AgentPruneService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        artifactStore = new AgentManagedArtifactStore(
            statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver)),
            stateStore);
    }

    /// <summary> Deletes only same-catalog orphan agents whose managed artifacts are clean, unless force allows local drift. </summary>
    public async ValueTask<SkillOperationResult<AgentPruneResult>> PruneAsync (
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
            RootRelativePath.Parse(input.CurrentCatalog.BundleDescriptor.CatalogId.Value)));
        if (!catalogDirectoryResult.IsSuccess)
        {
            return Failure(catalogDirectoryResult.Failure!);
        }

        if (!Directory.Exists(catalogDirectoryResult.Value!.Value))
        {
            return SkillOperationResult<AgentPruneResult>.Success(new AgentPruneResult(
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
            foreach (var plan in plans.Where(static plan => plan.Action.ActionKind == AgentRemovalActionKind.Deleted))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var preconditionResult = await targetInspector.InspectOwnedStateAsync(plan.State!, target, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return Failure(preconditionResult.Failure!);
                }

                if (preconditionResult.Value!.Kind != plan.TargetState.Kind || !string.Equals(preconditionResult.Value.Detail, plan.TargetState.Detail, StringComparison.Ordinal))
                {
                    return SkillOperationResult<AgentPruneResult>.FailureResult(
                        SkillFailureCodes.InstallTargetWriteFailed,
                        $"Custom-agent target changed after prune planning: {plan.Action.AgentName.Value}.");
                }
            }

            foreach (var plan in plans.Where(static plan => plan.Action.ActionKind == AgentRemovalActionKind.Deleted))
            {
                var deleteResult = await artifactStore.DeleteAsync(plan.State!, plan.StatePath!, target, cancellationToken).ConfigureAwait(false);
                if (!deleteResult.IsSuccess)
                {
                    return Failure(deleteResult.Failure!);
                }
            }
        }

        return SkillOperationResult<AgentPruneResult>.Success(new AgentPruneResult(
            target.ArtifactRoot,
            target.StateRoot,
            plans.Select(static plan => plan.Action).ToArray(),
            input.DryRun,
            input.Force));
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>> CreatePlansAsync (
        AgentPruneInput input,
        AgentResolvedTarget target,
        AbsolutePath catalogDirectory,
        CancellationToken cancellationToken)
    {
        var currentNames = input.CurrentCatalog.SelectedAgents.Select(static agent => agent.Manifest.AgentName).ToHashSet();
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
            return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Could not enumerate installed agent state: {exception.Message}");
        }

        foreach (var statePath in statePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readResult = await stateStore.ReadAsync(statePath, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(readResult.Failure!.Code, readResult.Failure.Message);
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
                    return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(
                        SkillFailureCodes.ManifestInvalid,
                        $"Managed agent states for '{owner.Value}' and '{state.AgentName.Value}' own the same artifact: {artifact.Path}.");
                }

                managedPaths.Add(artifact.Path, state.AgentName);
            }

            if (!MatchesSelection(state, input))
            {
                continue;
            }

            var fileAgentName = Path.GetFileNameWithoutExtension(statePath.Value);
            if (!string.Equals(fileAgentName, state.AgentName.Value, StringComparison.Ordinal))
            {
                plans.Add(new AgentRemovalPlan(
                    state,
                    statePath,
                    new AgentInstalledTargetState(AgentInstalledTargetStateKind.Invalid, "Ownership-state file name does not match its agent name."),
                    new AgentRemovalAction(
                        state.AgentName,
                        AgentRemovalActionKind.BlockedInvalid,
                        AgentInstalledTargetStateKind.Invalid,
                        "Ownership-state file name does not match its agent name.")));
                continue;
            }

            AgentInstalledTargetState targetState;
            if (state.CatalogId != input.CurrentCatalog.BundleDescriptor.CatalogId)
            {
                targetState = new AgentInstalledTargetState(AgentInstalledTargetStateKind.OtherCatalog);
            }
            else if (currentNames.Contains(state.AgentName))
            {
                targetState = new AgentInstalledTargetState(AgentInstalledTargetStateKind.Current);
                plans.Add(new AgentRemovalPlan(
                    state,
                    statePath,
                    targetState,
                    new AgentRemovalAction(state.AgentName, AgentRemovalActionKind.SkippedCurrent, targetState.Kind, targetState.Detail)));
                continue;
            }
            else
            {
                var inspectResult = await targetInspector.InspectOwnedStateAsync(state, target, cancellationToken).ConfigureAwait(false);
                if (!inspectResult.IsSuccess)
                {
                    return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.FailureResult(inspectResult.Failure!.Code, inspectResult.Failure.Message);
                }

                targetState = inspectResult.Value!;
            }

            var actionKind = targetState.Kind switch
            {
                AgentInstalledTargetStateKind.Current => AgentRemovalActionKind.Deleted,
                AgentInstalledTargetStateKind.LocallyModified when input.Force => AgentRemovalActionKind.Deleted,
                AgentInstalledTargetStateKind.LocallyModified => AgentRemovalActionKind.BlockedLocalModification,
                AgentInstalledTargetStateKind.OtherCatalog => AgentRemovalActionKind.BlockedForeignCatalog,
                AgentInstalledTargetStateKind.Unmanaged => AgentRemovalActionKind.BlockedUnmanaged,
                _ => AgentRemovalActionKind.BlockedInvalid,
            };
            plans.Add(new AgentRemovalPlan(
                state,
                statePath,
                targetState,
                new AgentRemovalAction(state.AgentName, actionKind, targetState.Kind, targetState.Detail)));
        }

        return SkillOperationResult<IReadOnlyList<AgentRemovalPlan>>.Success(Array.AsReadOnly(plans.ToArray()));
    }

    private static bool MatchesSelection (AgentInstallationState state, AgentPruneInput input)
    {
        return (input.SelectedCategories.Count == 0 || input.SelectedCategories.Contains(state.Category))
            && (input.SelectedAgentNames.Count == 0 || input.SelectedAgentNames.Contains(state.AgentName));
    }

    private static SkillOperationResult<AgentPruneResult> Failure (SkillFailure failure)
    {
        return SkillOperationResult<AgentPruneResult>.FailureResult(failure.Code, failure.Message);
    }
}
