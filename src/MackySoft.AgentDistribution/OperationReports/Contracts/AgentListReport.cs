namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents product-neutral list data for selected custom agents and resolved SKILL dependencies. </summary>
public sealed class AgentListReport
{
    internal AgentListReport (
        IReadOnlyList<string> agentNames,
        IReadOnlyList<AgentListAgentReport> agents,
        IReadOnlyList<string> resolvedSkills,
        IReadOnlyList<HostKind> supportedHostIds)
    {
        AgentNames = OperationReportContractGuard.SnapshotRequiredStrings(agentNames, nameof(agentNames));
        Agents = OperationReportContractGuard.SnapshotRequiredItems(agents, nameof(agents));
        ResolvedSkills = OperationReportContractGuard.SnapshotRequiredStrings(resolvedSkills, nameof(resolvedSkills));
        ArgumentNullException.ThrowIfNull(supportedHostIds);
        if (supportedHostIds.Any(static host => !Vocabulary.IsDefined(host)))
        {
            throw new ArgumentOutOfRangeException(nameof(supportedHostIds), "Supported agent hosts must be defined.");
        }

        SupportedHostIds = Array.AsReadOnly(supportedHostIds.Distinct().OrderBy(Vocabulary.GetText, StringComparer.Ordinal).ToArray());
    }

    /// <summary> Gets exact selected agent names. An empty collection means no name filter. </summary>
    public IReadOnlyList<string> AgentNames { get; }

    /// <summary> Gets selected canonical agents in ordinal name order. </summary>
    public IReadOnlyList<AgentListAgentReport> Agents { get; }

    /// <summary> Gets the resolved transitive SKILL dependency names in ordinal order. </summary>
    public IReadOnlyList<string> ResolvedSkills { get; }

    /// <summary> Gets host identifiers supported by at least one selected agent in ordinal order. </summary>
    public IReadOnlyList<HostKind> SupportedHostIds { get; }
}
