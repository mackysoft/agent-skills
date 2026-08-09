namespace MackySoft.AgentDistribution.Installation.Results;

/// <summary> Defines the outcome for one installed SKILL. </summary>
[VocabularyDefinition]
public enum SkillInstallActionKind
{
    /// <summary> The target skill directory is planned to be created or was created. </summary>
    [VocabularyText("created")]
    Created = 0,

    /// <summary> The managed target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    [VocabularyText("updated")]
    Updated = 1,

    /// <summary> The target skill directory already contained matching content for the same host. </summary>
    [VocabularyText("noOp")]
    NoOp = 2,

    /// <summary> The target contains managed non-current content and force was not enabled. </summary>
    [VocabularyText("blockedManagedOverwrite")]
    BlockedManagedOverwrite = 3,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 4,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 5,
}
