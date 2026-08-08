namespace MackySoft.AgentSkills.Agents.Installation.Results;

/// <summary> Defines one custom-agent uninstall or prune outcome. </summary>
[VocabularyDefinition]
public enum AgentRemovalActionKind
{
    /// <summary> Managed artifacts and ownership state are deleted. </summary>
    [VocabularyText("deleted")]
    Deleted = 0,

    /// <summary> No managed installation exists. </summary>
    [VocabularyText("noOp")]
    NoOp = 1,

    /// <summary> The agent remains in the current catalog and is not pruned. </summary>
    [VocabularyText("skippedCurrent")]
    SkippedCurrent = 2,

    /// <summary> Local modifications require force. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 3,

    /// <summary> An unmanaged artifact is never deleted. </summary>
    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 4,

    /// <summary> Another catalog owns the target. </summary>
    [VocabularyText("blockedForeignCatalog")]
    BlockedForeignCatalog = 5,

    /// <summary> Ownership state or managed artifacts are invalid. </summary>
    [VocabularyText("blockedInvalid")]
    BlockedInvalid = 6,
}
