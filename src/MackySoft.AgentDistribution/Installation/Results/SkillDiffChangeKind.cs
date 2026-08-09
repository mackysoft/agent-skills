namespace MackySoft.AgentDistribution.Installation.Results;

/// <summary> Defines structured SKILL diff change kinds. </summary>
[VocabularyDefinition]
public enum SkillDiffChangeKind
{
    /// <summary> A file is added. </summary>
    [VocabularyText("added")]
    Added,

    /// <summary> A file is modified. </summary>
    [VocabularyText("modified")]
    Modified,

    /// <summary> A file is deleted. </summary>
    [VocabularyText("deleted")]
    Deleted,
}
