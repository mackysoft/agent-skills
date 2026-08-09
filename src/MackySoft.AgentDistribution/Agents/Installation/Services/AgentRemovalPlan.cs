using MackySoft.AgentDistribution.Agents.Installation.Results;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal sealed class AgentRemovalPlan
{
    public AgentRemovalPlan (
        AgentInstallationState? state,
        AbsolutePath? statePath,
        AgentInstalledTargetState targetState,
        AgentRemovalAction action)
    {
        State = state;
        StatePath = statePath;
        TargetState = targetState;
        Action = action;
    }

    public AgentInstallationState? State { get; }

    public AbsolutePath? StatePath { get; }

    public AgentInstalledTargetState TargetState { get; }

    public AgentRemovalAction Action { get; }
}
