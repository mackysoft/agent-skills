namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for generated skills and supported hosts. </summary>
/// <param name="Tiers"> The selected product-owned SKILL tier literals, or an empty list when the caller did not provide any. </param>
/// <param name="Skills"> The canonical skill reports sorted by skill name using ordinal comparison. </param>
/// <param name="SupportedHosts"> The supported host reports sorted by host key using ordinal comparison. </param>
public sealed record SkillListReport (
    IReadOnlyList<string> Tiers,
    IReadOnlyList<SkillListSkillReport> Skills,
    IReadOnlyList<SkillHostReport> SupportedHosts);
