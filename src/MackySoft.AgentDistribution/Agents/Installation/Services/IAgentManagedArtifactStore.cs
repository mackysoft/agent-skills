using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

/// <summary> Defines the shared custom-agent artifact write and deletion transaction boundary. </summary>
internal interface IAgentManagedArtifactStore
{
    ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
        AgentReconciliationPlan plan,
        AgentResolvedTarget target,
        CancellationToken cancellationToken);

    ValueTask<AgentDistributionOperationResult<bool>> DeleteAsync (
        AgentInstallationState state,
        AbsolutePath statePath,
        AgentResolvedTarget target,
        Func<CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken);
}
