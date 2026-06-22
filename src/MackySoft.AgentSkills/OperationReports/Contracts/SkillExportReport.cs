namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral export result data. </summary>
/// <param name="Host"> The canonical host key used for export. </param>
/// <param name="Tiers"> The selected product-owned SKILL tier literals, or an empty list when the caller did not provide any. </param>
/// <param name="Format"> The stable export format literal. </param>
/// <param name="OutputPath"> The canonical output directory or zip file path returned by export. </param>
/// <param name="Skills"> The exported skill names sorted using ordinal comparison. </param>
/// <param name="SkillCount"> The number of exported skills. </param>
/// <param name="ReloadGuidance"> The host-specific guidance for reloading exported or installed SKILLs. </param>
public sealed record SkillExportReport (
    string Host,
    IReadOnlyList<string> Tiers,
    string Format,
    string OutputPath,
    IReadOnlyList<string> Skills,
    int SkillCount,
    string ReloadGuidance);
