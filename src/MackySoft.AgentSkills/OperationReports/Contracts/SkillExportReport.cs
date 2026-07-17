using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral export result data. </summary>
public sealed class SkillExportReport
{
    internal SkillExportReport (
        SkillHostKind host,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> skillNames,
        SkillExportFormat format,
        string outputPath,
        IReadOnlyList<string> skills,
        int skillCount,
        string reloadGuidance)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!ContractLiteralCodec.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reloadGuidance);

        var categorySnapshot = OperationReportContractGuard.SnapshotRequiredStrings(categories, nameof(categories));
        var skillNameSnapshot = OperationReportContractGuard.SnapshotRequiredStrings(skillNames, nameof(skillNames));
        var exportedSkillSnapshot = OperationReportContractGuard.SnapshotRequiredStrings(skills, nameof(skills));
        if (skillCount != exportedSkillSnapshot.Count)
        {
            throw new ArgumentException("Exported SKILL count must match the exported SKILL collection.", nameof(skillCount));
        }

        Host = host;
        Categories = categorySnapshot;
        SkillNames = skillNameSnapshot;
        Format = format;
        OutputPath = Path.GetFullPath(outputPath);
        Skills = exportedSkillSnapshot;
        SkillCount = skillCount;
        ReloadGuidance = reloadGuidance;
    }

    /// <summary> Gets the host used for export. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the selected category literals. </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary> Gets the exact SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<string> SkillNames { get; }

    /// <summary> Gets the export format. </summary>
    public SkillExportFormat Format { get; }

    /// <summary> Gets the canonical output directory or zip path. </summary>
    public string OutputPath { get; }

    /// <summary> Gets the exported SKILL names in ordinal order. </summary>
    public IReadOnlyList<string> Skills { get; }

    /// <summary> Gets the number of exported SKILLs. </summary>
    public int SkillCount { get; }

    /// <summary> Gets the host-specific reload guidance. </summary>
    public string ReloadGuidance { get; }
}
