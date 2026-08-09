using MackySoft.AgentDistribution.Installation.Requests;
using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Installation.Targeting;

namespace MackySoft.AgentDistribution.Installation.Services;

/// <summary> Owns one resolved SKILL update decision and the exact write inputs derived from it. </summary>
internal sealed class SkillUpdatePlan
{
    public SkillUpdatePlan (
        SkillUpdateInput input,
        SkillResolvedInstallTarget target,
        IReadOnlyList<SkillUpdateActionPlan> actionPlans)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentNullException.ThrowIfNull(actionPlans);
        ActionPlans = Array.AsReadOnly(actionPlans.ToArray());
    }

    public SkillUpdateInput Input { get; }

    public SkillResolvedInstallTarget Target { get; }

    public IReadOnlyList<SkillUpdateActionPlan> ActionPlans { get; }

    public SkillUpdateResult CreateResult (bool dryRun)
    {
        return new SkillUpdateResult(
            Target.TargetRoot,
            ActionPlans.Select(static plan => plan.Action).ToArray(),
            dryRun,
            Input.Force,
            Input.PrintDiff);
    }
}
