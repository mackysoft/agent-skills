namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for generated skills and supported hosts. </summary>
/// <param name="Skills"> The canonical skill reports sorted by skill name using ordinal comparison. </param>
/// <param name="SupportedHosts"> The supported host reports sorted by host key using ordinal comparison. </param>
public sealed record SkillListReport (
    IReadOnlyList<SkillListSkillReport> Skills,
    IReadOnlyList<SkillHostReport> SupportedHosts);
