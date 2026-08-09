namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the outcome for one SKILL uninstall. </summary>
[VocabularyDefinition]
public enum SkillUninstallActionKind
{
    /// <summary> The managed target skill directory is planned to be deleted or was deleted. </summary>
    [VocabularyText("deleted")]
    Deleted = 0,

    /// <summary> The target skill directory was already absent. </summary>
    [VocabularyText("noOp")]
    NoOp = 1,

    /// <summary> The target skill directory exists but is not managed by Agent Skills. </summary>
    [VocabularyText("skippedUnmanaged")]
    SkippedUnmanaged = 2,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 3,
}
