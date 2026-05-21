namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents one per-skill operation action with stable action and status literals. </summary>
/// <param name="SkillName"> The canonical skill name. </param>
/// <param name="Action"> The stable fine-grained action literal. </param>
/// <param name="Status"> The stable coarse status literal. </param>
/// <param name="BlockedReason"> The stable blocked reason literal, or <see langword="null" /> when the action is not blocked. </param>
/// <param name="TargetState"> The analyzed target state that produced this action, when available. </param>
/// <param name="FileChanges"> Existing target files that the action replaces or removes, when available. </param>
/// <param name="FileDiffs"> File content diffs requested by the caller, in deterministic order. </param>
public sealed record SkillOperationActionReport (
    string SkillName,
    string Action,
    string Status,
    string? BlockedReason,
    SkillTargetStateReport? TargetState,
    SkillOperationFileChangesReport? FileChanges,
    IReadOnlyList<SkillOperationFileDiffReport> FileDiffs);
