using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Services;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

/// <summary> Updates selected custom agents and their resolved SKILL dependency closure. </summary>
public sealed class AgentUpdateService
{
    private readonly AgentInstallTargetResolver targetResolver;
    private readonly AgentReconciliationPlanner planner;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly AgentManagedArtifactStore artifactStore;
    private readonly SkillUpdateService skillUpdateService;

    /// <summary> Initializes a custom-agent update service. </summary>
    public AgentUpdateService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        SkillDigestCalculator digestCalculator,
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore,
        SkillUpdateService skillUpdateService)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        planner = new AgentReconciliationPlanner(targetInspector, digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator)));
        artifactStore = new AgentManagedArtifactStore(
            statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver)),
            stateStore ?? throw new ArgumentNullException(nameof(stateStore)));
        this.skillUpdateService = skillUpdateService ?? throw new ArgumentNullException(nameof(skillUpdateService));
    }

    /// <summary> Updates agents only after both the agent plan and the SKILL dry-run plan are write-safe. </summary>
    public async ValueTask<SkillOperationResult<AgentUpdateResult>> UpdateAsync (
        AgentUpdateInput input,
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
        var plansResult = await planner.CreatePlansAsync(
            input.Catalog.SelectedAgents,
            target,
            AgentReconciliationMode.Update,
            input.Force,
            input.PrintDiff,
            cancellationToken).ConfigureAwait(false);
        if (!plansResult.IsSuccess)
        {
            return Failure(plansResult.Failure!);
        }

        var skillPlanResult = await skillUpdateService.PlanAsync(
            new SkillUpdateInput(
                input.Catalog.BundleDescriptor.CatalogId,
                input.Catalog.ResolvedSkills,
                input.SkillTargetRequest,
                dryRun: true,
                input.Force,
                input.PrintDiff),
            cancellationToken).ConfigureAwait(false);
        if (!skillPlanResult.IsSuccess)
        {
            return Failure(skillPlanResult.Failure!);
        }

        if (input.DryRun)
        {
            return Success(input, target, plansResult.Value!, skillPlanResult.Value!.CreateResult(dryRun: true));
        }

        var agentBlocker = AgentOperationBlocker.ValidateAgents(plansResult.Value!);
        if (!agentBlocker.IsSuccess)
        {
            return Failure(agentBlocker.Failure!);
        }

        var skillBlocker = AgentOperationBlocker.ValidateSkills(skillPlanResult.Value!.CreateResult(dryRun: true));
        if (!skillBlocker.IsSuccess)
        {
            return Failure(skillBlocker.Failure!);
        }

        var preconditionResult = await ValidatePreconditionsAsync(plansResult.Value!, target, cancellationToken).ConfigureAwait(false);
        if (!preconditionResult.IsSuccess)
        {
            return Failure(preconditionResult.Failure!);
        }

        var skillWriteResult = await skillUpdateService.ApplyAsync(skillPlanResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!skillWriteResult.IsSuccess)
        {
            return Failure(skillWriteResult.Failure!);
        }

        foreach (var plan in plansResult.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan.Action.ActionKind == AgentReconcileActionKind.NoOp)
            {
                continue;
            }

            var writeResult = await artifactStore.WriteAsync(plan, target, cancellationToken).ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                return Failure(writeResult.Failure!);
            }
        }

        return Success(input, target, plansResult.Value!, skillWriteResult.Value!);
    }

    private async ValueTask<SkillOperationResult<bool>> ValidatePreconditionsAsync (
        IReadOnlyList<AgentReconciliationPlan> plans,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans)
        {
            var currentResult = await targetInspector.InspectAsync(plan.Package.Manifest, target, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            if (currentResult.Value!.Kind != plan.TargetState.Kind || !string.Equals(currentResult.Value.Detail, plan.TargetState.Detail, StringComparison.Ordinal))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.InstallTargetWriteFailed,
                    $"Custom-agent target changed after planning: {plan.Package.Manifest.AgentName.Value}.");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<AgentUpdateResult> Success (
        AgentUpdateInput input,
        AgentResolvedTarget target,
        IReadOnlyList<AgentReconciliationPlan> plans,
        global::MackySoft.AgentDistribution.Installation.Results.SkillUpdateResult skillResult)
    {
        return SkillOperationResult<AgentUpdateResult>.Success(new AgentUpdateResult(
            target.ArtifactRoot,
            target.StateRoot,
            plans.Select(static plan => plan.Action).ToArray(),
            skillResult,
            input.DryRun,
            input.Force,
            input.PrintDiff));
    }

    private static SkillOperationResult<AgentUpdateResult> Failure (SkillFailure failure)
    {
        return SkillOperationResult<AgentUpdateResult>.FailureResult(failure.Code, failure.Message);
    }
}
