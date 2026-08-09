using MackySoft.AgentDistribution.Agents.Installation.State;

namespace MackySoft.AgentDistribution.Agents.Installation.Results;

/// <summary> Represents one planned or completed custom-agent install or update action. </summary>
public sealed class AgentReconcileAction
{
    /// <summary> Initializes one immutable reconciliation action. </summary>
    internal AgentReconcileAction (
        AgentName agentName,
        AgentReconcileActionKind actionKind,
        AgentInstalledTargetStateKind targetStateKind,
        string? detail,
        IReadOnlyList<AgentArtifactDiff>? diffs)
    {
        AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        ActionKind = actionKind;
        TargetStateKind = targetStateKind;
        Detail = detail;
        Diffs = diffs is null ? null : Array.AsReadOnly(diffs.ToArray());
    }

    /// <summary> Gets the affected agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the planned or completed outcome. </summary>
    public AgentReconcileActionKind ActionKind { get; }

    /// <summary> Gets the target state observed while planning. </summary>
    public AgentInstalledTargetStateKind TargetStateKind { get; }

    /// <summary> Gets optional diagnostic detail. </summary>
    public string? Detail { get; }

    /// <summary> Gets requested per-artifact content diffs, or <see langword="null" /> when diff output was not requested. </summary>
    public IReadOnlyList<AgentArtifactDiff>? Diffs { get; }

    /// <summary> Gets whether the action prevents writes. </summary>
    public bool IsBlocked => ActionKind is AgentReconcileActionKind.BlockedManagedOverwrite
        or AgentReconcileActionKind.BlockedLocalModification
        or AgentReconcileActionKind.BlockedUnmanaged
        or AgentReconcileActionKind.BlockedForeignCatalog
        or AgentReconcileActionKind.BlockedInvalid;
}
