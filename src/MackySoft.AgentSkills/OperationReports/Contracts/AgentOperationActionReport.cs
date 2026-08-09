using MackySoft.AgentSkills.OperationReports.Literals;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents one product-neutral custom-agent action. </summary>
public sealed class AgentOperationActionReport
{
    internal AgentOperationActionReport (
        string agentName,
        string action,
        OperationActionStatus status,
        AgentTargetStateReport targetState,
        IReadOnlyList<OperationFileDiffReport> fileDiffs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        if (!Vocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported operation action status.");
        }

        AgentName = agentName;
        Action = action;
        Status = status;
        TargetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
        FileDiffs = OperationReportContractGuard.SnapshotRequiredItems(fileDiffs, nameof(fileDiffs));
    }

    /// <summary> Gets the canonical agent name. </summary>
    public string AgentName { get; }

    /// <summary> Gets the stable fine-grained action literal. </summary>
    public string Action { get; }

    /// <summary> Gets the coarse action status. </summary>
    public OperationActionStatus Status { get; }

    /// <summary> Gets the target state observed while planning. </summary>
    public AgentTargetStateReport TargetState { get; }

    /// <summary> Gets requested artifact content diffs in deterministic order. </summary>
    public IReadOnlyList<OperationFileDiffReport> FileDiffs { get; }
}
