using MackySoft.AgentSkills.Installation.Targeting;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill prune action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The prune outcome. </param>
/// <param name="BlockedReason"> The reason why the prune action is blocked. </param>
/// <param name="TargetState"> The observed target state, when relevant to the prune decision. </param>
public sealed record SkillPruneAction (
    SkillInstallIdentity Identity,
    SkillPruneActionKind ActionKind,
    SkillBlockedReason? BlockedReason = null,
    SkillActionTargetState? TargetState = null)
{
    /// <summary>
    /// Gets the existing file removal summary for <see cref="SkillPruneActionKind.Deleted" /> actions.
    /// </summary>
    public SkillActionFileChanges? FileChanges { get; init; }
}
