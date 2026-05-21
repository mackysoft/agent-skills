namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents product-neutral export result data. </summary>
/// <param name="Host"> The canonical host key used for export. </param>
/// <param name="Format"> The stable export format literal. </param>
/// <param name="OutputPath"> The canonical output directory or zip file path returned by export. </param>
/// <param name="Skills"> The exported skill names sorted using ordinal comparison. </param>
/// <param name="SkillCount"> The number of exported skills. </param>
public sealed record SkillExportReport (
    string Host,
    string Format,
    string OutputPath,
    IReadOnlyList<string> Skills,
    int SkillCount);
