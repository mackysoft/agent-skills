using MackySoft.AgentSkills.Agents.Installation.Results;
using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Packaging;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

internal sealed class AgentReconciliationPlan
{
    public AgentReconciliationPlan (
        CanonicalAgentPackage package,
        AgentInstalledTargetState targetState,
        AgentReconcileAction action,
        IReadOnlyList<AgentPlannedArtifact> artifacts,
        AgentInstallationState desiredState)
    {
        Package = package;
        TargetState = targetState;
        Action = action;
        Artifacts = artifacts;
        DesiredState = desiredState;
    }

    public CanonicalAgentPackage Package { get; }

    public AgentInstalledTargetState TargetState { get; }

    public AgentReconcileAction Action { get; }

    public IReadOnlyList<AgentPlannedArtifact> Artifacts { get; }

    public AgentInstallationState DesiredState { get; }
}
