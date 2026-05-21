using MackySoft.AgentSkills.Installation.Targeting;
namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill update action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
/// <param name="BlockedReason"> The blocked reason category, when the action is blocked. </param>
/// <param name="Diffs"> The optional structured diffs. </param>
/// <param name="TargetState"> The analyzed target state that produced this action, when available. </param>
public sealed record SkillUpdateAction (
    SkillInstallIdentity Identity,
    SkillUpdateActionKind ActionKind,
    SkillBlockedReason? BlockedReason = null,
    IReadOnlyList<SkillActionDiff>? Diffs = null,
    SkillActionTargetState? TargetState = null)
{
    /// <summary>
    /// Gets the existing file change summary for <see cref="SkillUpdateActionKind.Created" /> and
    /// <see cref="SkillUpdateActionKind.Updated" /> actions.
    /// </summary>
    /// <remarks>
    /// The value is an empty summary for <see cref="SkillUpdateActionKind.Created" /> and the planned or
    /// performed replacement summary for <see cref="SkillUpdateActionKind.Updated" />. It is
    /// <see langword="null" /> for <see cref="SkillUpdateActionKind.NoOp" />, blocked local modification,
    /// and blocked unmanaged actions.
    /// </remarks>
    public SkillActionFileChanges? FileChanges { get; init; }
}
