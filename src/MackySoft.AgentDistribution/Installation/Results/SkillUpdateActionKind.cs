namespace MackySoft.AgentDistribution.Installation.Results;

/// <summary> Defines the outcome for one SKILL update. </summary>
[VocabularyDefinition]
public enum SkillUpdateActionKind
{
    /// <summary> The target skill directory is planned to be created or was created because it was missing. </summary>
    [VocabularyText("created")]
    Created = 0,

    /// <summary> The target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    [VocabularyText("updated")]
    Updated = 1,

    /// <summary> The target skill directory already contained current content for the same host. </summary>
    [VocabularyText("noOp")]
    NoOp = 2,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 3,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 4,

    /// <summary> The target was generated from a newer SKILL bundle and cannot be overwritten without force. </summary>
    [VocabularyText("blockedVersionAhead")]
    BlockedVersionAhead = 5,
}
