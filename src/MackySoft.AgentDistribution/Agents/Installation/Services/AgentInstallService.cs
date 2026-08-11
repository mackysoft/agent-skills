using MackySoft.AgentDistribution.Agents.Installation.Requests;
using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Services;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

/// <summary> Installs selected custom agents and their resolved SKILL dependency closure. </summary>
public sealed class AgentInstallService
{
    private readonly AgentInstallTargetResolver targetResolver;
    private readonly AgentReconciliationPlanner planner;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly AgentManagedArtifactStore artifactStore;
    private readonly SkillInstallService skillInstallService;

    /// <summary> Initializes a custom-agent install service. </summary>
    public AgentInstallService (
        AgentInstallTargetResolver targetResolver,
        AgentInstalledTargetInspector targetInspector,
        PackageContentDigestCalculator digestCalculator,
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore,
        SkillInstallService skillInstallService)
    {
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        planner = new AgentReconciliationPlanner(targetInspector, digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator)));
        artifactStore = new AgentManagedArtifactStore(
            statePathResolver ?? throw new ArgumentNullException(nameof(statePathResolver)),
            stateStore ?? throw new ArgumentNullException(nameof(stateStore)));
        this.skillInstallService = skillInstallService ?? throw new ArgumentNullException(nameof(skillInstallService));
    }

    /// <summary> Installs agents only after both the agent plan and the SKILL dry-run plan are write-safe. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentInstallResult>> InstallAsync (
        AgentInstallInput input,
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
            AgentReconciliationMode.Install,
            input.Force,
            input.PrintDiff,
            cancellationToken).ConfigureAwait(false);
        if (!plansResult.IsSuccess)
        {
            return Failure(plansResult.Failure!);
        }

        var skillPlanResult = await skillInstallService.PlanAsync(
            new SkillInstallInput(
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

        var skillWriteResult = await skillInstallService.ApplyAsync(skillPlanResult.Value!, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidatePreconditionsAsync (
        IReadOnlyList<AgentReconciliationPlan> plans,
        AgentResolvedTarget target,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans)
        {
            var currentResult = await targetInspector.InspectAsync(plan.Package.Manifest, target, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            if (currentResult.Value!.Kind != plan.TargetState.Kind || !string.Equals(currentResult.Value.Detail, plan.TargetState.Detail, StringComparison.Ordinal))
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetWriteFailed,
                    $"Custom-agent target changed after planning: {plan.Package.Manifest.AgentName.Value}.");
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<AgentInstallResult> Success (
        AgentInstallInput input,
        AgentResolvedTarget target,
        IReadOnlyList<AgentReconciliationPlan> plans,
        global::MackySoft.AgentDistribution.Installation.Results.SkillInstallResult skillResult)
    {
        return AgentDistributionOperationResult<AgentInstallResult>.Success(new AgentInstallResult(
            target.ArtifactRoot,
            target.StateRoot,
            plans.Select(static plan => plan.Action).ToArray(),
            skillResult,
            input.DryRun,
            input.Force,
            input.PrintDiff));
    }

    private static AgentDistributionOperationResult<AgentInstallResult> Failure (AgentDistributionFailure failure)
    {
        return AgentDistributionOperationResult<AgentInstallResult>.FailureResult(failure.Code, failure.Message);
    }
}
