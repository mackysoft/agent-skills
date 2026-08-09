namespace MackySoft.AgentDistribution.Agents.Installation.Results;

/// <summary> Defines one custom-agent install or update outcome. </summary>
[VocabularyDefinition]
public enum AgentReconcileActionKind
{
    /// <summary> The agent artifacts are created. </summary>
    [VocabularyText("created")]
    Created = 0,

    /// <summary> Managed agent artifacts are replaced. </summary>
    [VocabularyText("updated")]
    Updated = 1,

    /// <summary> The installed agent already matches the package. </summary>
    [VocabularyText("noOp")]
    NoOp = 2,

    /// <summary> A clean managed install requires force for this operation. </summary>
    [VocabularyText("blockedManagedOverwrite")]
    BlockedManagedOverwrite = 3,

    /// <summary> Local modifications require force. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 4,

    /// <summary> An unmanaged artifact occupies the target. </summary>
    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 5,

    /// <summary> Another catalog owns the target. </summary>
    [VocabularyText("blockedForeignCatalog")]
    BlockedForeignCatalog = 6,

    /// <summary> Ownership state or managed artifacts are invalid. </summary>
    [VocabularyText("blockedInvalid")]
    BlockedInvalid = 7,
}
