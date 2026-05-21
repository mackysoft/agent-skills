using MackySoft.AgentSkills.Installation.Targeting;
namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill uninstall action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
/// <param name="BlockedReason"> The blocked reason category, when the action is blocked. </param>
/// <param name="TargetState"> The analyzed target state that produced this action, when available. </param>
public sealed record SkillUninstallAction (
    SkillInstallIdentity Identity,
    SkillUninstallActionKind ActionKind,
    SkillBlockedReason? BlockedReason = null,
    SkillActionTargetState? TargetState = null)
{
    /// <summary> Gets the deterministic file removal summary for delete actions. </summary>
    public SkillActionFileChanges? FileChanges { get; init; }
}
