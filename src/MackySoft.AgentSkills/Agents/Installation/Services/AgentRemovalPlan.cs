using MackySoft.AgentSkills.Agents.Installation.Results;
using MackySoft.AgentSkills.Agents.Installation.State;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

internal sealed class AgentRemovalPlan
{
    public AgentRemovalPlan (
        AgentInstallationState? state,
        string? statePath,
        AgentInstalledTargetState targetState,
        AgentRemovalAction action)
    {
        State = state;
        StatePath = statePath;
        TargetState = targetState;
        Action = action;
    }

    public AgentInstallationState? State { get; }

    public string? StatePath { get; }

    public AgentInstalledTargetState TargetState { get; }

    public AgentRemovalAction Action { get; }
}
