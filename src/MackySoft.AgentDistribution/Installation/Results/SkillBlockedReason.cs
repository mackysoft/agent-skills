namespace MackySoft.AgentDistribution.Installation.Results;

/// <summary> Defines blocked action reason categories. </summary>
[VocabularyDefinition]
public enum SkillBlockedReason
{
    /// <summary> The operation would overwrite a managed target without <c>--force</c>. </summary>
    [VocabularyText("managedOverwriteRequiresForce")]
    ManagedOverwriteRequiresForce = 0,

    /// <summary> The operation would overwrite or delete local modifications without <c>--force</c>. </summary>
    [VocabularyText("localModificationRequiresForce")]
    LocalModificationRequiresForce = 1,

    /// <summary> The target directory is not managed by Agent Distribution. </summary>
    [VocabularyText("unmanagedTarget")]
    UnmanagedTarget = 2,

    /// <summary> The operation would overwrite a managed target generated from a newer SKILL bundle without <c>--force</c>. </summary>
    [VocabularyText("installedVersionAhead")]
    InstalledVersionAhead = 3,
}
