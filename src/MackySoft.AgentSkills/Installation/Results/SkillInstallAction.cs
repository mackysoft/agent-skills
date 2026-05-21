using MackySoft.AgentSkills.Installation.Targeting;
namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill install action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
/// <param name="BlockedReason"> The blocked reason category, when the action is blocked. </param>
/// <param name="Diffs"> The optional structured diffs. </param>
/// <param name="TargetState"> The analyzed target state that produced this action, when available. </param>
public sealed record SkillInstallAction (
    SkillInstallIdentity Identity,
    SkillInstallActionKind ActionKind,
    SkillBlockedReason? BlockedReason = null,
    IReadOnlyList<SkillActionDiff>? Diffs = null,
    SkillActionTargetState? TargetState = null)
{
    /// <summary> Gets the deterministic file replacement/removal summary for writing actions. </summary>
    public SkillActionFileChanges? FileChanges { get; init; }
}
