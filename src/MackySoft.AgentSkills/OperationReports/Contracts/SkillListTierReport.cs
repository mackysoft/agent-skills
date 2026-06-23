namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one available SKILL tier. </summary>
/// <param name="Tier"> The product-owned SKILL tier literal. </param>
/// <param name="SkillCount"> The number of bundled skills assigned to <paramref name="Tier" />. </param>
public sealed record SkillListTierReport (
    string Tier,
    int SkillCount);
