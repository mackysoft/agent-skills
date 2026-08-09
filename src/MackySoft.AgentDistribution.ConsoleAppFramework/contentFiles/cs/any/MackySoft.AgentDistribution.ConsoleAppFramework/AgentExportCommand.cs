#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class AgentExportCommand
{
    private readonly AgentCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public AgentExportCommand (AgentCommandRunner runner, IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Exports selected custom agents and their resolved SKILL dependencies. </summary>
    [Command("export")]
    public async Task<int> ExportAsync (
        string? host = null,
        string[]? category = null,
        string[]? agent = null,
        string? output = null,
        string? format = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ExportAsync(new AgentExportCommandRequest(host, category, agent, output, format), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}
