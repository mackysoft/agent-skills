namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Represents one inspected custom-agent target state. </summary>
public sealed class AgentInstalledTargetState
{
    /// <summary> Initializes one target state. </summary>
    internal AgentInstalledTargetState (AgentInstalledTargetStateKind kind, string? detail = null)
    {
        Kind = kind;
        Detail = detail;
    }

    /// <summary> Gets the classified state. </summary>
    public AgentInstalledTargetStateKind Kind { get; }

    /// <summary> Gets optional diagnostic detail. </summary>
    public string? Detail { get; }
}
