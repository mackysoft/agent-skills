using MackySoft.AgentDistribution.Agents.Installation.State;

namespace MackySoft.AgentDistribution.Agents.Installation.Results;

/// <summary> Represents one planned or completed custom-agent removal action. </summary>
public sealed class AgentRemovalAction
{
    /// <summary> Initializes one immutable removal action. </summary>
    internal AgentRemovalAction (AgentName agentName, AgentRemovalActionKind actionKind, AgentInstalledTargetStateKind targetStateKind, string? detail)
    {
        AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        ActionKind = actionKind;
        TargetStateKind = targetStateKind;
        Detail = detail;
    }

    /// <summary> Gets the affected agent name. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the planned or completed outcome. </summary>
    public AgentRemovalActionKind ActionKind { get; }

    /// <summary> Gets the target state observed while planning. </summary>
    public AgentInstalledTargetStateKind TargetStateKind { get; }

    /// <summary> Gets optional diagnostic detail. </summary>
    public string? Detail { get; }

    /// <summary> Gets whether the action prevents deletion. </summary>
    public bool IsBlocked => ActionKind is AgentRemovalActionKind.BlockedLocalModification
        or AgentRemovalActionKind.BlockedUnmanaged
        or AgentRemovalActionKind.BlockedForeignCatalog
        or AgentRemovalActionKind.BlockedInvalid;
}
