using MackySoft.AgentSkills.Installation.Results;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary> Represents planned file changes together with the target snapshot they were computed from. </summary>
/// <param name="FileChanges"> The deterministic file replacement/removal summary. </param>
/// <param name="TargetSnapshot"> The target snapshot used to compute <paramref name="FileChanges" />. </param>
internal sealed record SkillActionFileChangePlan (
    SkillActionFileChanges FileChanges,
    SkillActionTargetSnapshot TargetSnapshot);
