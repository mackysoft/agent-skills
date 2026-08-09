using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;

namespace MackySoft.AgentSkills.Installation.Services;

/// <summary> Owns one resolved SKILL install decision and the exact write inputs derived from it. </summary>
internal sealed class SkillInstallPlan
{
    public SkillInstallPlan (
        SkillInstallInput input,
        SkillResolvedInstallTarget target,
        IReadOnlyList<SkillInstallActionPlan> actionPlans)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentNullException.ThrowIfNull(actionPlans);
        ActionPlans = Array.AsReadOnly(actionPlans.ToArray());
    }

    public SkillInstallInput Input { get; }

    public SkillResolvedInstallTarget Target { get; }

    public IReadOnlyList<SkillInstallActionPlan> ActionPlans { get; }

    public SkillInstallResult CreateResult (bool dryRun)
    {
        return new SkillInstallResult(
            Target.TargetRoot,
            ActionPlans.Select(static plan => plan.Action).ToArray(),
            dryRun,
            Input.Force,
            Input.PrintDiff);
    }
}
