using MackySoft.AgentDistribution.Hosting.Commands;

namespace MackySoft.AgentDistribution.Hosting.Reporting;

/// <summary> Emits Agent Distribution command runtime results and returns process exit codes. </summary>
public interface IAgentDistributionCommandResultEmitter
{
    /// <summary> Emits one command result. </summary>
    /// <param name="result"> The command result to emit. </param>
    /// <param name="options"> The output formatting options. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes with the emitted result's process exit code. </returns>
    ValueTask<int> EmitAsync (
        AgentDistributionCommandResult result,
        AgentDistributionCommandOutputOptions options,
        CancellationToken cancellationToken = default);
}
