using MackySoft.AgentSkills.Installation.Results;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary>
/// Represents planned file changes together with the target snapshot that must still match before execution.
/// </summary>
/// <param name="FileChanges">
/// The public file replacement/removal summary computed from the target entries present at planning time.
/// </param>
/// <param name="TargetSnapshot">
/// The file-and-directory target snapshot used to reject stale write or delete plans.
/// </param>
internal sealed record SkillActionFileChangePlan (
    SkillActionFileChanges FileChanges,
    SkillActionTargetSnapshot TargetSnapshot);
