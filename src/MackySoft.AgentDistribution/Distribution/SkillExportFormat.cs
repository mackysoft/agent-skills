namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Defines supported SKILL export output formats. </summary>
[VocabularyDefinition]
public enum SkillExportFormat
{
    /// <summary> Export materialized SKILL directories under the output root. </summary>
    [VocabularyText("directory")]
    Directory = 0,

    /// <summary> Export materialized SKILL directories into one deterministic zip file. </summary>
    [VocabularyText("zip")]
    Zip = 1,
}
