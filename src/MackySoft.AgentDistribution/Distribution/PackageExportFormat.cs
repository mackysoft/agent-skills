namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Defines supported package export output formats. </summary>
[VocabularyDefinition]
public enum PackageExportFormat
{
    /// <summary> Export materialized package directories under the output root. </summary>
    [VocabularyText("directory")]
    Directory = 0,

    /// <summary> Export materialized package directories into one deterministic zip file. </summary>
    [VocabularyText("zip")]
    Zip = 1,
}
