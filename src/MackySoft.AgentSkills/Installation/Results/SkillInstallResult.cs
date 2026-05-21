namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL install operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill install actions. </param>
/// <param name="DryRun"> Whether the result represents a plan without writes. </param>
/// <param name="Force"> Whether the operation was planned or executed with install force semantics enabled. </param>
/// <param name="PrintDiff"> Whether diff payloads were requested. </param>
public sealed record SkillInstallResult (
    string TargetRoot,
    IReadOnlyList<SkillInstallAction> Actions,
    bool DryRun = false,
    bool Force = false,
    bool PrintDiff = false);
