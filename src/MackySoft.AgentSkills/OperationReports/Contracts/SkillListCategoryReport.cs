namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for one available SKILL category. </summary>
public sealed class SkillListCategoryReport
{
    internal SkillListCategoryReport (
        string category,
        int skillCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (skillCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skillCount), skillCount, "Available category SKILL count must be nonnegative.");
        }

        Category = category;
        SkillCount = skillCount;
    }

    /// <summary> Gets the category literal. </summary>
    public string Category { get; }

    /// <summary> Gets the number of bundled SKILLs in the category. </summary>
    public int SkillCount { get; }
}
