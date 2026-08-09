namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines custom-agent target states exposed by operation reports. </summary>
[VocabularyDefinition]
public enum AgentOperationTargetState
{
    [VocabularyText("missing")]
    Missing = 0,

    [VocabularyText("current")]
    Current = 1,

    [VocabularyText("locallyModified")]
    LocallyModified = 2,

    [VocabularyText("unmanaged")]
    Unmanaged = 3,

    [VocabularyText("otherCatalog")]
    OtherCatalog = 4,

    [VocabularyText("invalid")]
    Invalid = 5,

    [VocabularyText("cleanOutdated")]
    CleanOutdated = 6,
}
