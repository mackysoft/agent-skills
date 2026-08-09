namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Classifies one installed custom-agent target. </summary>
[VocabularyDefinition]
public enum AgentInstalledTargetStateKind
{
    /// <summary> No managed state and no expected artifact exists. </summary>
    [VocabularyText("missing")]
    Missing = 0,

    /// <summary> Managed state and all managed artifacts match the requested package. </summary>
    [VocabularyText("current")]
    Current = 1,

    /// <summary> Managed artifacts differ from their installed digests. </summary>
    [VocabularyText("locallyModified")]
    LocallyModified = 2,

    /// <summary> The expected artifact exists without Agent Skills ownership state. </summary>
    [VocabularyText("unmanaged")]
    Unmanaged = 3,

    /// <summary> Ownership state belongs to another catalog. </summary>
    [VocabularyText("otherCatalog")]
    OtherCatalog = 4,

    /// <summary> Ownership state is malformed or contradicts the requested target. </summary>
    [VocabularyText("invalid")]
    Invalid = 5,

    /// <summary> Managed artifacts are clean but belong to a different generated manifest. </summary>
    [VocabularyText("cleanOutdated")]
    CleanOutdated = 6,
}
