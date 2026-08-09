namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines the coarse status for one package operation action. </summary>
[VocabularyDefinition]
public enum OperationActionStatus
{
    /// <summary> The action creates, updates, replaces, or deletes target files. </summary>
    [VocabularyText("changed")]
    Changed = 0,

    /// <summary> The target already satisfies the requested operation. </summary>
    [VocabularyText("noOp")]
    NoOp = 1,

    /// <summary> The operation intentionally leaves the target unchanged. </summary>
    [VocabularyText("skipped")]
    Skipped = 2,

    /// <summary> The action is blocked and requires a different caller decision before it can change files. </summary>
    [VocabularyText("blocked")]
    Blocked = 3,
}
