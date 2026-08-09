#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class AgentListCommand
{
    private readonly AgentCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public AgentListCommand (AgentCommandRunner runner, IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Lists custom agents and their resolved SKILL dependencies. </summary>
    [Command("list")]
    public async Task<int> ListAsync (
        string[]? category = null,
        string[]? agent = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ListAsync(new AgentListCommandRequest(category, agent), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}
