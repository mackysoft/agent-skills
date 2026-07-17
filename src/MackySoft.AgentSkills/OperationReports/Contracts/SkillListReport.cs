namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for generated skills and supported hosts. </summary>
public sealed class SkillListReport
{
    internal SkillListReport (
        IReadOnlyList<string> categories,
        IReadOnlyList<string> skillNames,
        IReadOnlyList<SkillListCategoryReport> availableCategories,
        IReadOnlyList<SkillListSkillReport> skills,
        IReadOnlyList<SkillHostReport> supportedHosts)
    {
        Categories = OperationReportContractGuard.SnapshotRequiredStrings(categories, nameof(categories));
        SkillNames = OperationReportContractGuard.SnapshotRequiredStrings(skillNames, nameof(skillNames));
        AvailableCategories = OperationReportContractGuard.SnapshotRequiredItems(availableCategories, nameof(availableCategories));
        Skills = OperationReportContractGuard.SnapshotRequiredItems(skills, nameof(skills));
        SupportedHosts = OperationReportContractGuard.SnapshotRequiredItems(supportedHosts, nameof(supportedHosts));
    }

    /// <summary> Gets the selected category literals. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets the exact SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<string> SkillNames { get; }

    /// <summary> Gets all available categories with bundled SKILL counts. </summary>
    public IReadOnlyList<SkillListCategoryReport> AvailableCategories { get; }

    /// <summary> Gets the canonical SKILL reports in ordinal name order. </summary>
    public IReadOnlyList<SkillListSkillReport> Skills { get; }

    /// <summary> Gets supported host reports in canonical literal order. </summary>
    public IReadOnlyList<SkillHostReport> SupportedHosts { get; }
}
