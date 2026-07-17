using MackySoft.AgentSkills.Installation.Results;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary> Represents planned file changes and the target snapshot that must still match before execution. </summary>
internal sealed class SkillActionFileChangePlan
{
    /// <summary> Initializes one planned file change snapshot. </summary>
    /// <param name="fileChanges"> The file replacement and removal summary computed during planning. </param>
    /// <param name="targetSnapshot"> The target snapshot used to reject stale execution. </param>
    public SkillActionFileChangePlan (
        SkillActionFileChanges fileChanges,
        SkillActionTargetSnapshot targetSnapshot)
    {
        FileChanges = fileChanges ?? throw new ArgumentNullException(nameof(fileChanges));
        TargetSnapshot = targetSnapshot ?? throw new ArgumentNullException(nameof(targetSnapshot));
    }

    /// <summary> Gets the file replacement and removal summary computed during planning. </summary>
    public SkillActionFileChanges FileChanges { get; }

    /// <summary> Gets the target snapshot used to reject stale execution. </summary>
    public SkillActionTargetSnapshot TargetSnapshot { get; }
}
