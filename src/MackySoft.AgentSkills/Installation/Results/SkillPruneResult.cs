namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL prune operation. </summary>
/// <param name="TargetRoot"> The resolved host target root. </param>
/// <param name="Actions"> The per-skill prune actions. </param>
/// <param name="DryRun"> Whether the operation returned a plan without deleting from the file system. </param>
/// <param name="Force"> Whether the operation was planned or executed with prune force semantics enabled. </param>
public sealed record SkillPruneResult (
    string TargetRoot,
    IReadOnlyList<SkillPruneAction> Actions,
    bool DryRun,
    bool Force);
