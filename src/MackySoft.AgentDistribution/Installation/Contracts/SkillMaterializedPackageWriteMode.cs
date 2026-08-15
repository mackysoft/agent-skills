namespace MackySoft.AgentDistribution.Installation.Contracts;

/// <summary> Defines the target existence precondition for a materialized package write. </summary>
[VocabularyDefinition]
public enum SkillMaterializedPackageWriteMode
{
    /// <summary> The target directory must be absent at commit time. </summary>
    [VocabularyText("createNew")]
    CreateNew = 0,

    /// <summary> The target directory must be present and eligible for replacement at commit time. </summary>
    [VocabularyText("replaceExisting")]
    ReplaceExisting = 1,
}
