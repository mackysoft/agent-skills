using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.OperationReports.Literals;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents one per-skill operation action. </summary>
public sealed class SkillOperationActionReport
{
    internal SkillOperationActionReport (
        string skillName,
        string action,
        OperationActionStatus status,
        SkillBlockedReason? blockedReason,
        SkillTargetStateReport? targetState,
        SkillOperationFileChangesReport? fileChanges,
        IReadOnlyList<OperationFileDiffReport> fileDiffs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        if (!Vocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported operation action status.");
        }

        if (blockedReason.HasValue && !Vocabulary.IsDefined(blockedReason.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(blockedReason), blockedReason, "Unsupported blocked reason.");
        }

        SkillName = skillName;
        Action = action;
        Status = status;
        BlockedReason = blockedReason;
        TargetState = targetState;
        FileChanges = fileChanges;
        FileDiffs = OperationReportContractGuard.SnapshotRequiredItems(fileDiffs, nameof(fileDiffs));
    }

    /// <summary> Gets the canonical skill name. </summary>
    public string SkillName { get; }

    /// <summary> Gets the stable fine-grained action literal. </summary>
    public string Action { get; }

    /// <summary> Gets the coarse action status. </summary>
    public OperationActionStatus Status { get; }

    /// <summary> Gets the blocked reason, or <see langword="null" /> when the source action does not expose one. </summary>
    public SkillBlockedReason? BlockedReason { get; }

    /// <summary> Gets the analyzed target state that produced this action, when available. </summary>
    public SkillTargetStateReport? TargetState { get; }

    /// <summary> Gets existing target files that the action replaces or removes, when available. </summary>
    public SkillOperationFileChangesReport? FileChanges { get; }

    /// <summary> Gets file content diffs requested by the caller, in deterministic order. </summary>
    public IReadOnlyList<OperationFileDiffReport> FileDiffs { get; }
}
