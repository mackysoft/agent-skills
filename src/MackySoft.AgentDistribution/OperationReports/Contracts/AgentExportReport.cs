using MackySoft.AgentDistribution.Distribution;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents product-neutral custom-agent export result data. </summary>
public sealed class AgentExportReport
{
    internal AgentExportReport (
        HostKind hostId,
        IReadOnlyList<string> agentNames,
        SkillExportFormat format,
        AbsolutePath outputPath,
        IReadOnlyList<string> agents,
        IReadOnlyList<string> skills)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        if (!Vocabulary.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }

        ArgumentNullException.ThrowIfNull(outputPath);
        HostId = hostId;
        AgentNames = OperationReportContractGuard.SnapshotRequiredStrings(agentNames, nameof(agentNames));
        Format = format;
        OutputPath = outputPath.Value;
        Agents = OperationReportContractGuard.SnapshotRequiredStrings(agents, nameof(agents));
        Skills = OperationReportContractGuard.SnapshotRequiredStrings(skills, nameof(skills));
    }

    /// <summary> Gets the host identifier used for export. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets exact selected agent names. An empty collection means no name filter. </summary>
    public IReadOnlyList<string> AgentNames { get; }

    /// <summary> Gets the export format. </summary>
    public SkillExportFormat Format { get; }

    /// <summary> Gets the canonical output directory or zip path. </summary>
    public string OutputPath { get; }

    /// <summary> Gets exported agent names in ordinal order. </summary>
    public IReadOnlyList<string> Agents { get; }

    /// <summary> Gets exported resolved SKILL names in ordinal order. </summary>
    public IReadOnlyList<string> Skills { get; }

    /// <summary> Gets the number of exported agents. </summary>
    public int AgentCount => Agents.Count;

    /// <summary> Gets the number of exported resolved SKILLs. </summary>
    public int SkillCount => Skills.Count;
}
