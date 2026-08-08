namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the outcome for one SKILL prune candidate. </summary>
[VocabularyDefinition]
public enum SkillPruneActionKind
{
    /// <summary> The managed orphan target is planned to be deleted or was deleted. </summary>
    [VocabularyText("deleted")]
    Deleted = 0,

    /// <summary> The target is still present in the current catalog and is not a prune candidate. </summary>
    [VocabularyText("skippedCurrent")]
    SkippedCurrent = 1,

    /// <summary> The target is managed by another catalog and is not a prune candidate. </summary>
    [VocabularyText("skippedForeignCatalog")]
    SkippedForeignCatalog = 2,

    /// <summary> The target directory is not managed by Agent Skills. </summary>
    [VocabularyText("skippedUnmanaged")]
    SkippedUnmanaged = 3,

    /// <summary> The managed orphan target contains local modifications and requires force before deletion. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 4,

    /// <summary> The target manifest is invalid and cannot be trusted for pruning. </summary>
    [VocabularyText("blockedManifestInvalid")]
    BlockedManifestInvalid = 5,

    /// <summary> The target manifest identifies a different SKILL name than the target directory. </summary>
    [VocabularyText("blockedNameCollision")]
    BlockedNameCollision = 6,

    /// <summary> The target is materialized for a different host. </summary>
    [VocabularyText("blockedHostConflict")]
    BlockedHostConflict = 7,
}
